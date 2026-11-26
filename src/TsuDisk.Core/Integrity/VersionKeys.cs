using TsuDisk.Core.Pro;

namespace TsuDisk.Core.Integrity;

/// <summary>
/// Khoa cong khai Ed25519 cua ban ke phien ban toan he, GHIM luc dung (APP_INTEGRITY 2.1, ADR-0012 muc 3). App KHONG
/// tai JWKS luc chay va KHONG co bien moi truong/tham so doi khoa hay URL (do la cua bo qua cong).
/// Xoay khoa: phat hanh ban ghim CA khoa cu va khoa moi truoc, roi tsudev.com moi doi.
/// Doi chieu: <c>https://tsudev.com/.well-known/tsudev/versions-jwks.json</c> (test <c>VersionKeys_khop_JWKS_production</c>).
/// </summary>
public static class VersionKeys
{
    public const string ManifestUrl = "https://tsudev.com/.well-known/tsudev/versions.json";
    public const string SignatureUrl = "https://tsudev.com/.well-known/tsudev/versions.json.sig";

    /// <summary>Ma san pham trong ban ke = client OIDC (<see cref="ProContract.ClientId"/>).</summary>
    public const string Product = ProContract.ClientId;

    /// <summary><c>kid</c> -> khoa cong khai Ed25519 32 byte (gia tri <c>x</c> cua JWK, base64url).</summary>
    public static IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Production { get; } =
        new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
        {
            ["tsu-versions-2026"] = Key("ld1fFoBdWmYU_iTKEq8_0x-0KBWOo2U2KJIVLL8TVBI"),
        };

    private static ReadOnlyMemory<byte> Key(string x)
        => Base64Url.TryDecode(x) is { Length: 32 } k ? k : throw new InvalidOperationException("Khoa ghim sai do dai");
}
