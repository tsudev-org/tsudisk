using System.Text.Json;
using System.Text.RegularExpressions;
using TsuDisk.Core.Integrity;

namespace TsuDisk.Core.Tests;

/// <summary>Cong phien ban cua CLI (ADR-0012 muc 7, 9; PLAN Phase INT3).</summary>
public sealed partial class CommandGateTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "tsudisk-cmdgate-" + Guid.NewGuid().ToString("N"));
    private readonly ManifestSigner signer = new();

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    /// <summary>Lenh ghi dia (doi bo cuc, dinh dang, ghi nhat ky/sao luu) - KHONG BAO GIO duoc chay khi khoa.</summary>
    private static readonly string[] DiskWriteCommands =
        ["mo-rong", "mo-rong-winre", "doi-ten", "tao-phan-vung", "thu-nho", "xoa-phan-vung", "sao-luu"];

    public static TheoryData<string> AllCommands()
    {
        var data = new TheoryData<string>();
        foreach (var c in CommandGate.KnownCommands.Order(StringComparer.Ordinal)) data.Add(c);
        return data;
    }

    private VersionGate Gate(IVersionSource source)
        => new(source, new VersionManifestCache(dir), () => ManifestSigner.Now, signer.Keys);

    private FakeVersionSource NewerOnGithub()
    {
        var m = ManifestSigner.Manifest(github: "26.11.2701");
        var sig = signer.Sign(m);
        return new FakeVersionSource(() => new VersionDocument(m, sig));
    }

    // --- Bang moi lenh (nghiem thu INT3) ---

    [Theory]
    [MemberData(nameof(AllCommands))]
    public async Task Bang_moi_lenh_khi_khoa_chi_4_lenh_duoc_chay(string command)
    {
        var source = NewerOnGithub();
        var d = await CommandGate.EvaluateAsync(command, () => true, () => Gate(source), "github", "26.11.2601", CancellationToken.None);
        var allowed = command is "phien-ban" or "tro-giup" or "dang-xuat" or "xuat-du-lieu";
        Assert.Equal(!allowed, CommandGate.MustBlock(command, d));
        if (allowed)
        {
            Assert.Null(d);                 // lenh duoc phep: khong kiem, khong mang
            Assert.Equal(0, source.Calls);
        }
        else
        {
            Assert.Equal(GateStatus.UpdateRequired, d!.Status);
            Assert.Equal(1, source.Calls);
        }
    }

    [Fact]
    public void Moi_lenh_ghi_dia_deu_co_trong_danh_sach_va_khong_duoc_phep_khi_khoa()
    {
        foreach (var c in DiskWriteCommands)
        {
            Assert.True(CommandGate.KnownCommands.Contains(c), c);
            Assert.False(CommandGate.IsAllowedWhenLocked(c), c);
            var locked = new GateDecision(GateStatus.UpdateRequired, AppVersion.TryParse("26.11.2601"), null, null, null, false);
            Assert.True(CommandGate.MustBlock(c, locked), c);
        }
    }

    [Fact]
    public void Lenh_la_chua_dang_ky_mac_dinh_bi_chan()
    {
        var locked = new GateDecision(GateStatus.UpdateRequired, null, null, null, null, false);
        Assert.True(CommandGate.MustBlock("lenh-moi-sau-nay", locked));
    }

    [Theory]
    [InlineData(GateStatus.Ok)]
    [InlineData(GateStatus.Unknown)]
    [InlineData(GateStatus.NotApplicable)]
    public void Chi_UpdateRequired_moi_chan(GateStatus status)
    {
        var d = new GateDecision(status, null, null, null, null, false);
        foreach (var c in CommandGate.KnownCommands) Assert.False(CommandGate.MustBlock(c, d), c);
        Assert.False(CommandGate.MustBlock("mo-rong", null));
    }

    // --- Q1: chua dang nhap thi khong co luot goi mang nao ---

    [Theory]
    [MemberData(nameof(AllCommands))]
    public async Task Chua_dang_nhap_khong_goi_mang_khong_tao_cong(string command)
    {
        var source = NewerOnGithub();
        var created = 0;
        var d = await CommandGate.EvaluateAsync(command, () => false, () => { created++; return Gate(source); },
            "github", "26.11.2601", CancellationToken.None);
        Assert.Null(d);
        Assert.Equal(0, created);
        Assert.Equal(0, source.Calls);
        Assert.False(Directory.Exists(dir));   // khong tep dem nao
    }

    [Fact]
    public async Task Lenh_duoc_phep_khong_doc_trang_thai_dang_nhap()
    {
        var asked = 0;
        foreach (var c in CommandGate.AllowedWhenLocked)
            await CommandGate.EvaluateAsync(c, () => { asked++; return true; }, () => throw new InvalidOperationException(), "github", "26.11.2601", CancellationToken.None);
        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task Da_dang_nhap_loi_mang_khong_dem_thi_khong_chan()
    {
        var source = new FakeVersionSource();   // nem HttpRequestException
        var d = await CommandGate.EvaluateAsync("mo-rong", () => true, () => Gate(source), "github", "26.11.2601", CancellationToken.None);
        Assert.Equal(GateStatus.Unknown, d!.Status);
        Assert.False(CommandGate.MustBlock("mo-rong", d));
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Kenh_khac_co_ban_moi_thi_khong_chan()
    {
        var source = NewerOnGithub();
        var d = await CommandGate.EvaluateAsync("mo-rong", () => true, () => Gate(source), "scoop", "26.11.2601", CancellationToken.None);
        Assert.Equal(GateStatus.Ok, d!.Status);
    }

    [Fact]
    public async Task Ban_tu_dung_khong_chan()
    {
        var source = NewerOnGithub();
        var d = await CommandGate.EvaluateAsync("mo-rong", () => true, () => Gate(source), "github", "0",CancellationToken.None);
        Assert.Equal(GateStatus.NotApplicable, d!.Status);
        Assert.False(CommandGate.MustBlock("mo-rong", d));
    }

    // --- Noi dung in ra ---

    [Fact]
    public void Dong_khoa_dung_nguyen_van_APP_INTEGRITY_va_huong_dan_theo_kenh()
    {
        var d = new GateDecision(GateStatus.UpdateRequired, AppVersion.TryParse("26.11.2601"), AppVersion.TryParse("26.11.2701"),
            new Uri("https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701"), null, false);
        var lines = CommandGate.LockedLines(d, "github");
        Assert.Equal("Cần cập nhật ứng dụng", lines[0]);
        Assert.Equal("Đã có phiên bản 26.11.2701. Để bảo vệ an toàn cho bạn và hệ thống, phiên bản 26.11.2601 không còn được sử dụng. Vui lòng cập nhật lên phiên bản mới nhất.", lines[1]);
        Assert.Contains(lines, l => l == "Cập nhật ngay: https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701");
        Assert.Contains(lines, l => l.StartsWith("Xuất dữ liệu: tsudisk xuat-du-lieu", StringComparison.Ordinal));
        Assert.Contains("Cập nhật ngay: scoop update tsudisk",CommandGate.LockedLines(d, "scoop"));
        Assert.Equal("winget upgrade tsudev.TsuDisk", CommandGate.UpdateHint("winget", d.Url));
        Assert.Equal("https://github.com/tsudev-org/tsudisk/releases/latest", CommandGate.UpdateHint("github", null));
    }

    [Fact]
    public void Json_khoa_co_error_latest_url()
    {
        var d = new GateDecision(GateStatus.UpdateRequired, AppVersion.TryParse("26.11.2601"), AppVersion.TryParse("26.11.2701"),
            new Uri("https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701"), null, false);
        using var j = JsonDocument.Parse(CommandGate.LockedJson(d, "winget"));
        Assert.Equal("update_required", j.RootElement.GetProperty("error").GetString());
        Assert.Equal("26.11.2701", j.RootElement.GetProperty("latest").GetString());
        Assert.Equal("26.11.2601", j.RootElement.GetProperty("current").GetString());
        Assert.Equal("https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701", j.RootElement.GetProperty("url").GetString());
        Assert.Equal("winget upgrade tsudev.TsuDisk", j.RootElement.GetProperty("update").GetString());
    }

    [Fact]
    public void Json_khoa_426_khong_noi_ban_nao_van_hop_le()
    {
        var d = VersionGate.FromServer(new Core.Pro.UpdateRequiredResponse(null, null), "26.11.2601");
        using var j = JsonDocument.Parse(CommandGate.LockedJson(d, "github"));
        Assert.Equal(JsonValueKind.Null, j.RootElement.GetProperty("latest").ValueKind);
        Assert.Contains("mới nhất", CommandGate.LockedLines(d, "github")[1], StringComparison.Ordinal);
    }

    // --- Doi chieu ma nguon CLI (CLI chi build tren Windows nen kiem bang van ban) ---

    [GeneratedRegex("""\] = "([a-z0-9-]+)"(?:,|\s|$)""")]
    private static partial Regex AliasTarget();

    [GeneratedRegex("""^\s+"([a-z0-9-]+)"(?: or "([a-z0-9-]+)")*(?: or "([a-z0-9-]+)")? =>""", RegexOptions.Multiline)]
    private static partial Regex SwitchArm();

    [Fact]
    public void CliArgs_chi_dung_lenh_da_dang_ky_va_dang_ky_du()
    {
        var text = File.ReadAllText(DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Cli", "CliArgs.cs"));
        var block = text[text.IndexOf("CommandAliases", StringComparison.Ordinal)..text.IndexOf("FlagAliases", StringComparison.Ordinal)];
        var targets = AliasTarget().Matches(block).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(CommandGate.KnownCommands.Order(StringComparer.Ordinal), targets.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Program_co_nhanh_cho_moi_lenh_va_goi_cong_truoc_lenh()
    {
        var text = File.ReadAllText(DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Cli", "Program.cs"));
        var arms = SwitchArm().Matches(text).SelectMany(m => m.Groups.Cast<Group>().Skip(1).SelectMany(g => g.Captures.Select(c => c.Value)))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var c in CommandGate.KnownCommands.Where(c => c != "tro-giup"))   // tro-giup = nhanh mac dinh "_"
            Assert.Contains(c, arms);
        var gate = text.IndexOf("CliGate.Check(cli)", StringComparison.Ordinal);
        var dispatch = text.IndexOf("return cli.Command switch", StringComparison.Ordinal);
        Assert.True(gate > 0 && gate < dispatch, "Cong phai chay truoc khi chon lenh");
    }
}
