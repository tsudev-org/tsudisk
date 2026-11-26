using System.Text;
using System.Text.Json;
using TsuDisk.Core.Pro;
using Ed25519 = Org.BouncyCastle.Math.EC.Rfc8032.Ed25519;

namespace TsuDisk.Core.Integrity;

public enum JwsCheck
{
    Ok,
    Malformed,
    UnsupportedAlgorithm,
    UnknownKey,
    BadSignature,
}

/// <summary>
/// JWS compact tach noi dung (RFC 7515 phu luc F): <c>&lt;b64url(header)&gt;..&lt;b64url(chu ky)&gt;</c>, header
/// <c>{"alg":"EdDSA","kid":...}</c>. Chu ky Ed25519 tren <c>header-goc + "." + b64url(BYTE TAI VE)</c> - khong parse
/// roi serialize lai. Chi nhan <c>EdDSA</c>; header co tham so la (<c>crit</c>, <c>b64</c>, <c>jwk</c>...) hoac khoa
/// trung lap ⇒ tu choi (khong de ke tan cong chon khoa/thuat toan).
/// </summary>
public static class DetachedJws
{
    public const int MaxLength = 1024;
    private const int SignatureSize = 64;   // Ed25519 (RFC 8032)
    private const int PublicKeySize = 32;
    private static readonly HashSet<string> AllowedHeader = new(StringComparer.Ordinal) { "alg", "kid", "typ" };

    public static JwsCheck Verify(ReadOnlySpan<byte> payload, string? compact, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (string.IsNullOrEmpty(compact)) return JwsCheck.Malformed;
        compact = compact.TrimEnd(' ', '\t', '\r', '\n');
        if (compact.Length is 0 or > MaxLength) return JwsCheck.Malformed;

        var parts = compact.Split('.');
        if (parts.Length != 3 || parts[1].Length != 0 || parts[0].Length == 0 || parts[2].Length == 0)
            return JwsCheck.Malformed;

        if (!TryReadHeader(parts[0], out var alg, out var kid)) return JwsCheck.Malformed;
        if (!string.Equals(alg, "EdDSA", StringComparison.Ordinal)) return JwsCheck.UnsupportedAlgorithm;
        if (kid is null || !keys.TryGetValue(kid, out var key) || key.Length != PublicKeySize)
            return JwsCheck.UnknownKey;

        if (Base64Url.TryDecode(parts[2]) is not { Length: SignatureSize } sig) return JwsCheck.Malformed;

        var input = Encoding.ASCII.GetBytes(parts[0] + "." + Base64Url.Encode(payload));
        bool ok;
        try
        {
            ok = Ed25519.Verify(sig, 0, key.ToArray(), 0, input, 0, input.Length);
        }
        catch (ArgumentException)
        {
            ok = false;
        }
        return ok ? JwsCheck.Ok : JwsCheck.BadSignature;
    }

    private static bool TryReadHeader(string encoded, out string? alg, out string? kid)
    {
        alg = null;
        kid = null;
        if (Base64Url.TryDecode(encoded) is not { Length: > 0 and <= 512 } bytes) return false;
        try
        {
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (!seen.Add(p.Name) || !AllowedHeader.Contains(p.Name) || p.Value.ValueKind != JsonValueKind.String)
                    return false;
                if (p.Name == "alg") alg = p.Value.GetString();
                else if (p.Name == "kid") kid = p.Value.GetString();
            }
            return alg is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
