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
    /// Client OIDC native cua TsuDisk tren tsudev.com - CHUA DANG KY (ADR-0007 muc 3, phieu gui tsudev
    /// 20261122-PRO-CLIENT). Cung la <c>aud</c> cua JWS quyen loi va tham so <c>app</c> cua trang Pro.
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

    /// <summary>Thu muc du lieu cuc bo cua tai khoan (ADR-0007): <c>%LOCALAPPDATA%\tsudev\tsudisk\</c>.</summary>
    public static string DefaultStateDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tsudev", "tsudisk");

    /// <summary>JWS quyen loi da dem (luong dang nhap - khi co - ghi vao day). Khong co tep ⇒ mien phi, khong loi.</summary>
    public const string CachedEntitlementFile = "entitlement.jws";

    /// <summary>JWKS cua tsudev.com dem cung luc voi JWS.</summary>
    public const string CachedJwksFile = "jwks.json";

    /// <summary>Trang Pro cho ban ngoai cua hang (muc 7.3 "Kenh").</summary>
    public static string ProUrl(string clientId) => "https://tsudev.com/pro?app=" + Uri.EscapeDataString(clientId);
}
