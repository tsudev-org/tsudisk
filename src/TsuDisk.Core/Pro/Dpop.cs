using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TsuDisk.Core.Pro;

/// <summary>
/// Khoa DPoP cua thiet bi (RFC 9449, PRODUCT_POLICY 7.4, ADR-0012 muc 13). Chi lo ra phan CONG KHAI va phep ky:
/// khoa rieng khong bao gio roi noi cat giu (TPM / KSP cua CNG tren Windows).
/// </summary>
public interface IDpopKey : IDisposable
{
    /// <summary>Toa do X, Y cua khoa cong khai P-256 (32 byte moi toa do).</summary>
    (byte[] X, byte[] Y) PublicPoint { get; }

    /// <summary>Ky ES256: SHA-256 + ECDSA P-256, chu ky R||S 64 byte (IEEE P1363) nhu JWS doi hoi.</summary>
    byte[] SignP1363(byte[] data);
}

/// <summary>
/// Noi cat khoa DPoP. <see cref="OpenOrCreate"/> CHI duoc goi khi da dang nhap (bat bien hang C: chua dang nhap thi
/// khong tao gi tren may). Tra <c>null</c> khi nen tang khong tao/mo duoc khoa - luc do app goi khong kem DPoP (7.4 la
/// SHOULD), khong lam hong Pro.
/// </summary>
public interface IDpopKeyStore
{
    IDpopKey? OpenOrCreate();

    /// <summary>"Dang xuat va xoa du lieu cuc bo": xoa khoa (co gang, khong nem).</summary>
    void Delete();
}

/// <summary>Khoa DPoP boc mot <see cref="ECDsa"/> P-256 (ECDsaCng tren Windows; khoa tam trong test).</summary>
public sealed class EcdsaDpopKey : IDpopKey
{
    private readonly ECDsa _key;

    public EcdsaDpopKey(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        // Chi xuat phan cong khai (khoa CNG ExportPolicy=None van cho xuat phan nay).
        var p = key.ExportParameters(false);
        if (key.KeySize != 256 || p.Q.X is not { Length: 32 } || p.Q.Y is not { Length: 32 })
            throw new ArgumentException("DPoP chi dung P-256 (ES256).", nameof(key));
        _key = key;
        PublicPoint = (p.Q.X!, p.Q.Y!);
    }

    public (byte[] X, byte[] Y) PublicPoint { get; }

    public byte[] SignP1363(byte[] data)
        => _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public void Dispose() => _key.Dispose();
}

/// <summary>
/// Dung bang chung DPoP (RFC 9449 muc 4.2) va thumbprint khoa (RFC 7638) - tu viet bang BCL nhu
/// <see cref="EntitlementVerifier"/>, khong them thu vien JWT. Hop dong phia may chu: tsudev ADR-0004 muc 6
/// (<c>typ</c> dpop+jwt, ES256, <c>jwk</c> P-256 cong khai, <c>htm</c>, <c>htu</c>, <c>iat</c> ±60 giay, <c>jti</c> mot lan,
/// <c>ath</c>).
/// </summary>
public static class DpopProof
{
    public const string Type = "dpop+jwt";
    public const string Header = "DPoP";
    public const string NonceHeader = "DPoP-Nonce";

    /// <summary>
    /// Thumbprint RFC 7638 cua khoa EC: SHA-256 cua JSON co dung bon thanh vien bat buoc, xep theo thu tu tu dien,
    /// khong khoang trang - <c>{"crv":"P-256","kty":"EC","x":"..","y":".."}</c>; ket qua base64url.
    /// </summary>
    public static string Thumbprint(byte[] x, byte[] y)
    {
        var json = $$"""{"crv":"P-256","kty":"EC","x":"{{Base64Url.Encode(x)}}","y":"{{Base64Url.Encode(y)}}"}""";
        return Base64Url.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public static string Thumbprint(IDpopKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Thumbprint(key.PublicPoint.X, key.PublicPoint.Y);
    }

    /// <summary><c>htu</c>: URI dich KHONG co query va fragment (RFC 9449 muc 4.2).</summary>
    public static string Htu(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.GetLeftPart(UriPartial.Path);
    }

    /// <summary>
    /// Tao bang chung compact JWS. <paramref name="accessToken"/> khac null ⇒ them <c>ath</c> = base64url(SHA-256(token
    /// ASCII)). <paramref name="nonce"/> la gia tri may chu cap qua <c>DPoP-Nonce</c> (neu co).
    /// </summary>
    public static string Create(IDpopKey key, string htm, Uri htu, string? accessToken, DateTimeOffset now,
                                string? nonce = null, string? jti = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        var (x, y) = key.PublicPoint;
        // JsonObject thay vi serialize bang phan chieu: khong can ma sinh, qua duoc bo phan tich trim.
        var header = new JsonObject
        {
            ["typ"] = Type,
            ["alg"] = ProContract.Algorithm,
            ["jwk"] = new JsonObject
            {
                ["kty"] = "EC", ["crv"] = "P-256", ["x"] = Base64Url.Encode(x), ["y"] = Base64Url.Encode(y),
            },
        };
        var payload = new JsonObject
        {
            ["htm"] = htm,
            ["htu"] = Htu(htu),
            ["iat"] = now.ToUnixTimeSeconds(),
            // 128 bit ngau nhien tu CSPRNG: may chu chi nhan moi jti MOT lan trong cua so ±60 giay.
            ["jti"] = jti ?? Base64Url.Encode(RandomNumberGenerator.GetBytes(16)),
        };
        if (accessToken is not null)
            payload["ath"] = Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken)));
        if (nonce is not null) payload["nonce"] = nonce;

        var input = Base64Url.Encode(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." +
                    Base64Url.Encode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var sig = key.SignP1363(Encoding.ASCII.GetBytes(input));
        if (sig.Length != 64) throw new CryptographicException("Chu ky ES256 phai la R||S 64 byte.");
        return input + "." + Base64Url.Encode(sig);
    }
}
