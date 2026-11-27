using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TsuDisk.Core.Integrity;

/// <summary>
/// Cong phien ban cho CLI (ADR-0012 muc 7 + 9, quyet dinh chu du an Q1/Q2). Dat o Core de test duoc tren Linux:
/// - Lenh trong <see cref="AllowedWhenLocked"/> KHONG kiem gi (khong mang).
/// - Chua dang nhap ⇒ KHONG tao cong, KHONG goi mang (hang C, Q1).
/// - Moi lenh khac (mac dinh CHAN - ke ca lenh moi them sau nay) bi chan khi cong tra <see cref="GateStatus.UpdateRequired"/>.
/// Cong chay TRUOC khi lenh bat dau, khong bao gio ngat thao tac dia dang chay.
/// </summary>
public static class CommandGate
{
    /// <summary>Q2: lenh van chay khi bi khoa - khong lenh nao trong day ghi dia.</summary>
    public static readonly FrozenSet<string> AllowedWhenLocked =
        new[] { "phien-ban", "tro-giup", "dang-xuat", "xuat-du-lieu" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Ten chuan moi lenh CLI. <c>CliArgs</c> tu choi lenh khong co o day (them lenh moi ⇒ PHAI dang ky, va bang test
    /// trong <c>CommandGateTests</c> tu dong kiem lenh do bi chan khi khoa).
    /// </summary>
    public static readonly FrozenSet<string> KnownCommands = new[]
    {
        "danh-sach", "phan-tich", "ke-hoach", "mo-rong", "ke-hoach-winre", "mo-rong-winre", "ke-hoach-doi-ten", "doi-ten",
        "ke-hoach-tao-phan-vung", "tao-phan-vung", "ke-hoach-thu-nho", "thu-nho", "ke-hoach-xoa", "xoa-phan-vung",
        "nhat-ky", "winre", "suc-khoe", "kiem-tra-gpt", "kiem-tra-mbr2gpt", "sao-luu", "xuat-du-lieu",
        "dang-nhap", "dang-xuat", "tai-khoan", "xoa-tai-khoan", "phien-ban", "tro-giup",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsAllowedWhenLocked(string command) => AllowedWhenLocked.Contains(command);

    /// <summary>
    /// Tra ve quyet dinh cua cong, hoac <c>null</c> khi khong kiem (lenh duoc phep, hoac chua dang nhap). Thu tu kiem
    /// dam bao: lenh duoc phep ⇒ khong doc trang thai dang nhap; chua dang nhap ⇒ <paramref name="createGate"/> KHONG
    /// duoc goi (khong mang, khong tep).
    /// </summary>
    public static async Task<GateDecision?> EvaluateAsync(string command, Func<bool> isSignedIn, Func<VersionGate> createGate,
        string channel, string currentVersion, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(isSignedIn);
        ArgumentNullException.ThrowIfNull(createGate);
        if (IsAllowedWhenLocked(command) || !isSignedIn())
            return null;
        return await createGate().CheckAsync(VersionKeys.Product, channel, currentVersion, cancel).ConfigureAwait(false);
    }

    /// <summary>Chi chan khi cong noi ro <see cref="GateStatus.UpdateRequired"/>; Unknown/NotApplicable/Ok deu chay.</summary>
    public static bool MustBlock(string command, GateDecision? decision)
        => decision is { IsLocked: true } && !IsAllowedWhenLocked(command);

    /// <summary>Lenh cap nhat theo kenh (ADR-0012 muc 9). GitHub ⇒ <c>url</c> cua ban ke (hoac trang Release).</summary>
    public static string UpdateHint(string channel, Uri? url) => channel switch
    {
        "scoop" => "scoop update tsudisk",
        "winget" => "winget upgrade tsudev.TsuDisk",
        _ => (url ?? new Uri("https://github.com/tsudev-org/tsudisk/releases/latest")).AbsoluteUri,
    };

    /// <summary>Cac dong in ra man hinh: nguyen van APP_INTEGRITY 2.2 (vi) + cach cap nhat + loi ra Xuat du lieu.</summary>
    public static IReadOnlyList<string> LockedLines(GateDecision decision, string channel)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var t = UpdateRequiredText.Vi;
        return
        [
            t.Title,
            t.Body(decision.Latest, decision.Current),
            "",
            $"{t.Primary}: {UpdateHint(channel, decision.Url)}",
            $"{t.Secondary}: tsudisk xuat-du-lieu <thư mục>",
        ];
    }

    /// <summary>Doi tuong loi cho <c>--json</c> (ADR-0012 muc 7).</summary>
    public static string LockedJson(GateDecision decision, string channel)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var o = new JsonObject
        {
            ["error"] = "update_required",
            ["current"] = decision.Current?.Text,
            ["latest"] = decision.Latest?.Text,
            ["url"] = decision.Url?.AbsoluteUri,
            ["update"] = UpdateHint(channel, decision.Url),
            ["export"] = "tsudisk xuat-du-lieu <thư mục>",
        };
        return o.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }
}
