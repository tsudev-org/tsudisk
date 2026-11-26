using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using TsuDisk.Core.Integrity;

namespace TsuDisk.Core.Tests;

/// <summary>Xuat du lieu (APP_INTEGRITY I6, ADR-0012 muc 11): du tep + SHA-256, khong bi mat, khong theo lien ket.</summary>
public sealed class DataExportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "tsudisk-export-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 22, 0, 0, TimeSpan.Zero);

    public DataExportTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string Make(string rel, string content)
    {
        var p = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
        return p;
    }

    private static Dictionary<string, string> Read(string zip)
    {
        using var z = ZipFile.OpenRead(zip);
        return z.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var r = new StreamReader(e.Open());
            return r.ReadToEnd();
        });
    }

    [Fact]
    public void Xuat_du_tep_kem_manifest_SHA256_khop()
    {
        Make("logs/a.log", "nhat ky 1");
        Make("logs/sub/b.log", "nhat ky 2");
        var settings = Make("gui/giao-dien.json", "{\"theme\":\"dark\"}");
        var r = DataExport.Export(
            [new ExportSource("nhat-ky", Path.Combine(root, "logs")), new ExportSource("cai-dat", settings)],
            Path.Combine(root, "out"), "26.11.2701", Now);

        var entries = Read(r.ArchivePath);
        Assert.Equal(new[] { "cai-dat/giao-dien.json", "manifest.json", "nhat-ky/a.log", "nhat-ky/sub/b.log" }, entries.Keys.Order(StringComparer.Ordinal));
        using var m = JsonDocument.Parse(entries["manifest.json"]);
        Assert.Equal(DataExport.Format, m.RootElement.GetProperty("format").GetString());
        Assert.Equal("26.11.2701", m.RootElement.GetProperty("version").GetString());
        foreach (var f in m.RootElement.GetProperty("files").EnumerateArray())
        {
            var content = entries[f.GetProperty("path").GetString()!];
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))), f.GetProperty("sha256").GetString());
        }
        Assert.Equal(3, r.Files.Count);
        Assert.Empty(r.Skipped);
    }

    [Fact]
    public void Khong_xuat_bi_mat_thiet_bi()
    {
        Make("state/refresh-token.dpapi", "bi mat");
        Make("state/session.lock", "");
        Make("state/x.json.tmp", "tam");
        Make("state/state.json", "{}");
        var r = DataExport.Export([new ExportSource("trang-thai", Path.Combine(root, "state"))], Path.Combine(root, "out"), "1.0.0", Now);
        var entries = Read(r.ArchivePath);
        Assert.Contains("trang-thai/state.json", entries.Keys);
        Assert.DoesNotContain(entries.Keys, k => k.EndsWith(".dpapi", StringComparison.Ordinal) || k.EndsWith(".lock", StringComparison.Ordinal) || k.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.Equal(3, r.Skipped.Count);
    }

    [Fact]
    public void Khong_theo_lien_ket_tuong_trung_ra_ngoai_nguon()
    {
        var outside = Make("outside/secret.txt", "ngoai nguon");
        Make("logs/a.log", "x");
        File.CreateSymbolicLink(Path.Combine(root, "logs", "link.txt"), outside);
        Directory.CreateSymbolicLink(Path.Combine(root, "logs", "linkdir"), Path.Combine(root, "outside"));
        var r = DataExport.Export([new ExportSource("nhat-ky", Path.Combine(root, "logs"))], Path.Combine(root, "out"), "1.0.0", Now);
        var entries = Read(r.ArchivePath);
        Assert.DoesNotContain(entries.Values, v => v == "ngoai nguon");
        Assert.Contains("nhat-ky/link.txt", r.Skipped);
        Assert.Contains("nhat-ky/linkdir", r.Skipped);
    }

    [Fact]
    public void Ten_muc_zip_khong_co_duong_dan_nguy_hiem()
    {
        Make("logs/a.log", "x");
        var r = DataExport.Export([new ExportSource("nhat-ky", Path.Combine(root, "logs"))], Path.Combine(root, "out"), "1.0.0", Now);
        using var z = ZipFile.OpenRead(r.ArchivePath);
        Assert.All(z.Entries, e =>
        {
            Assert.DoesNotContain("..", e.FullName, StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(e.FullName));
            Assert.DoesNotContain('\\', e.FullName);
        });
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("A")]
    [InlineData("")]
    [InlineData("a/b")]
    public void Ten_thu_muc_trong_zip_sai_khuon_thi_tu_choi(string folder)
        => Assert.Throws<ArgumentException>(() => DataExport.Export([new ExportSource(folder, root)], Path.Combine(root, "out"), "1.0.0", Now));

    [Fact]
    public void Khong_ghi_de_tep_xuat_co_san()
    {
        var out1 = DataExport.Export([], Path.Combine(root, "out"), "1.0.0", Now);
        var out2 = DataExport.Export([], Path.Combine(root, "out"), "1.0.0", Now);
        Assert.NotEqual(out1.ArchivePath, out2.ArchivePath);
        Assert.True(File.Exists(out1.ArchivePath));
        Assert.EndsWith("-2.zip", out2.ArchivePath, StringComparison.Ordinal);
    }

    [Fact]
    public void Thu_muc_dich_nam_trong_nguon_khong_tu_chua_chinh_no()
    {
        Make("logs/a.log", "x");
        var r = DataExport.Export([new ExportSource("nhat-ky", Path.Combine(root, "logs"))], Path.Combine(root, "logs"), "1.0.0", Now);
        Assert.DoesNotContain(Read(r.ArchivePath).Keys, k => k.EndsWith(".zip", StringComparison.Ordinal));
    }

    [Fact]
    public void Nguon_khong_ton_tai_bi_bo_qua()
    {
        var r = DataExport.Export([new ExportSource("nhat-ky", Path.Combine(root, "khong-co"))], Path.Combine(root, "out"), "1.0.0", Now);
        Assert.Empty(r.Files);
        Assert.Contains("manifest.json", Read(r.ArchivePath).Keys);
    }

    [Fact]
    public void Tep_trung_ten_giua_hai_nguon_chi_giu_mot()
    {
        var a = Make("a/x.log", "1");
        var b = Make("b/x.log", "2");
        var r = DataExport.Export([new ExportSource("nhat-ky", a), new ExportSource("nhat-ky", b)], Path.Combine(root, "out"), "1.0.0", Now);
        Assert.Single(r.Files);
        Assert.Contains("nhat-ky/x.log", r.Skipped);
    }
}
