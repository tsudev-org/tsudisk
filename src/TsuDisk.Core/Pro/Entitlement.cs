namespace TsuDisk.Core.Pro;

/// <summary>
/// Quyen loi TsuDEV Pro da KIEM CHU KY (PRODUCT_POLICY muc 7.4). Chi
/// <see cref="EntitlementVerifier"/> tao ra doi tuong nay tu JWS that.
/// </summary>
/// <param name="Jkt"><c>cnf.jkt</c> (RFC 9449 muc 6.1): thumbprint khoa DPoP ma JWS duoc gan; null khi luot lay khong kem DPoP.</param>
public sealed record Entitlement(
    string Subject,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    long ProUntil,
    string? ProSource,
    bool Donor,
    string Badge,
    int Devices30d,
    string? Jkt = null)
{
    /// <summary><c>pro_until</c> = 0: chua tung co Pro. Pro con han khi <c>pro_until</c> &gt; gio hien tai.</summary>
    public bool IsProAt(DateTimeOffset now) => ProUntil > now.ToUnixTimeSeconds();
}
