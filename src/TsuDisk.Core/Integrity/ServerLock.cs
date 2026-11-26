using System.Text.Json;
using System.Text.Json.Nodes;
using TsuDisk.Core.Pro;

namespace TsuDisk.Core.Integrity;

/// <summary>
/// Dau "may chu da tra <c>426</c>" (ADR-0012 muc 8). <see cref="ProAccount"/> nhan <c>426</c> trong luot lam moi quyen loi
/// (chi CLI tuong tac goi mang Pro) ⇒ ghi dau nay canh dem ban ke; <see cref="VersionGate"/> cua CLI VA GUI doc no truoc
/// khi tai ban ke ⇒ ca hai cung khoa.
///
/// - Dau gan voi DUNG phien ban dang chay: nang cap xong la dau tu het tac dung (khong can xoa).
/// - Het han sau <see cref="Lifetime"/>: may chu tra 426 nham thi khong khoa mai. Ban that su bi khoa thi lan lam moi
///   quyen loi ke tiep (JWS dem khong duoc lam moi nen moi lenh tuong tac deu thu lai) nhan 426 lan nua va ghi lai dau.
/// - Tep hong/sua tay/thieu ⇒ coi nhu khong co dau (cong KHONG BAO GIO lam hong lenh vi tep nay). Xoa tay de go khoa chi
///   go duoc o may khach - may chu van tu choi (I1, Kerckhoffs).
/// </summary>
public sealed class ServerLockStore(string directory)
{
    public const string FileName = "server-lock.json";

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private const int MaxBytes = 4096;

    public void Save(UpdateRequiredResponse response, string currentVersion, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(response);
        var o = new JsonObject
        {
            ["current"] = currentVersion,
            ["latest"] = response.Latest,
            ["url"] = response.Url?.AbsoluteUri,
            ["at"] = now.ToUnixTimeSeconds(),
        };
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, FileName);
        var tmp = target + ".tmp";
        File.WriteAllText(tmp, o.ToJsonString());
        File.Move(tmp, target, overwrite: true);
    }

    /// <summary>Ghi dau, nuot loi tep (thu muc khong ghi duoc ⇒ khong khoa lau dai, nhung lenh nay van bi khoa).</summary>
    public void TrySave(UpdateRequiredResponse response, string currentVersion, DateTimeOffset now)
    {
        try { Save(response, currentVersion, now); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <returns>Quyet dinh khoa neu co dau con han cho DUNG <paramref name="current"/>; nguoc lai <c>null</c>.</returns>
    public GateDecision? Load(AppVersion current, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);
        try
        {
            var path = Path.Combine(directory, FileName);
            if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes) return null;
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject o) return null;
            if (o["current"]?.GetValue<string>() != current.Text) return null;
            if (o["at"]?.GetValue<long>() is not { } at) return null;
            var age = now - DateTimeOffset.FromUnixTimeSeconds(at);
            if (age < TimeSpan.Zero || age >= Lifetime) return null;
            Uri? url = o["url"]?.GetValue<string>() is { } u && Uri.TryCreate(u, UriKind.Absolute, out var parsed)
                && parsed.Scheme == Uri.UriSchemeHttps ? parsed : null;
            var latest = o["latest"]?.GetValue<string>() is { } l ? AppVersion.TryParse(l) : null;
            return new GateDecision(GateStatus.UpdateRequired, current, latest, url, null, FromCache: true);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }   // GetValue sai kieu
        catch (FormatException) { return null; }
        catch (ArgumentOutOfRangeException) { return null; }  // "at" ngoai khoang
    }
}
