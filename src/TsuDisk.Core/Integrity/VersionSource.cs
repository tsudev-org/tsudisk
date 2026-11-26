using System.Net;
using System.Text;

namespace TsuDisk.Core.Integrity;

/// <summary>Byte ban ke + chuoi chu ky tach, CHUA kiem.</summary>
public sealed record VersionDocument(byte[] Manifest, string Signature);

/// <summary>Nguon ban ke. Loi mang (<see cref="HttpRequestException"/>, het gio, <see cref="IOException"/>) NEM ra ngoai.</summary>
public interface IVersionSource
{
    Task<VersionDocument> FetchAsync(CancellationToken cancel);
}

/// <summary>
/// GET ban ke tu host co dinh <c>tsudev.com</c> qua https (<see cref="VersionKeys"/>). Chi nhan 200 (3xx ⇒ loi - ben goi
/// tao HttpClient KHONG theo chuyen huong, giong <see cref="Pro.ProHttp"/>), gioi han byte. KHONG gui gi ngoai
/// User-Agent cua HttpClient (APP_INTEGRITY I5).
/// </summary>
public sealed class HttpVersionSource(HttpClient http) : IVersionSource
{
    public const int MaxSignatureBytes = DetachedJws.MaxLength;

    public async Task<VersionDocument> FetchAsync(CancellationToken cancel)
    {
        var manifest = await GetAsync(new Uri(VersionKeys.ManifestUrl), VersionManifestVerifier.MaxManifestBytes, cancel).ConfigureAwait(false);
        var sig = await GetAsync(new Uri(VersionKeys.SignatureUrl), MaxSignatureBytes, cancel).ConfigureAwait(false);
        return new VersionDocument(manifest, Encoding.ASCII.GetString(sig));
    }

    private async Task<byte[]> GetAsync(Uri uri, int max, CancellationToken cancel)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        if (res.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Ban ke tra ma " + ((int)res.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture), null, res.StatusCode);
        if (res.Content.Headers.ContentLength is > 0 and var len && len > max)
            throw new HttpRequestException("Ban ke qua lon");
        var stream = await res.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[max + 1];
            var total = 0;
            int n;
            while (total <= max && (n = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancel).ConfigureAwait(false)) > 0)
                total += n;
            if (total > max) throw new HttpRequestException("Ban ke qua lon");
            return buffer.AsSpan(0, total).ToArray();
        }
    }
}

/// <summary>
/// Dem ban ke DA QUA kiem vao <c>%LOCALAPPDATA%\tsudev\tsudisk\versions\</c> (ADR-0012 muc 5). Doc lai luon kiem lai
/// day du (tep dem bi sua tay ⇒ chu ky sai ⇒ bo). Moi loi doc ⇒ coi nhu khong co dem.
/// </summary>
public sealed class VersionManifestCache(string directory)
{
    public const string ManifestFile = "versions.json";
    public const string SignatureFile = "versions.json.sig";

    public VersionDocument? Load()
    {
        try
        {
            var m = Path.Combine(directory, ManifestFile);
            var s = Path.Combine(directory, SignatureFile);
            if (!File.Exists(m) || !File.Exists(s)) return null;
            if (new FileInfo(m).Length > VersionManifestVerifier.MaxManifestBytes || new FileInfo(s).Length > DetachedJws.MaxLength)
                return null;
            return new VersionDocument(File.ReadAllBytes(m), File.ReadAllText(s, Encoding.ASCII));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>Ghi nguyen tu tung tep. Hong giua chung (lech cap) ⇒ lan doc sau chu ky sai ⇒ bo - an toan.</summary>
    public void Save(VersionDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        Directory.CreateDirectory(directory);
        WriteAtomic(ManifestFile, doc.Manifest);
        WriteAtomic(SignatureFile, Encoding.ASCII.GetBytes(doc.Signature));
    }

    private void WriteAtomic(string name, byte[] data)
    {
        var target = Path.Combine(directory, name);
        var tmp = target + ".tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, target, overwrite: true);
    }
}
