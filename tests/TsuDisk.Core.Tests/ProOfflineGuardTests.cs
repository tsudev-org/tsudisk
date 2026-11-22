using TsuDisk.Core.Pro;

namespace TsuDisk.Core.Tests;

/// <summary>
/// CANH HANG C (QU-STD-AUTH, ADR-0002 muc 2.1, PRO1 P3 muc e): khi CHUA dang nhap, KHONG duong nao
/// ngoai <c>tsudisk dang-nhap</c> duoc goi mang hay de lai tep. Bo xu ly HTTP o day nem loi va dem moi luot
/// goi; thu muc du lieu phai van khong ton tai sau tat ca.
/// </summary>
public class ProOfflineGuardTests
{
    [Fact]
    public async Task Chua_dang_nhap_moi_duong_deu_khong_goi_mang_khong_tao_tep()
    {
        var guard = new NoNetworkHandler();
        using var rig = new ProRig(guard);
        var a = rig.Account;

        Assert.False(a.IsSignedIn);
        Assert.Null(a.Account);
        Assert.Same(NoEntitlementSource.Instance, a.CachedSource());
        Assert.Same(NoEntitlementSource.Instance, await a.ForRunAsync(interactive: true, CancellationToken.None));
        Assert.Same(NoEntitlementSource.Instance, await a.ForRunAsync(interactive: false, CancellationToken.None));
        Assert.Equal(RefreshStatus.NotSignedIn, (await a.RefreshAsync(CancellationToken.None)).Status);
        Assert.Null(await a.LogoutAsync(CancellationToken.None));

        // Duong cua lan quet tuong tac: ProNoticePrompt voi nguon tu ForRunAsync.
        var output = new StringWriter();
        var choice = new ProNoticePrompt(await a.ForRunAsync(true, CancellationToken.None),
                                         new LocalStateStore(rig.Dir.Path), ProTestKit.ClientId, output,
                                         () => throw new InvalidOperationException("khong duoc cho phim"),
                                         _ => throw new InvalidOperationException("khong duoc mo URL"))
            .Run(rig.Now, interactive: true, "C:\\bao-cao");
        Assert.Equal(ProNoticeChoice.NotShown, choice);
        Assert.Equal("", output.ToString());

        Assert.Equal(0, guard.Calls);
        Assert.False(Directory.Exists(rig.Dir.Path));
    }

    [Fact]
    public async Task Tep_rac_khong_phai_phien_cung_khong_kich_hoat_mang()
    {
        // Con ma cai dat / state.json / JWS cu nhung KHONG co refresh token = chua dang nhap.
        var guard = new NoNetworkHandler();
        using var rig = new ProRig(guard);
        Directory.CreateDirectory(rig.Dir.Path);
        rig.Installs.GetOrCreate();
        File.WriteAllText(Path.Combine(rig.Dir.Path, ProAccountStore.EntitlementFile), "a.b.c");
        File.WriteAllText(Path.Combine(rig.Dir.Path, ProAccountStore.JwksFile), rig.Kit.JwksJson());

        Assert.False(rig.Account.IsSignedIn);
        Assert.Same(NoEntitlementSource.Instance, await rig.Account.ForRunAsync(true, CancellationToken.None));
        Assert.Equal(RefreshStatus.NotSignedIn, (await rig.Account.RefreshAsync(CancellationToken.None)).Status);
        Assert.Equal(0, guard.Calls);
    }

    /// <summary>
    /// Lop Cli (net10.0-windows) khong chay duoc tren Linux - canh bang van ban nguon: ProHook.Run thoat TRUOC moi
    /// luot goi mang khi chua dang nhap, va khong hien o --json / chay tu dong / vao-ra bi chuyen huong.
    /// </summary>
    [Fact]
    public void CLI_thoat_truoc_khi_goi_mang_va_khong_hien_khi_chay_tu_dong()
    {
        var root = RepoRoot();
        var pro = File.ReadAllText(Path.Combine(root, "src", "TsuDisk.Cli", "ProHook.cs"));
        var body = pro[pro.IndexOf("public static void Run", StringComparison.Ordinal)..];
        var guard = body.IndexOf("if (!account.IsSignedIn) return;", StringComparison.Ordinal);
        Assert.True(guard > 0, "ProHook.Run phai kiem IsSignedIn");
        Assert.True(guard < body.IndexOf("ForRunAsync", StringComparison.Ordinal), "kiem IsSignedIn TRUOC ForRunAsync");
        Assert.True(body.IndexOf("if (!IsInteractive(cli)) return;", StringComparison.Ordinal) is >= 0 and var i && i < guard);
        foreach (var flag in new[] { "\"json\"", "\"dong-y\"", "\"xac-nhan\"", "IsInputRedirected", "IsOutputRedirected" })
            Assert.Contains(flag, pro[..pro.IndexOf("private static HttpClient", StringComparison.Ordinal)], StringComparison.Ordinal);
        // Nhac Pro CHI IN, khong cho phim (khong chan lenh dia).
        Assert.DoesNotContain("Console.ReadLine", pro, StringComparison.Ordinal);

        var main = File.ReadAllText(Path.Combine(root, "src", "TsuDisk.Cli", "Program.cs"));
        Assert.Contains("ProHook.Run(cli, Commands.DefaultLogRoot);", main, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "TsuDisk.slnx"))) return d.FullName;
        throw new InvalidOperationException("Khong tim thay goc repo.");
    }
}
