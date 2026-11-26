using System.Globalization;

namespace TsuDisk.Core.Pro;

public enum ProRenewalKind { None, Reminder, Expired }

/// <summary>Vi sao hien / khong hien - de test noi ro ca nao dang xay ra.</summary>
public enum ProRenewalReason
{
    Reminder,
    Expired,
    NonInteractive,
    NoEntitlement,
    ResumedRecently,
    NotNearExpiry,
    NeverHadProHere,
    AlreadyNotified,
}

public sealed record ProRenewalDecision(ProRenewalKind Kind, ProRenewalReason Reason, int DaysLeft, long ProUntil)
{
    public bool Show => Kind != ProRenewalKind.None;
}

/// <summary>
/// Nhac gia han + bao het ky TsuDEV Pro - PRODUCT_POLICY muc 7.6 (noi dung v2.0.0, bo quy uoc v7.0.0+: bo Q3 dong bo),
/// phieu 20261120-PRO2, 20261125-SYN1. La ngoai le co chu dich cua muc 3.8 (chi nguoi da mua Pro).
///
/// Nguon thoi han: <c>pro_until</c> cua JWS quyen loi CON HAN; khong co ⇒ khong hien gi.
/// <list type="bullet">
/// <item>Nhac: 0 &lt; <c>pro_until</c> - bay gio &lt;= 7 ngay, MOI LAN MO APP. Voi CLI, moi lan chay <c>tsudisk.exe</c>
/// tuong tac la mot lan mo (tien trinh khong o nen) ⇒ <c>resumedAfter</c> = null. Tham so nay giu cho dung
/// dieu kien "quay lai sau &gt;= 30 phut o nen" cua chuan neu sau nay co giao dien chay nen.</item>
/// <item>Het ky: <c>pro_until</c> &lt;= bay gio, MAY NAY tung thay ky do con han (<see cref="SeenProUntilKey"/>),
/// bao MOT LAN cho moi <c>pro_until</c> (<see cref="NotifiedUntilKey"/>).</item>
/// </list>
/// <c>--json</c>, <c>--dong-y</c>/<c>--xac-nhan</c> (chay tu dong) hay dau vao/ra bi chuyen huong MUST NOT hien.
/// </summary>
public static class ProRenewal
{
    /// <summary>Khoa chuan (muc 7.6): <c>pro_until</c> da bao het ky, giay Unix.</summary>
    public const string NotifiedUntilKey = "tsu.pro_expired.notified_until";

    /// <summary>Khoa rieng: <c>pro_until</c> gan nhat may nay thay CON HAN ("JWS truoc do cua thiet bi tung co Pro").</summary>
    public const string SeenProUntilKey = "tsu.pro.seen_until";

    public static readonly TimeSpan Window = TimeSpan.FromDays(7);
    public static readonly TimeSpan ResumeGap = TimeSpan.FromMinutes(30);

    public static ProRenewalDecision Decide(Entitlement? entitlement, long? seenProUntil, long? notifiedUntil,
                                            DateTimeOffset now, bool interactive, TimeSpan? resumedAfter = null)
    {
        if (!interactive) return None(ProRenewalReason.NonInteractive);
        if (entitlement is null) return None(ProRenewalReason.NoEntitlement);
        if (resumedAfter is { } gap && gap < ResumeGap) return None(ProRenewalReason.ResumedRecently);

        var until = entitlement.ProUntil;
        var left = until - now.ToUnixTimeSeconds();
        if (left > 0)
        {
            if (left > (long)Window.TotalSeconds) return None(ProRenewalReason.NotNearExpiry, until);
            var days = (int)Math.Max(1, Math.Ceiling(left / TimeSpan.FromDays(1).TotalSeconds));
            return new(ProRenewalKind.Reminder, ProRenewalReason.Reminder, days, until);
        }
        if (until <= 0 || seenProUntil != until) return None(ProRenewalReason.NeverHadProHere, until);
        if (notifiedUntil == until) return None(ProRenewalReason.AlreadyNotified, until);
        return new(ProRenewalKind.Expired, ProRenewalReason.Expired, 0, until);
    }

