using System.Net;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Security;
using TsuDisk.Core.Integrity;
using TsuDisk.Core.Pro;
using Ed25519 = Org.BouncyCastle.Math.EC.Rfc8032.Ed25519;

namespace TsuDisk.Core.Tests;

/// <summary>Bo khoa Ed25519 thu + ky ban ke giong content-service cua tsudev.com (JWS tach noi dung, EdDSA).</summary>
internal sealed class ManifestSigner
{
    private readonly byte[] secret = new byte[32];
    public byte[] PublicKey { get; } = new byte[32];
    public string Kid { get; }

    public ManifestSigner(string kid = "tsu-versions-test")
    {
        Kid = kid;
        Ed25519.GeneratePrivateKey(new SecureRandom(), secret);
        Ed25519.GeneratePublicKey(secret, 0, PublicKey, 0);
    }

    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Keys
        => new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal) { [Kid] = PublicKey };

    public string Sign(byte[] payload, string? headerJson = null)
    {
        var header = Base64Url.Encode(Encoding.UTF8.GetBytes(headerJson ?? $"{{\"alg\":\"EdDSA\",\"kid\":\"{Kid}\"}}"));
        var input = Encoding.ASCII.GetBytes(header + "." + Base64Url.Encode(payload));
        var sig = new byte[64];
        Ed25519.Sign(secret, 0, input, 0, input.Length, sig, 0);
        return header + ".." + Base64Url.Encode(sig);
    }

    public static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_796_000_000);

    public static byte[] Manifest(string? github = "26.11.2701", long? issued = null, long lifetime = 7 * 24 * 3600,
        string? scoop = null, int v = 1, string url = "https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701")
    {
        var iss = issued ?? Now.ToUnixTimeSeconds() - 3600;
        var channels = new Dictionary<string, object>();
        if (github is not null) channels["github"] = new Dictionary<string, string> { ["latest"] = github, ["url"] = url };
        if (scoop is not null) channels["scoop"] = new Dictionary<string, string> { ["latest"] = scoop, ["url"] = url };
        var doc = new Dictionary<string, object>
        {
            ["v"] = v,
            ["issued_at"] = iss,
            ["expires_at"] = iss + lifetime,
            ["products"] = new Dictionary<string, object> { ["tsudisk"] = new Dictionary<string, object> { ["channels"] = channels } },
        };
        return JsonSerializer.SerializeToUtf8Bytes(doc);
    }
}

internal sealed class FakeVersionSource(Func<VersionDocument>? fetch = null) : IVersionSource
{
    public int Calls { get; private set; }

    public Task<VersionDocument> FetchAsync(CancellationToken cancel)
    {
        Calls++;
        return fetch is null ? throw new HttpRequestException("mang loi") : Task.FromResult(fetch());
    }
}

