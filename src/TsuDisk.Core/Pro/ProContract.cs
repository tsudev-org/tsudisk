namespace TsuDisk.Core.Pro;

/// <summary>
/// Dinh danh chung cua TsuDEV Pro - chep NGUYEN VAN tu .standards/docs/PRODUCT_POLICY.md
/// muc 7.4 va AUTH_AND_ACCOUNT.md muc 14.3. MUST NOT tu dat khac (ADR-0007).
/// </summary>
public static class ProContract
{
    public const string Issuer = "https://tsudev.com/oidc";
    public const string JwksUrl = "https://tsudev.com/oidc/jwks";
    public const string EntitlementsUrl = "https://tsudev.com/api/me/entitlements";

    /// <summary>
    /// Client OIDC native cua TsuDisk tren tsudev.com - da dang ky 22/11/2026 (phieu tsudev
    /// 20261122-DK1, ADR-0007). Cung la <c>aud</c> cua JWS quyen loi va tham so <c>app</c> cua trang Pro.
    /// </summary>
    public const string ClientId = "tsudisk";

    /// <summary>
    /// <c>offline_access</c> de nhan refresh token (luu DPAPI khi co dang nhap, ADR-0007) - thieu no thi moi lan
    /// lay quyen loi phai mo trinh duyet. oidc-provider BO <c>offline_access</c> neu yeu cau khong kem
    /// <c>prompt=consent</c> (xem AuthorizationRequest cua tsuswico).
    /// </summary>
    public const string Scope = "openid offline_access tsudev.entitlements";

    /// <summary>Duong callback da dang ky (<c>http://127.0.0.1/callback</c>, <c>http://[::1]/callback</c>, cong bat ky).</summary>
    public const string CallbackPath = "/callback";

    /// <summary>Header <c>typ</c> bat buoc cua JWS quyen loi.</summary>
    public const string EntitlementType = "tsu-ent+jwt";

    /// <summary>Thuat toan DUY NHAT duoc nhan.</summary>
    public const string Algorithm = "ES256";

    /// <summary><c>exp</c> &lt;= <c>iat</c> + 7 ngay - JWS song lau hon la sai hop dong, tu choi.</summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(7);

    public const string InstallationHeader = "X-Tsu-Installation";

    /// <summary>APP_INTEGRITY 2.1: moi luot goi API tsudev.com gui <c>X-Tsu-App: &lt;ma-san-pham&gt;/&lt;phien ban&gt;/&lt;kenh&gt;</c>.</summary>
    public const string AppHeader = "X-Tsu-App";

    // Cung khuon voi cong cua tsudev.com (services/auth-service/src/appVersionGate.ts) - sai khuon thi may chu tra 400.
    private static readonly System.Text.RegularExpressions.Regex AppHeaderShape = new(
        @"^[a-z0-9][a-z0-9-]{1,39}/\d{1,4}\.\d{1,4}\.\d{1,6}(-[0-9A-Za-z.-]{1,32})?/[a-z][a-z0-9-]{1,23}$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Gia tri <see cref="AppHeader"/>; phien ban/kenh sai khuon (vd. ban tu build "0") ⇒ null, KHONG gui.</summary>
    public static string? AppHeaderValue(string version, string channel)
    {
        var v = $"{ClientId}/{version}/{channel}";
        return v.Length <= 120 && AppHeaderShape.IsMatch(v) ? v : null;
    }

    /// <summary>
    /// Kenh phat hanh suy tu noi dat exe (chot chan THEO KENH, APP_INTEGRITY 2.1): Scoop cai vao <c>...\scoop\apps\</c>,
    /// winget (goi portable) vao <c>...\WinGet\Packages\</c> hoac lien ket <c>...\WinGet\Links\</c>; con lai la tai
    /// tu GitHub Releases. Doan sai kenh chi lam may chu so voi <c>latest</c> cua kenh khac - khong lo du lieu.
    /// </summary>
    public static string DetectChannel(string? exePath)
    {
        var p = (exePath ?? "").Replace('/', '\\');
        if (p.Contains(@"\scoop\apps\", StringComparison.OrdinalIgnoreCase)) return "scoop";
        if (p.Contains(@"\WinGet\Packages\", StringComparison.OrdinalIgnoreCase)
            || p.Contains(@"\WinGet\Links\", StringComparison.OrdinalIgnoreCase)) return "winget";
        return "github";
    }

    /// <summary>Thu muc du lieu cuc bo cua tai khoan (ADR-0007): <c>%LOCALAPPDATA%\tsudev\tsudisk\</c>.</summary>
    public static string DefaultStateDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tsudev", "tsudisk");

    /// <summary>
    /// AUTH_AND_ACCOUNT v5.0.0 muc 2.1: app CHI co nut "Mo trang tai khoan" va "Xoa tai khoan" dan toi CONG tsudev.com -
    /// dang nhap/dang ky/QR/mat khau/xoa (xac thuc lai) deu nam o cong, app khong co o nhap nao. Do 27/11/2026: trang tai
    /// khoan cua cong la <c>/settings/security</c> (200), muc xoa co neo <c>#xoa-tai-khoan</c>; bo duong <c>/tai-khoan</c>
    /// cua muc 13.1 chua co (404). Dang nhap KHONG ghi cung trang nao: di qua <c>/authorize</c> cua issuer
    /// (<see cref="ProEndpoints"/>), cong tu chuyen toi trang dang nhap hien hanh.
    /// </summary>
    public const string AccountPageUrl = "https://tsudev.com/settings/security";

    /// <summary>Trang xoa tai khoan cua cong (muc 2.1 "Xoa tai khoan": mo dung trang xoa, xac thuc lai nam o cong).</summary>
    public const string DeleteAccountUrl = "https://tsudev.com/settings/security#xoa-tai-khoan";

    /// <summary>Trang Pro cho ban ngoai cua hang (muc 7.3 "Kenh").</summary>
    public static string ProUrl(string clientId) => "https://tsudev.com/pro?app=" + Uri.EscapeDataString(clientId);
}
