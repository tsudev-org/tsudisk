using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TsuDisk.Core.Pro;

namespace TsuDisk.Core.Tests;

/// <summary>
/// INT6 (ADR-0012 muc 13): bang chung DPoP theo RFC 9449 / thumbprint RFC 7638, va luong lay quyen loi co DPoP tren
/// IdP GIA - may chu gia kiem bang chung bang ma viet doc lap (<see cref="DpopCheck"/>).
/// </summary>
public class DpopTests
{
    private static readonly Uri Target = new("https://tsudev.com/api/me/entitlements?a=1#x");

    private static byte[] Hex(string s) => Convert.FromHexString(s);

    private static EcdsaDpopKey NewKey() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

    private static JsonElement Part(string proof, int i)
    {
        using var doc = JsonDocument.Parse(Base64Url.TryDecode(proof.Split('.')[i])!);
        return doc.RootElement.Clone();
    }

    // ---- Vector ----

    [Fact]
    public void Thumbprint_khop_vector_chung_voi_tsudev_khoa_RFC_6979_A_2_5()
        => Assert.Equal("DOvxvJiAdIqVWIkFt5hDtCunXLF0BV4-JGv4f-ALSm0", DpopProof.Thumbprint(
            Hex("60FED4BA255A9D31C961EB74C6356D68C049B8923B61FA6CE669622E60F29FB6"),
            Hex("7903FE1008B8BC99A41AE9E95628BC64F2F1B20C2D7E9F5177A3C294D4462299")));

    [Fact]
    public void Ath_khop_vi_du_RFC_9449_muc_7_1()
    {
        using var key = NewKey();
        var proof = DpopProof.Create(key, "GET", Target, "Kz~8mXK1EalYznwH-LC-1fBAo.4Ljp~zsPE_NeO.gxU", ProTestKit.Now);
        Assert.Equal("fUHyO2r2Z3DZ53EsNrWBb0xWXoaNy59IiKCAqksmQEo", Part(proof, 1).GetProperty("ath").GetString());
    }

    // ---- Hinh dang bang chung ----

    [Fact]
    public void Bang_chung_dung_hinh_dang_chu_ky_kiem_duoc_va_khong_co_khoa_rieng()
    {
        using var key = NewKey();
        var proof = DpopProof.Create(key, "GET", Target, "at", ProTestKit.Now);

        var r = DpopCheck.Verify(proof, "GET", "https://tsudev.com/api/me/entitlements", "at");
        Assert.Equal(DpopProof.Thumbprint(key), r.Jkt);
        Assert.Equal(ProTestKit.Now.ToUnixTimeSeconds(), r.Iat);
        Assert.Null(r.Nonce);
        Assert.False(Part(proof, 0).GetProperty("jwk").TryGetProperty("d", out _));
        Assert.True(proof.Length <= 4096);   // gioi han cua may chu
    }

    [Fact]
    public void Moi_bang_chung_mot_jti_moi_va_khong_token_thi_khong_co_ath()
    {
        using var key = NewKey();
        var a = Part(DpopProof.Create(key, "POST", Target, null, ProTestKit.Now), 1);
        var b = Part(DpopProof.Create(key, "POST", Target, null, ProTestKit.Now), 1);
        Assert.NotEqual(a.GetProperty("jti").GetString(), b.GetProperty("jti").GetString());
        Assert.Equal(16, Base64Url.TryDecode(a.GetProperty("jti").GetString())!.Length);
        Assert.False(a.TryGetProperty("ath", out _));
        Assert.Equal("POST", a.GetProperty("htm").GetString());
    }