    private static ProRenewalDecision None(ProRenewalReason r, long until = 0) => new(ProRenewalKind.None, r, 0, until);

    /// <summary>Ngay theo gio may: DD/MM/YYYY (vi), theo ngon ngu (en).</summary>
    public static string DateText(long unixSeconds)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        return Loc.IsEnglish
            ? local.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US"))
            : local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    // ---- Noi dung NGUYEN VAN muc 7.6 (khong sua tung chu) ----

    public static string ReminderTitle => Loc.T("Gia hạn TsuDEV Pro", "Renew TsuDEV Pro");

    public static string ReminderBody(int days, string date) => Loc.T(
        $"Gói TsuDEV Pro của bạn hết hạn sau {days} ngày (ngày {date}). Gia hạn để giữ huy hiệu và giao diện Pro. Nếu không gia hạn, tài khoản sẽ trở về bản miễn phí; mọi tính năng và dữ liệu trên thiết bị vẫn giữ nguyên.",
        $"Your TsuDEV Pro expires in {days} days (on {date}). Renew to keep the Pro badge and Pro themes. If you don't renew, your account returns to the free version; all features and data on your devices stay intact.");

    public static string ExpiredTitle => Loc.T("TsuDEV Pro đã hết hạn", "TsuDEV Pro has expired");

    public static string ExpiredBody => Loc.T(
        "Tài khoản đã trở về bản miễn phí: huy hiệu và giao diện Pro đã tắt. Mọi tính năng và dữ liệu trên thiết bị vẫn giữ nguyên.",
        "Your account is back on the free version: the Pro badge and Pro themes are off. All features and data on your devices stay intact.");

    public static string RenewButton => Loc.T("Gia hạn", "Renew");
    public static string LaterButton => Loc.T("Để sau", "Later");
    public static string CloseButton => Loc.T("Đóng", "Close");

    // ---- Muc 7.5: dieu khoan dung TRUOC nut/lien ket mua (nguyen van) ----

    public static string Price => Loc.T("490.000 ₫", "490,000 ₫");

    public static string PurchaseTerms => Loc.T(
        $"TsuDEV Pro {Price} cho 1 năm, KHÔNG tự gia hạn. Gia hạn sớm được cộng thêm 1 năm vào thời hạn còn lại. Hết hạn mà chưa gia hạn, tài khoản trở về bản miễn phí: huy hiệu và giao diện Pro tắt; mọi tính năng và dữ liệu trên thiết bị vẫn giữ nguyên.",
        $"TsuDEV Pro {Price} for 1 year, does NOT renew automatically. Renewing early adds 1 year to your remaining time. If it expires without renewal, your account returns to the free version: the Pro badge and Pro themes are turned off; all features and data on your devices stay intact.");

    // ---- Muc 3.7.3: man mua (desktop mo tsudev.com/pro; o xac nhan nam tren trang mua cua tsudev.com) ----

    /// <summary>Cau dinh vi nguyen van muc 3.7.3 diem 1 (tsudisk la du an OPEN - repo public).</summary>
    public static string Positioning => Loc.T(
        "Ứng dụng TsuDEV miễn phí; dự án gắn nhãn OPEN công khai mã nguồn trên GitHub. TsuDEV Pro không bắt buộc: bạn chọn Pro để ủng hộ tác giả duy trì và phát triển dự án, đổi lại tài khoản nhận thêm các quyền lợi dưới đây.",
        "TsuDEV apps are free; projects labelled OPEN publish their source code on GitHub. TsuDEV Pro is optional: choose Pro to support the author in maintaining and developing the project, and your account gets the extra benefits below.");

