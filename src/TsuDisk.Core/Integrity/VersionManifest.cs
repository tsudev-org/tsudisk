using System.Text.Json;

namespace TsuDisk.Core.Integrity;

/// <summary>Muc <c>products.&lt;sp&gt;.channels.&lt;kenh&gt;</c>: <see cref="Url"/> co the null (vd. kenh <c>web</c>).</summary>
public sealed record ChannelRelease(AppVersion Latest, Uri? Url);

/// <summary>Ban ke da QUA kiem chu ky + noi dung (chi tao duoc qua <see cref="VersionManifestVerifier"/>).</summary>
public sealed class VersionManifest
{
    private readonly Dictionary<string, Dictionary<string, ChannelRelease>> products;

    internal VersionManifest(long issuedAt, long expiresAt, Dictionary<string, Dictionary<string, ChannelRelease>> products)
    {
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        this.products = products;
    }

    public long IssuedAt { get; }
    public long ExpiresAt { get; }

    /// <summary>Chot chan THEO KENH (APP_INTEGRITY 2.1): chi doc dung (san pham, kenh) cua ban dang chay.</summary>
    public ChannelRelease? Find(string product, string channel)
        => products.TryGetValue(product, out var ch) && ch.TryGetValue(channel, out var r) ? r : null;
}

public enum ManifestCheck
{
    Ok,
    TooLarge,
    SignatureMalformed,
    UnsupportedAlgorithm,
    UnknownKey,
    BadSignature,
    InvalidContent,
    UnsupportedFormat,
    LifetimeTooLong,
    Expired,
    Replayed,
}

/// <summary>
/// <see cref="Manifest"/> chi khac null khi <see cref="Status"/> = Ok. <see cref="IssuedAt"/> co gia tri khi chu ky va
/// noi dung dung (ke ca da het han) - dung lam moc chong phat lai.
/// </summary>
public sealed record ManifestResult(ManifestCheck Status, VersionManifest? Manifest, long? IssuedAt);

/// <summary>
/// Kiem ban ke theo ADR-0012 muc 4, DUNG THU TU: kich thuoc -> chu ky (tren byte tai ve) -> chi SAU DO moi parse JSON
/// -> <c>v</c> -> thoi han (<c>expires_at - issued_at</c> &lt;= 7 ngay, chua het han) -> chong phat lai
/// (<c>issued_at</c> khong cu hon ban da dem).
/// </summary>
public static class VersionManifestVerifier
{
    public const int MaxManifestBytes = 256 * 1024;
    public const long MaxLifetimeSeconds = 7 * 24 * 3600;

    public static ManifestResult Verify(
        ReadOnlySpan<byte> manifest, string? signature, DateTimeOffset now, long? minIssuedAt,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> keys)
    {
        if (manifest.Length is 0 or > MaxManifestBytes) return Fail(ManifestCheck.TooLarge);

        switch (DetachedJws.Verify(manifest, signature, keys))
        {
            case JwsCheck.Ok: break;
            case JwsCheck.UnsupportedAlgorithm: return Fail(ManifestCheck.UnsupportedAlgorithm);
            case JwsCheck.UnknownKey: return Fail(ManifestCheck.UnknownKey);
            case JwsCheck.BadSignature: return Fail(ManifestCheck.BadSignature);
            default: return Fail(ManifestCheck.SignatureMalformed);
        }

        long issued, expires;
        Dictionary<string, Dictionary<string, ChannelRelease>> products;
        try
        {
            using var doc = JsonDocument.Parse(manifest.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Fail(ManifestCheck.InvalidContent);
            if (!root.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var ver))
                return Fail(ManifestCheck.InvalidContent);
            if (ver != 1) return Fail(ManifestCheck.UnsupportedFormat);
            if (!TryInt64(root, "issued_at", out issued) || !TryInt64(root, "expires_at", out expires) || expires <= issued)
                return Fail(ManifestCheck.InvalidContent);
            if (!root.TryGetProperty("products", out var prods) || prods.ValueKind != JsonValueKind.Object)
                return Fail(ManifestCheck.InvalidContent);
            products = ReadProducts(prods);
        }
        catch (JsonException)
        {
            return Fail(ManifestCheck.InvalidContent);
        }

        if (expires - issued > MaxLifetimeSeconds) return new ManifestResult(ManifestCheck.LifetimeTooLong, null, null);
        if (now.ToUnixTimeSeconds() >= expires) return new ManifestResult(ManifestCheck.Expired, null, issued);
        if (minIssuedAt is { } min && issued < min) return new ManifestResult(ManifestCheck.Replayed, null, issued);
        return new ManifestResult(ManifestCheck.Ok, new VersionManifest(issued, expires, products), issued);
    }

    private static ManifestResult Fail(ManifestCheck status) => new(status, null, null);

    private static bool TryInt64(JsonElement o, string name, out long value)
    {
        value = 0;
        return o.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out value) && value > 0;
    }

    /// <summary>
    /// Truong la bi bo qua (tuong thich ve sau). Muc kenh hong (latest sai khuon, url khong phai https) bi BO QUA - phia
    /// app khong khoa nguoi dung vi du lieu khong doc duoc; bao ve that nam o may chu (426, I1).
    /// </summary>
    private static Dictionary<string, Dictionary<string, ChannelRelease>> ReadProducts(JsonElement prods)
    {
        var result = new Dictionary<string, Dictionary<string, ChannelRelease>>(StringComparer.Ordinal);
        foreach (var p in prods.EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.Object
                || !p.Value.TryGetProperty("channels", out var chans) || chans.ValueKind != JsonValueKind.Object)
                continue;
            var map = new Dictionary<string, ChannelRelease>(StringComparer.Ordinal);
            foreach (var c in chans.EnumerateObject())
            {
                if (c.Value.ValueKind != JsonValueKind.Object) continue;
                if (!c.Value.TryGetProperty("latest", out var l) || l.ValueKind != JsonValueKind.String) continue;
                if (AppVersion.TryParse(l.GetString()) is not { } latest) continue;
                Uri? url = null;
                if (c.Value.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
                    && Uri.TryCreate(u.GetString(), UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps)
                    url = parsed;
                map[c.Name] = new ChannelRelease(latest, url);
            }
            result[p.Name] = map;
        }
        return result;
    }
}