    [Fact]
    public void Khoa_khong_phai_P256_bi_tu_choi()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.Throws<ArgumentException>(() => new EcdsaDpopKey(p384));
    }

    // ---- Luong lay quyen loi ----

    [Fact]
    public async Task Dang_nhap_gui_DPoP_va_JWS_mang_cnf_jkt_khop_khoa()
    {
        using var rig = new ProRig();
        var o = await rig.LoginAsync();

        using var key = rig.Dpop.OpenOrCreate()!;
        Assert.Equal(DpopProof.Thumbprint(key), o.Entitlement!.Jkt);
        Assert.Single(rig.Idp.DpopProofs);
        Assert.Equal(1, rig.Dpop.Created);
        // Chi /api/me/entitlements mang DPoP - /oidc/token thi khong (access token chua gan khoa).
        Assert.All(rig.Idp.Requests.Where(r => r.RequestUri!.AbsolutePath != "/api/me/entitlements"),
                   r => Assert.False(r.Headers.Contains(DpopProof.Header)));
    }

    [Fact]
    public async Task Lam_moi_dung_lai_cung_khoa()
    {
        using var rig = new ProRig();
        var login = await rig.LoginAsync();
        var r = await rig.Account.RefreshAsync(CancellationToken.None);
        Assert.Equal(RefreshStatus.Updated, r.Status);
        Assert.Equal(login.Entitlement!.Jkt, r.Entitlement!.Jkt);
        Assert.Equal(1, rig.Dpop.Created);
    }

    [Theory]
    [InlineData("")]                                              // may chu bo cnf
    [InlineData("DOvxvJiAdIqVWIkFt5hDtCunXLF0BV4-JGv4f-ALSm0")]   // jkt cua khoa khac
    public async Task JWS_khong_gan_dung_khoa_bi_bo_va_giu_ban_dem(string cnf)
    {
        using var rig = new ProRig();
        await rig.LoginAsync();
        var good = rig.Store.ReadEntitlementJws();

        rig.Idp.CnfOverride = cnf;
        var r = await rig.Account.RefreshAsync(CancellationToken.None);

        Assert.Equal(RefreshStatus.Rejected, r.Status);
        Assert.Equal(good, rig.Store.ReadEntitlementJws());
    }

    [Fact]
    public async Task Use_dpop_nonce_thu_lai_dung_mot_lan_roi_nho_nonce()
    {
        using var rig = new ProRig();
        rig.Idp.RequiredNonce = "n-1";
        var o = await rig.LoginAsync();
        Assert.NotNull(o.Entitlement);
        Assert.Equal(2, rig.Idp.DpopProofs.Count);

        var r = await rig.Account.RefreshAsync(CancellationToken.None);
        Assert.Equal(RefreshStatus.Updated, r.Status);
        Assert.Equal(3, rig.Idp.DpopProofs.Count);   // nonce da nho: mot luot
    }

    [Fact]
    public async Task Dong_ho_may_lech_thi_ky_lai_theo_gio_may_chu_mot_lan()
    {
        using var rig = new ProRig();
        rig.Idp.ServerClockOffset = TimeSpan.FromMinutes(10);
        var o = await rig.LoginAsync();
        Assert.NotNull(o.Entitlement);
        Assert.Equal(2, rig.Idp.DpopProofs.Count);
    }

    [Fact]
    public async Task Bi_tu_choi_khong_do_dong_ho_thi_khong_thu_lai()
    {
        using var rig = new ProRig();
        rig.Idp.RejectDpop = true;
        var o = await rig.LoginAsync();
        Assert.Equal(LoginStatus.Success, o.Status);
        Assert.Null(o.Entitlement);
        Assert.Single(rig.Idp.DpopProofs);
        Assert.Null(rig.Store.ReadEntitlementJws());
    }

    [Fact]
    public async Task Khong_tao_duoc_khoa_thi_van_lay_quyen_loi_khong_DPoP()
    {
        using var rig = new ProRig();
        rig.Dpop.Unavailable = true;
        var o = await rig.LoginAsync();
        Assert.NotNull(o.Entitlement);
        Assert.Null(o.Entitlement!.Jkt);
        Assert.Empty(rig.Idp.DpopProofs);
    }

    [Fact]
    public async Task Dang_xuat_xoa_khoa_va_dang_nhap_lai_sinh_khoa_moi()
    {
        using var rig = new ProRig();
        var first = await rig.LoginAsync();
        await rig.Account.LogoutAsync(CancellationToken.None);
        Assert.Equal(1, rig.Dpop.Deleted);

        var second = await rig.LoginAsync();
        Assert.NotEqual(first.Entitlement!.Jkt, second.Entitlement!.Jkt);
    }

    [Fact]
    public async Task Doi_tai_khoan_tren_cung_may_thi_doi_khoa()
    {
        using var rig = new ProRig();
        var first = await rig.LoginAsync();
        rig.Idp.Sub = "u-2";
        var second = await rig.LoginAsync();
        Assert.NotEqual(first.Entitlement!.Jkt, second.Entitlement!.Jkt);
    }

    [Fact]
    public async Task Chua_dang_nhap_khong_tao_khoa()
    {
        using var net = new NoNetworkHandler();
        using var rig = new ProRig(net);
        await rig.Account.RefreshAsync(CancellationToken.None);
        await rig.Account.ForRunAsync(interactive: true, CancellationToken.None);
        Assert.Equal(0, rig.Dpop.Created);
        Assert.Equal(0, net.Calls);
    }
}
