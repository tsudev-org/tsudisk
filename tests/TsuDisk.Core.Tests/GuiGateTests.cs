using System.Text.RegularExpressions;
using System.Xml.Linq;
using TsuDisk.Core.Integrity;
using TsuDisk.Core.Presentation;
using TsuDisk.Core.Pro;

namespace TsuDisk.Core.Tests;

/// <summary>
/// PLAN Phase INT4: cong phien ban cua GUI (lich kiem, cho thao tac dia, nut theo kenh), dau 426 dung chung CLI/GUI
/// (ADR-0012 muc 8), tuong phan token cua modal, va rang buoc van ban ma GUI (khong co co bo qua cong).
/// </summary>
public sealed partial class GuiGateTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "tsudisk-guigate-" + Guid.NewGuid().ToString("N"));
    private readonly ManifestSigner signer = new();
    private DateTimeOffset now = ManifestSigner.Now;

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static readonly AppVersion Current = AppVersion.TryParse("26.11.2601")!;

    private static GateDecision Locked() =>
        new(GateStatus.UpdateRequired, Current, AppVersion.TryParse("26.11.2701"), UpdateAction.ReleasesPage, null, false);

    private static GateDecision Ok() => new(GateStatus.Ok, Current, null, null, null, false);

    // --- Lich kiem (ADR-0012 muc 7) ---

    [Fact]
    public void Hang_so_dung_APP_INTEGRITY()
    {
        Assert.Equal(TimeSpan.FromHours(6), GuiGateSchedule.Period);
        Assert.Equal(TimeSpan.FromMinutes(30), GuiGateSchedule.ReactivateAfter);
        Assert.Equal(TimeSpan.FromSeconds(5), VersionGate.FetchTimeout);
    }

    [Fact]
    public void Kich_hoat_lai_chi_kiem_khi_o_nen_tu_30_phut()
    {
        var s = new GuiGateSchedule(() => now);
        Assert.False(s.Activated());                      // chua tung roi focus
        s.Deactivated();
        now += TimeSpan.FromMinutes(29);
        Assert.False(s.Activated());
        s.Deactivated();
        now += TimeSpan.FromMinutes(30);
        Assert.True(s.Activated());
        Assert.False(s.Activated());                      // moc da dung
    }

    [Fact]
    public void Roi_focus_nhieu_lan_tinh_tu_lan_dau()
    {
        var s = new GuiGateSchedule(() => now);
        s.Deactivated();
        now += TimeSpan.FromMinutes(20);
        s.Deactivated();                                  // vd. hop thoai con cua Windows
        now += TimeSpan.FromMinutes(10);
        Assert.True(s.Activated());
    }

    [Fact]
    public void Khong_chay_hai_luot_kiem_chong_nhau_va_thoi_kiem_sau_khi_khoa()
    {
        var s = new GuiGateSchedule(() => now);
        Assert.True(s.TryBeginCheck());
        Assert.False(s.TryBeginCheck());
        Assert.Null(s.EndCheck(Ok(), busy: false));
        Assert.True(s.TryBeginCheck());
        Assert.NotNull(s.EndCheck(Locked(), busy: false));
        Assert.False(s.TryBeginCheck());
    }

    [Fact]
    public void Khoa_khi_ranh_hien_ngay_mot_lan()
    {
        var s = new GuiGateSchedule(() => now);
        s.TryBeginCheck();
        var show = s.EndCheck(Locked(), busy: false);
        Assert.NotNull(show);
        Assert.True(s.Shown);
        Assert.Null(s.Idle());                            // khong hien lan hai
    }

    [Fact]
    public void Dang_ghi_dia_thi_cho_xong_moi_hien()
    {
        var s = new GuiGateSchedule(() => now);
        s.TryBeginCheck();
        Assert.Null(s.EndCheck(Locked(), busy: true));
        Assert.True(s.IsLocked);
        Assert.False(s.Shown);
        var show = s.Idle();
        Assert.NotNull(show);
        Assert.Equal("26.11.2701", show.Latest!.Text);
        Assert.Null(s.Idle());
    }

    [Theory]
    [InlineData(GateStatus.Ok)]
    [InlineData(GateStatus.Unknown)]
    [InlineData(GateStatus.NotApplicable)]
    public void Chi_UpdateRequired_moi_khoa_GUI(GateStatus status)
    {
        var s = new GuiGateSchedule(() => now);
        s.TryBeginCheck();
        Assert.Null(s.EndCheck(new GateDecision(status, Current, null, null, null, false), busy: false));
        Assert.Null(s.EndCheck(null, busy: false));       // chua dang nhap / loi ⇒ null
        Assert.False(s.IsLocked);
        Assert.Null(s.Idle());
    }

    // --- Nut "Cap nhat ngay" theo kenh (ADR-0012 muc 10) ---

    [Fact]
    public void Nut_cap_nhat_theo_kenh()
    {
        var url = new Uri("https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701");
        Assert.Equal(new UpdateAction(url, null), UpdateAction.For("github", url));
        Assert.Equal(new UpdateAction(null, "scoop update tsudisk"), UpdateAction.For("scoop", url));
        Assert.Equal(new UpdateAction(null, "winget upgrade tsudev.TsuDisk"), UpdateAction.For("winget", url));
        Assert.Equal(UpdateAction.ReleasesPage, UpdateAction.For("github", null).OpenUrl);
        Assert.Equal(UpdateAction.ReleasesPage, UpdateAction.For("github", new Uri("http://evil.test/x")).OpenUrl);
        Assert.Equal(UpdateAction.ReleasesPage, UpdateAction.For("github", new Uri("file:///C:/x.exe")).OpenUrl);
    }

    // --- Dau 426 (ADR-0012 muc 8) ---

    private VersionGate Gate(IVersionSource source) =>
        new(source, new VersionManifestCache(dir), () => now, signer.Keys, new ServerLockStore(dir));

    private FakeVersionSource SameOnGithub()
    {
        var m = ManifestSigner.Manifest(github: "26.11.2601");
        var sig = signer.Sign(m);
        return new FakeVersionSource(() => new VersionDocument(m, sig));
    }

    [Fact]
    public async Task Dau_426_khoa_cong_khong_goi_mang()
    {
        new ServerLockStore(dir).Save(new UpdateRequiredResponse("26.11.2701",
            new Uri("https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701")), "26.11.2601", now);
        var source = SameOnGithub();
        var d = await Gate(source).CheckAsync(VersionKeys.Product, "github", "26.11.2601", CancellationToken.None);
        Assert.True(d.IsLocked);
        Assert.Equal("26.11.2701", d.Latest!.Text);
        Assert.Equal("v26.11.2701", d.Url!.Segments[^1]);
        Assert.Equal(0, source.Calls);
        Assert.True(CommandGate.MustBlock("mo-rong", d));
        Assert.False(CommandGate.MustBlock("xuat-du-lieu", d));
    }

    [Fact]
    public async Task Dau_426_cua_ban_khac_hoac_het_han_thi_bo_qua()
    {
        var store = new ServerLockStore(dir);
        store.Save(new UpdateRequiredResponse("26.11.2601", null), "26.11.2501", now);   // da nang cap tu 2501
        var d = await Gate(SameOnGithub()).CheckAsync(VersionKeys.Product, "github", "26.11.2601", CancellationToken.None);
        Assert.Equal(GateStatus.Ok, d.Status);

        store.Save(new UpdateRequiredResponse(null, null), "26.11.2601", now - ServerLockStore.Lifetime);
        d = await Gate(SameOnGithub()).CheckAsync(VersionKeys.Product, "github", "26.11.2601", CancellationToken.None);
        Assert.Equal(GateStatus.Ok, d.Status);

        store.Save(new UpdateRequiredResponse(null, null), "26.11.2601", now + TimeSpan.FromHours(1));   // dong ho lui
        Assert.Null(store.Load(Current, now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong phai json")]
    [InlineData("[]")]
    [InlineData("""{"current":123,"at":1796000000}""")]
    [InlineData("""{"current":"26.11.2601","at":"x"}""")]
    [InlineData("""{"current":"26.11.2601"}""")]
    [InlineData("""{"current":"26.11.2601","at":99999999999999999}""")]
    public void Tep_dau_hong_coi_nhu_khong_co(string content)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ServerLockStore.FileName), content);
        Assert.Null(new ServerLockStore(dir).Load(Current, now));
    }

    [Fact]
    public void Dau_khong_dung_url_khong_https_va_426_khong_noi_ban()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ServerLockStore.FileName),
            $$"""{"current":"26.11.2601","latest":null,"url":"http://evil.test/","at":{{now.ToUnixTimeSeconds()}}}""");
        var d = new ServerLockStore(dir).Load(Current, now)!;
        Assert.True(d.IsLocked);
        Assert.Null(d.Url);
        Assert.Null(d.Latest);
        Assert.Equal("Đã có phiên bản mới nhất. Để bảo vệ an toàn cho bạn và hệ thống, phiên bản 26.11.2601 không còn được sử dụng. Vui lòng cập nhật lên phiên bản mới nhất.",
            UpdateRequiredText.Vi.Body(d.Latest, d.Current));
    }

    [Fact]
    public void Khong_dau_thi_khong_tao_tep()
    {
        Assert.Null(new ServerLockStore(dir).Load(Current, now));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task ProAccount_nhan_426_bao_UpdateRequired_va_goi_callback()
    {
        using var rig = new ProRig();
        Assert.True((await rig.LoginAsync()).IsSuccess);
        rig.Idp.UpgradeRequired = true;
        var r = await rig.Account.RefreshAsync(CancellationToken.None);
        Assert.Equal(RefreshStatus.UpdateRequired, r.Status);
        var u = Assert.Single(rig.Upgrades);
        Assert.Equal("26.11.2701", u.Latest);
        Assert.Equal(Uri.UriSchemeHttps, u.Url!.Scheme);
        Assert.True(rig.Account.IsSignedIn);            // 426 KHONG xoa phien
        Assert.NotNull(r.Entitlement);                   // giu JWS dem cu
    }

    [Fact]
    public async Task ProAccount_dang_nhap_luc_may_chu_tra_426_van_goi_callback()
    {
        using var rig = new ProRig();
        rig.Idp.UpgradeRequired = true;
        var login = await rig.LoginAsync();
        Assert.True(login.IsSuccess);
        Assert.Null(login.Entitlement);
        Assert.Single(rig.Upgrades);
    }

    // --- Nguon Xuat du lieu dung chung CLI/GUI (ADR-0012 muc 11) ---

    [Fact]
    public void Nguon_xuat_khong_co_thu_muc_trang_thai_Pro()
    {
        var sources = DataExport.StandardSources(dir);
        Assert.Equal(["nhat-ky", "cai-dat"], sources.Select(s => s.ArchiveFolder));
        Assert.Equal(Path.GetFullPath(dir), sources[0].Path);
        var pro = ProContract.DefaultStateDirectory();
        Assert.DoesNotContain(sources, s => s.Path.StartsWith(pro, StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(Path.Combine("TsuDisk", "nhat-ky"), DataExport.StandardSources()[0].Path, StringComparison.Ordinal);
    }

    // --- Tuong phan chu cua modal >= 4,5:1 (ACCESSIBILITY, nghiem thu INT4) ---

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Tuong_phan_chu_modal_dat_AA(ThemeMode mode)
    {
        var t = DesignTokens.Build(Token("design-tokens.json"), Token("products", "disk.json"), mode);
        (string fg, string bg)[] pairs =
        [
            ("text-primary", "bg-surface"), ("text-secondary", "bg-surface"), ("text-primary", "bg-base"),
            ("on-primary", "primary"), ("text-primary", "bg-hover"),
        ];
        foreach (var (fg, bg) in pairs)
        {
            var ratio = Contrast(t.Color(fg), t.Color(bg));
            Assert.True(ratio >= 4.5, $"{mode} {fg}/{bg} = {ratio:0.00}");
        }
    }

    private static string Token(params string[] parts) =>
        File.ReadAllText(DocumentationConsistencyTests.RepoPath([".standards", "tokens", .. parts]));

    private static double Contrast(TokenColor a, TokenColor b)
    {
        static double L(TokenColor c)
        {
            static double Ch(byte v)
            {
                var s = v / 255.0;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }
        var (x, y) = (L(a), L(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    // --- Rang buoc van ban ma GUI ---

    private static string Gui(string file) => File.ReadAllText(DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Gui", file));

    [Fact]
    public void Modal_dung_nguyen_van_Core_va_dung_hai_nut()
    {
        var xaml = XDocument.Parse(Gui("UpdateRequiredWindow.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var buttons = xaml.Descendants(p + "Button").ToList();
        Assert.Equal(2, buttons.Count);
        Assert.Equal(["ExportDataButton", "UpdateNowButton"],
            buttons.Select(b => (string?)b.Attribute("AutomationProperties.AutomationId")).Order(StringComparer.Ordinal));
        var root = xaml.Root!;
        Assert.Equal("None", (string?)root.Attribute("WindowStyle"));
        Assert.Equal("Maximized", (string?)root.Attribute("WindowState"));
        Assert.Equal("Cycle", (string?)root.Attribute("KeyboardNavigation.TabNavigation"));
        Assert.Equal("UpdateRequiredWindow", (string?)root.Attribute("AutomationProperties.AutomationId"));
        Assert.DoesNotContain("IsCancel", Gui("UpdateRequiredWindow.xaml"), StringComparison.Ordinal);

        var code = Gui("UpdateRequiredWindow.xaml.cs");
        Assert.Contains("UpdateRequiredText.For(", code, StringComparison.Ordinal);
        Assert.Contains("Key.Escape", code, StringComparison.Ordinal);
        Assert.Contains("e.SystemKey == Key.F4", code, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true", code, StringComparison.Ordinal);
        Assert.Contains("DataExport.StandardSources(", code, StringComparison.Ordinal);
    }

    [Fact]
    public void GUI_khong_co_co_hay_bien_moi_truong_bo_qua_cong()
    {
        var app = Gui("App.xaml.cs");
        var gate = Gui("GuiGate.cs");
        // Moi lan mo that (khong chup man hinh) deu khoi dong cong, tru nhanh xem MAU (cung la modal khoa).
        Assert.Matches(@"new GuiGate\(window, model, Value\(""--thu-muc-nhat-ky""\)\)\.Start\(\)", app);
        Assert.DoesNotContain("GetEnvironmentVariable", app + gate, StringComparison.Ordinal);
        // Nhanh "--xem-khoa-mau" chi chay kem du lieu mau va CHINH NO hien modal khoa.
        Assert.Matches(@"if \(sample && args\.Contains\(""--xem-khoa-mau""\)\)\s*\{\s*await model\.LoadAsync\(\)\.ConfigureAwait\(true\);\s*GuiGate\.Show\(", app);
        // Mot co dong lenh duy nhat duoc doc trong App: liet ke va doi chieu (them co moi ⇒ phai xem lai test nay).
        var flags = FlagLiteral().Matches(app).Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["--chi-o-ao", "--chup-man-hinh", "--dia", "--du-lieu-mau", "--thu-muc-nhat-ky", "--xem-khoa-mau"], flags);
        Assert.Contains("VersionKeys.Product", gate, StringComparison.Ordinal);
        Assert.Contains("AllowAutoRedirect = false", gate, StringComparison.Ordinal);
        Assert.Contains("new ServerLockStore(", gate, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"""(--[a-z-]+)""")]
    private static partial Regex FlagLiteral();

    [Fact]
    public void Chuoi_modal_co_du_vi_en()
    {
        string[] keys = ["LockCommandCopied", "LockCommandShown", "LockOpened", "LockExportPick", "LockExporting", "LockExported", "LockExportFailed"];
        foreach (var file in new[] { "Strings.resx", "Strings.vi.resx" })
        {
            var names = XDocument.Parse(Gui(Path.Combine("Resources", file))).Descendants("data").Select(d => (string?)d.Attribute("name")).ToHashSet();
            foreach (var k in keys)
                Assert.Contains(k, names);
        }
    }
}