public sealed class VersionGateTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "tsudisk-gate-" + Guid.NewGuid().ToString("N"));
    private readonly ManifestSigner signer = new();

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static string Fixture(string name) => DocumentationConsistencyTests.RepoPath("tests", "TsuDisk.Core.Tests", "Fixtures", "versions", name);

    private ManifestResult Check(byte[] m, string? sig, long? minIssued = null, DateTimeOffset? now = null)
        => VersionManifestVerifier.Verify(m, sig, now ?? ManifestSigner.Now, minIssued, signer.Keys);

    private VersionGate Gate(IVersionSource source, DateTimeOffset? now = null)
        => new(source, new VersionManifestCache(dir), () => now ?? ManifestSigner.Now, signer.Keys);

    private VersionDocument Doc(byte[] m) => new(m, signer.Sign(m));

    // --- Du lieu that production (26/11/2026) ---

    [Fact]
    public void Ban_ke_production_qua_kiem_bang_khoa_ghim()
    {
        var m = File.ReadAllBytes(Fixture("versions.json"));
        var sig = File.ReadAllText(Fixture("versions.json.sig"));
        using var doc = JsonDocument.Parse(m);
        var issued = doc.RootElement.GetProperty("issued_at").GetInt64();
        var r = VersionManifestVerifier.Verify(m, sig, DateTimeOffset.FromUnixTimeSeconds(issued + 60), null, VersionKeys.Production);
        Assert.Equal(ManifestCheck.Ok, r.Status);
        Assert.Null(r.Manifest!.Find("tsudisk", "github"));
    }

    [Fact]
    public void Ban_ke_production_doi_mot_byte_thi_chu_ky_sai()
    {
        var m = File.ReadAllBytes(Fixture("versions.json"));
        m[^2] ^= 0x01;
        var r = VersionManifestVerifier.Verify(m, File.ReadAllText(Fixture("versions.json.sig")), ManifestSigner.Now, null, VersionKeys.Production);
        Assert.Equal(ManifestCheck.BadSignature, r.Status);
    }

    [Fact]
    public void VersionKeys_khop_JWKS_production()
    {
        using var jwks = JsonDocument.Parse(File.ReadAllText(Fixture("versions-jwks.json")));
        var keys = jwks.RootElement.GetProperty("keys").EnumerateArray().ToList();
        Assert.Equal(VersionKeys.Production.Count, keys.Count);
        foreach (var k in keys)
        {
            Assert.Equal("OKP", k.GetProperty("kty").GetString());
            Assert.Equal("Ed25519", k.GetProperty("crv").GetString());
            var pinned = VersionKeys.Production[k.GetProperty("kid").GetString()!];
            Assert.Equal(Base64Url.TryDecode(k.GetProperty("x").GetString()), pinned.ToArray());
        }
    }

    [Fact]
    public void URL_ban_ke_la_https_tsudev_com_co_dinh()
    {
        Assert.Equal("https://tsudev.com/.well-known/tsudev/versions.json", VersionKeys.ManifestUrl);
        Assert.Equal(VersionKeys.ManifestUrl + ".sig", VersionKeys.SignatureUrl);
        Assert.Equal("tsudisk", VersionKeys.Product);
    }

    // --- Chu ky ---

    [Fact]
    public void Chu_ky_dung_thi_doc_duoc_kenh()
    {
        var m = ManifestSigner.Manifest();
        var r = Check(m, signer.Sign(m));
        Assert.Equal(ManifestCheck.Ok, r.Status);
        Assert.Equal("26.11.2701", r.Manifest!.Find("tsudisk", "github")!.Latest.Text);
    }

    [Fact]
    public void Chu_ky_co_xuong_dong_cuoi_van_nhan()
    {
        var m = ManifestSigner.Manifest();
        Assert.Equal(ManifestCheck.Ok, Check(m, signer.Sign(m) + "\n").Status);
    }

    [Fact]
    public void Khoa_khac_ky_cung_kid_thi_chu_ky_sai()
    {
        var m = ManifestSigner.Manifest();
        var other = new ManifestSigner(signer.Kid);
        Assert.Equal(ManifestCheck.BadSignature, Check(m, other.Sign(m)).Status);
    }

    [Fact]
    public void Kid_la_thi_tu_choi()
    {
        var m = ManifestSigner.Manifest();
        Assert.Equal(ManifestCheck.UnknownKey, Check(m, new ManifestSigner("tsu-versions-2099").Sign(m)).Status);
    }

    [Theory]
    [InlineData("{\"alg\":\"HS256\",\"kid\":\"tsu-versions-test\"}")]
    [InlineData("{\"alg\":\"none\",\"kid\":\"tsu-versions-test\"}")]
    [InlineData("{\"alg\":\"ES256\",\"kid\":\"tsu-versions-test\"}")]
    public void Thuat_toan_khac_EdDSA_thi_tu_choi(string header)
    {
        var m = ManifestSigner.Manifest();
        Assert.Equal(ManifestCheck.UnsupportedAlgorithm, Check(m, signer.Sign(m, header)).Status);
    }

    [Theory]
    [InlineData("{\"alg\":\"EdDSA\",\"kid\":\"tsu-versions-test\",\"crit\":\"b64\"}")]
    [InlineData("{\"alg\":\"EdDSA\",\"kid\":\"tsu-versions-test\",\"jwk\":\"x\"}")]
    [InlineData("{\"alg\":\"EdDSA\",\"alg\":\"EdDSA\",\"kid\":\"tsu-versions-test\"}")]
    [InlineData("{\"kid\":\"tsu-versions-test\"}")]
    [InlineData("[\"EdDSA\"]")]
    [InlineData("{\"alg\":\"EdDSA\",\"kid\":5}")]
    [InlineData("khong phai json")]
    public void Header_la_hoac_trung_khoa_thi_tu_choi(string header)
    {
        var m = ManifestSigner.Manifest();
        Assert.Equal(ManifestCheck.SignatureMalformed, Check(m, signer.Sign(m, header)).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    public void Chu_ky_sai_khuon_thi_tu_choi(string? sig)
        => Assert.Equal(ManifestCheck.SignatureMalformed, Check(ManifestSigner.Manifest(), sig).Status);

    [Fact]
    public void Chu_ky_khong_tach_noi_dung_thi_tu_choi()
    {
        var m = ManifestSigner.Manifest();
        var parts = signer.Sign(m).Split('.');
        Assert.Equal(ManifestCheck.SignatureMalformed, Check(m, parts[0] + "." + Base64Url.Encode(m) + "." + parts[2]).Status);
    }

    [Fact]
    public void Header_khong_phai_base64url_thi_tu_choi()
    {
        var m = ManifestSigner.Manifest();
        var parts = signer.Sign(m).Split('.');
        Assert.Equal(ManifestCheck.SignatureMalformed, Check(m, "ey+J/=.." + parts[2]).Status);
    }

    [Fact]
    public void Chu_ky_qua_dai_thi_tu_choi()
        => Assert.Equal(ManifestCheck.SignatureMalformed, Check(ManifestSigner.Manifest(), new string('a', DetachedJws.MaxLength + 1)).Status);

    [Fact]
    public void Chu_ky_toan_so_0_thi_sai()
    {
        var m = ManifestSigner.Manifest();
        var parts = signer.Sign(m).Split('.');
        Assert.Equal(ManifestCheck.BadSignature, Check(m, parts[0] + ".." + Base64Url.Encode(new byte[64])).Status);
    }

    [Fact]
    public void Chu_ky_cat_ngan_thi_tu_choi()
    {
        var m = ManifestSigner.Manifest();
        var sig = signer.Sign(m);
        Assert.Equal(ManifestCheck.SignatureMalformed, Check(m, sig[..^4]).Status);
    }

    [Fact]
    public void Ban_ke_qua_lon_bi_bo_truoc_khi_kiem()
        => Assert.Equal(ManifestCheck.TooLarge, Check(new byte[VersionManifestVerifier.MaxManifestBytes + 1], "x..y").Status);

    // --- Noi dung + thoi han ---

    [Fact]
    public void Het_han_thi_bo()
    {
        var m = ManifestSigner.Manifest(issued: ManifestSigner.Now.ToUnixTimeSeconds() - 8 * 24 * 3600);
        var r = Check(m, signer.Sign(m));
        Assert.Equal(ManifestCheck.Expired, r.Status);
        Assert.Null(r.Manifest);
        Assert.NotNull(r.IssuedAt);
    }

    [Fact]
    public void Song_qua_7_ngay_thi_bo()
    {
        var m = ManifestSigner.Manifest(lifetime: 7 * 24 * 3600 + 1);
        Assert.Equal(ManifestCheck.LifetimeTooLong, Check(m, signer.Sign(m)).Status);
    }

    [Fact]
    public void Issued_at_cu_hon_ban_dem_thi_bo_chong_phat_lai()
    {
        var m = ManifestSigner.Manifest();
        var issued = ManifestSigner.Now.ToUnixTimeSeconds() - 3600;
        Assert.Equal(ManifestCheck.Replayed, Check(m, signer.Sign(m), minIssued: issued + 1).Status);
        Assert.Equal(ManifestCheck.Ok, Check(m, signer.Sign(m), minIssued: issued).Status);
    }

    [Fact]
    public void Dinh_dang_v_khac_1_thi_bo()
    {
        var m = ManifestSigner.Manifest(v: 2);
        Assert.Equal(ManifestCheck.UnsupportedFormat, Check(m, signer.Sign(m)).Status);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"v\":1}")]
    [InlineData("{\"v\":1,\"issued_at\":10,\"expires_at\":5,\"products\":{}}")]
    [InlineData("{\"v\":1,\"issued_at\":10,\"expires_at\":20,\"products\":[]}")]
    [InlineData("khong phai json")]
    public void Noi_dung_hong_du_chu_ky_dung_thi_bo(string json)
    {
        var m = Encoding.UTF8.GetBytes(json);
        Assert.Equal(ManifestCheck.InvalidContent, Check(m, signer.Sign(m)).Status);
    }

    [Fact]
    public void Muc_kenh_hong_bi_bo_qua_khong_khoa()
    {
        var m = ManifestSigner.Manifest(github: "moi-nhat", url: "http://github.com/x");
        var r = Check(m, signer.Sign(m));
        Assert.Equal(ManifestCheck.Ok, r.Status);
        Assert.Null(r.Manifest!.Find("tsudisk", "github"));
    }

    [Fact]
    public void Url_khong_phai_https_thi_bo_url_giu_phien_ban()
    {
        var m = ManifestSigner.Manifest(url: "http://github.com/x");
        var r = Check(m, signer.Sign(m)).Manifest!.Find("tsudisk", "github")!;
        Assert.Null(r.Url);
        Assert.Equal("26.11.2701", r.Latest.Text);
    }

    // --- So phien ban ---

    [Theory]
    [InlineData("26.11.2601-beta.1", "26.11.2601", -1)]
    [InlineData("26.11.2601", "26.11.2602", -1)]
    [InlineData("26.11.2601", "26.12.0101", -1)]
    [InlineData("26.12.3199", "27.1.0101", -1)]
    [InlineData("26.11.2601-beta.2", "26.11.2601-beta.10", -1)]
    [InlineData("26.11.2601-beta.9", "26.11.2601-rc.1", -1)]
    [InlineData("26.11.2601-beta", "26.11.2601-beta.1", -1)]
    [InlineData("26.11.2601-1", "26.11.2601-beta", -1)]
    [InlineData("26.11.2601", "26.11.2601", 0)]
    public void So_phien_ban_theo_SemVer(string a, string b, int expected)
    {
        var x = AppVersion.TryParse(a)!;
        var y = AppVersion.TryParse(b)!;
        Assert.Equal(expected, Math.Sign(x.CompareTo(y)));
        Assert.Equal(-expected, Math.Sign(y.CompareTo(x)));
        Assert.Equal(expected < 0, x < y);
        Assert.Equal(expected == 0, x == y);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("26.11")]
    [InlineData("v26.11.2601")]
    [InlineData("26.11.2601 ")]
    [InlineData("26.11.2601-")]
    [InlineData("26.11.2601-beta..1")]
    [InlineData(null)]
    public void Phien_ban_sai_khuon_khong_parse(string? text) => Assert.Null(AppVersion.TryParse(text));

    // --- Cong (CheckAsync) ---

    [Fact]
    public async Task Co_ban_moi_tren_kenh_cua_minh_thi_khoa()
    {
        var src = new FakeVersionSource(() => Doc(ManifestSigner.Manifest()));
        var d = await Gate(src).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.True(d.IsLocked);
        Assert.Equal("26.11.2701", d.Latest!.Text);
        Assert.Equal("https://github.com/tsudev-org/tsudisk/releases/tag/v26.11.2701", d.Url!.AbsoluteUri);
        Assert.False(d.FromCache);
    }

    [Fact]
    public async Task Kenh_khac_co_ban_moi_khong_anh_huong_chot_chan_theo_kenh()
    {
        var src = new FakeVersionSource(() => Doc(ManifestSigner.Manifest(github: "26.11.2601", scoop: "26.11.2701")));
        var d = await Gate(src).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.Equal(GateStatus.Ok, d.Status);
        var s = await Gate(src).CheckAsync("tsudisk", "winget", "26.11.2601", default);
        Assert.Equal(GateStatus.Ok, s.Status);
    }

    [Fact]
    public async Task Ban_thu_cua_chinh_latest_bi_khoa()
    {
        var src = new FakeVersionSource(() => Doc(ManifestSigner.Manifest(github: "26.11.2701")));
        Assert.True((await Gate(src).CheckAsync("tsudisk", "github", "26.11.2701-beta.1", default)).IsLocked);
        Assert.False((await Gate(src).CheckAsync("tsudisk", "github", "26.11.2701", default)).IsLocked);
    }

    [Fact]
    public async Task Ban_tu_dung_khong_ap_dung_va_khong_goi_mang()
    {
        var src = new FakeVersionSource(() => Doc(ManifestSigner.Manifest()));
        var d = await Gate(src).CheckAsync("tsudisk", "github", "0", default);
        Assert.Equal(GateStatus.NotApplicable, d.Status);
        Assert.Equal(0, src.Calls);
    }

    [Fact]
    public async Task Mang_loi_va_khong_co_dem_thi_khong_chan()
    {
        var d = await Gate(new FakeVersionSource()).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.Equal(GateStatus.Unknown, d.Status);
        Assert.False(d.IsLocked);
    }

    [Fact]
    public async Task Mang_loi_dung_ban_dem_con_han()
    {
        await Gate(new FakeVersionSource(() => Doc(ManifestSigner.Manifest()))).CheckAsync("tsudisk", "github", "26.11.2601", default);
        var d = await Gate(new FakeVersionSource()).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.True(d.IsLocked);
        Assert.True(d.FromCache);
    }

    [Fact]
    public async Task Ngoai_tuyen_va_ban_dem_het_han_thi_khong_chan()
    {
        await Gate(new FakeVersionSource(() => Doc(ManifestSigner.Manifest()))).CheckAsync("tsudisk", "github", "26.11.2601", default);
        var later = ManifestSigner.Now.AddDays(8);
        var d = await Gate(new FakeVersionSource(), later).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.Equal(GateStatus.Unknown, d.Status);
    }

    [Fact]
    public async Task Ban_tai_ve_chu_ky_sai_thi_dung_dem_va_bao_ly_do()
    {
        await Gate(new FakeVersionSource(() => Doc(ManifestSigner.Manifest(github: "26.11.2601")))).CheckAsync("tsudisk", "github", "26.11.2601", default);
        var forged = ManifestSigner.Manifest(github: "99.1.0101");
        var src = new FakeVersionSource(() => new VersionDocument(forged, new ManifestSigner(signer.Kid).Sign(forged)));
        var d = await Gate(src).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.Equal(GateStatus.Ok, d.Status);
        Assert.Equal(ManifestCheck.BadSignature, d.Problem);
        Assert.True(d.FromCache);
    }

    [Fact]
    public async Task Phat_lai_ban_ke_cu_co_chu_ky_that_bi_bo()
    {
        var now = ManifestSigner.Now.ToUnixTimeSeconds();
        var old = ManifestSigner.Manifest(github: "26.11.2601", issued: now - 7200);
        var fresh = ManifestSigner.Manifest(github: "26.11.2701", issued: now - 3600);
        await Gate(new FakeVersionSource(() => Doc(fresh))).CheckAsync("tsudisk", "github", "26.11.2601", default);
        var d = await Gate(new FakeVersionSource(() => Doc(old))).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.Equal(ManifestCheck.Replayed, d.Problem);
        Assert.True(d.IsLocked);   // van theo ban dem moi hon
    }

    [Fact]
    public async Task Tep_dem_bi_sua_tay_thi_bo()
    {
        await Gate(new FakeVersionSource(() => Doc(ManifestSigner.Manifest()))).CheckAsync("tsudisk", "github", "26.11.2601", default);
        var path = Path.Combine(dir, VersionManifestCache.ManifestFile);
        File.WriteAllBytes(path, ManifestSigner.Manifest(github: "26.11.2601"));
        var d = await Gate(new FakeVersionSource()).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.Equal(GateStatus.Unknown, d.Status);
    }

    [Fact]
    public async Task Ban_ke_khong_co_san_pham_thi_ok()
    {
        var m = Encoding.UTF8.GetBytes($"{{\"v\":1,\"issued_at\":{ManifestSigner.Now.ToUnixTimeSeconds() - 10},\"expires_at\":{ManifestSigner.Now.ToUnixTimeSeconds() + 100},\"products\":{{}}}}");
        var d = await Gate(new FakeVersionSource(() => Doc(m))).CheckAsync("tsudisk", "github", "26.11.2601", default);
        Assert.Equal(GateStatus.Ok, d.Status);
    }

    [Fact]
    public async Task Huy_tu_ben_goi_thi_nem_ra()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var src = new FakeVersionSource(() => throw new OperationCanceledException(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Gate(src).CheckAsync("tsudisk", "github", "26.11.2601", cts.Token));
    }

    // --- 426 tu may chu ---

    [Fact]
    public void May_chu_426_thi_khoa()
    {
        var r = ProHttp.UpdateRequiredFrom((HttpStatusCode)426,
            JsonDocument.Parse("{\"error\":\"update_required\",\"latest\":\"26.11.2701\",\"url\":\"https://github.com/tsudev-org/tsudisk/releases/latest\"}").RootElement.Clone());
        var d = VersionGate.FromServer(r!, "26.11.2601");
        Assert.True(d.IsLocked);
        Assert.Equal("26.11.2701", d.Latest!.Text);
        Assert.NotNull(d.Url);
    }

    [Fact]
    public void May_chu_426_than_hong_van_khoa_url_http_bi_bo()
    {
        var r = ProHttp.UpdateRequiredFrom((HttpStatusCode)426,
            JsonDocument.Parse("{\"latest\":\"x\",\"url\":\"http://evil.example\"}").RootElement.Clone());
        var d = VersionGate.FromServer(r!, "26.11.2601");
        Assert.True(d.IsLocked);
        Assert.Null(d.Latest);
        Assert.Null(d.Url);
        Assert.Null(ProHttp.UpdateRequiredFrom(HttpStatusCode.Forbidden, null));
    }

    [Fact]
    public async Task Luot_lay_quyen_loi_426_tra_ve_tin_hieu_khoa()
    {
        using var handler = new StubHandler((HttpStatusCode)426, "{\"error\":\"update_required\",\"latest\":\"26.11.2701\",\"url\":\"https://github.com/tsudev-org/tsudisk/releases/latest\"}");
        using var http = new HttpClient(handler);
        var pro = new ProHttp(http, ProEndpoints.Production, "tsudisk", "tsudisk/26.11.2601/github");
        var r = await pro.GetEntitlementAsync("at", "inst", default);
        Assert.Equal("update_required", r.Error);
        Assert.Equal("26.11.2701", r.UpdateRequired!.Latest);
        Assert.Null(r.Jws);
    }

    // --- HttpVersionSource ---

    [Fact]
    public async Task Nguon_http_chi_nhan_200_va_gioi_han_byte()
    {
        using (var ok = new StubHandler(HttpStatusCode.OK, "{}"))
        using (var http = new HttpClient(ok))
        {
            var doc = await new HttpVersionSource(http).FetchAsync(default);
            Assert.Equal("{}", Encoding.UTF8.GetString(doc.Manifest));
            Assert.Equal(new[] { VersionKeys.ManifestUrl, VersionKeys.SignatureUrl }, ok.Requests);
        }
        using (var redirect = new StubHandler(HttpStatusCode.Found, ""))
        using (var http = new HttpClient(redirect))
            await Assert.ThrowsAsync<HttpRequestException>(() => new HttpVersionSource(http).FetchAsync(default));
        using (var huge = new StubHandler(HttpStatusCode.OK, new string('a', VersionManifestVerifier.MaxManifestBytes + 1)))
        using (var http = new HttpClient(huge))
            await Assert.ThrowsAsync<HttpRequestException>(() => new HttpVersionSource(http).FetchAsync(default));
    }

    [Fact]
    public async Task Nguon_http_khong_gui_header_du_lieu_nguoi_dung()
    {
        using var ok = new StubHandler(HttpStatusCode.OK, "{}");
        using var http = new HttpClient(ok);
        await new HttpVersionSource(http).FetchAsync(default);
        Assert.All(ok.HeaderNames, h => Assert.DoesNotContain(h, new[] { "Authorization", "X-Tsu-Installation", "Cookie" }));
    }

    // --- Nguyen van modal ---

    [Fact]
    public void Nguyen_van_modal_khoa_khop_APP_INTEGRITY_2_2()
    {
        var doc = File.ReadAllText(DocumentationConsistencyTests.RepoPath(".standards", "docs", "APP_INTEGRITY.md"));
        var section = doc[doc.IndexOf("### 2.2.", StringComparison.Ordinal)..doc.IndexOf("### 2.3.", StringComparison.Ordinal)];
        foreach (var t in new[] { UpdateRequiredText.Vi, UpdateRequiredText.En })
        {
            Assert.Contains("| " + t.Title + " |", section, StringComparison.Ordinal);
            Assert.Contains(t.BodyFormat.Replace("{0}", "{mới}", StringComparison.Ordinal).Replace("{1}", "{cũ}", StringComparison.Ordinal), section, StringComparison.Ordinal);
            Assert.Contains("| " + t.Primary + " |", section, StringComparison.Ordinal);
            Assert.Contains("| " + t.Secondary + " |", section, StringComparison.Ordinal);
        }
        Assert.Equal(
            "Đã có phiên bản 26.11.2701. Để bảo vệ an toàn cho bạn và hệ thống, phiên bản 26.11.2601 không còn được sử dụng. Vui lòng cập nhật lên phiên bản mới nhất.",
            UpdateRequiredText.For("vi-VN").Body(AppVersion.TryParse("26.11.2701"), AppVersion.TryParse("26.11.2601")));
        Assert.StartsWith("Version latest is available.", UpdateRequiredText.For("en-US").Body(null, AppVersion.TryParse("26.11.2601")), StringComparison.Ordinal);
    }
}

internal sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];
    public List<string> HeaderNames { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.AbsoluteUri);
        HeaderNames.AddRange(request.Headers.Select(h => h.Key));
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