    /// <summary>
    /// Quyen loi DANG CHAY THAT trong TsuDisk (muc 3.7.3 diem 2): chi Q1 huy hieu (lenh tai-khoan in huy hieu theo JWS).
    /// Q2 giao dien Pro chua lam trong TsuDisk ⇒ MUST NOT hien.
    /// </summary>
    public static IReadOnlyList<string> ActiveBenefits => [Loc.T("Huy hiệu Pro cạnh tên tài khoản ở mọi ứng dụng và website TsuDEV",
                                                                 "Pro badge next to your account name in every TsuDEV app and website")];

    public const string PaymentPolicyUrl = "https://tsudev.com/chinh-sach-thanh-toan";
    public const string TermsUrl = "https://tsudev.com/terms";
}

public enum ProRenewalChoice { NotShown, Renew, Later }

/// <summary>
/// Hien nhac gia han / bao het ky o dau lan chay tuong tac. Khong chan lenh: moi phim khac "1" (ke ca Enter,
/// het dau vao) la "De sau"/"Dong". Khong doi ma thoat. <paramref name="readLine"/> = null (CLI TsuDisk): CHI IN
/// noi dung + lien ket gia han, khong cho phim - lenh dia chay tiep ngay (PRO2 muc 2.1). Ghi <see cref="ProRenewal.SeenProUntilKey"/> moi khi
/// thay Pro con han, va <see cref="ProRenewal.NotifiedUntilKey"/> TRUOC khi cho phim (tat ngang = da bao).
/// </summary>
public sealed class ProRenewalPrompt(
    IEntitlementSource entitlements,
    LocalStateStore state,
    string clientId,
    TextWriter output,
    Func<string?>? readLine,
    Action<string> openUrl)
{
    public ProRenewalChoice Run(DateTimeOffset now, bool interactive)
    {
        if (!interactive) return ProRenewalChoice.NotShown;
        var ent = entitlements.Current(now);
        if (ent is not null && ent.IsProAt(now) && state.GetLong(ProRenewal.SeenProUntilKey) != ent.ProUntil)
            state.SetLong(ProRenewal.SeenProUntilKey, ent.ProUntil);

        var d = ProRenewal.Decide(ent, state.GetLong(ProRenewal.SeenProUntilKey),
                                  state.GetLong(ProRenewal.NotifiedUntilKey), now, interactive);
        if (!d.Show) return ProRenewalChoice.NotShown;

        output.WriteLine();
        if (d.Kind == ProRenewalKind.Reminder)
        {
            output.WriteLine($"=== {ProRenewal.ReminderTitle} ===");
            output.WriteLine(ProRenewal.ReminderBody(d.DaysLeft, ProRenewal.DateText(d.ProUntil)));
            output.WriteLine();
            if (readLine is not null) output.WriteLine($"  [1] {ProRenewal.RenewButton}    [2] {ProRenewal.LaterButton}");
        }
        else
        {
            output.WriteLine($"=== {ProRenewal.ExpiredTitle} ===");
            output.WriteLine(ProRenewal.ExpiredBody);
            output.WriteLine();
            if (readLine is not null) output.WriteLine($"  [1] {ProRenewal.RenewButton}    [2] {ProRenewal.CloseButton}");
            state.SetLong(ProRenewal.NotifiedUntilKey, d.ProUntil);
        }
        if (readLine is null)
        {
            output.WriteLine($"  {ProRenewal.RenewButton}: {ProContract.ProUrl(clientId)}");
            output.WriteLine();
            return ProRenewalChoice.Later;
        }
        output.Write("> ");

        string? answer;
        try { answer = readLine(); }
        catch (IOException) { answer = null; }
        output.WriteLine();

        if (answer?.Trim() == "1")
        {
            openUrl(ProContract.ProUrl(clientId));
            return ProRenewalChoice.Renew;
        }
        return ProRenewalChoice.Later;
    }
}
