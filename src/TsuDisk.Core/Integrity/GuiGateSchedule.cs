namespace TsuDisk.Core.Integrity;

/// <summary>
/// Lich kiem + trang thai khoa cua GUI (ADR-0012 muc 7 + 10, APP_INTEGRITY 2.2). Thuan logic, dong ho tiem vao - test tren
/// Linux; <c>TsuDisk.Gui</c> chi noi su kien cua so/bo dem gio vao day.
///
/// - Kiem khi mo; moi <see cref="Period"/>; khi cua so duoc kich hoat lai sau &gt;= <see cref="ReactivateAfter"/> o nen.
/// - Khong chay hai luot kiem chong nhau; da khoa thi thoi kiem (khoa la mot chieu trong phien - khong "mo khoa" giua chung).
/// - Dang co thao tac dia chay do ⇒ GIU quyet dinh khoa, chi hien modal khi thao tac xong (khong bao gio ngat ghi dia).
/// </summary>
public sealed class GuiGateSchedule(Func<DateTimeOffset> clock)
{
    public static readonly TimeSpan Period = TimeSpan.FromHours(6);

    public static readonly TimeSpan ReactivateAfter = TimeSpan.FromMinutes(30);

    private DateTimeOffset? deactivatedAt;
    private bool checking;

    /// <summary>Quyet dinh khoa da nhan (co the chua hien vi dang ban).</summary>
    public GateDecision? Lock { get; private set; }

    /// <summary>Modal da hien (hoac da yeu cau hien).</summary>
    public bool Shown { get; private set; }

    public bool IsLocked => Lock is not null;

    /// <summary>Cua so mat focus (nguoi dung chuyen sang app khac/thu nho).</summary>
    public void Deactivated() => deactivatedAt ??= clock();

    /// <returns><c>true</c> neu da o nen du lau ⇒ nen kiem lai.</returns>
    public bool Activated()
    {
        var since = deactivatedAt;
        deactivatedAt = null;
        return since is { } t && clock() - t >= ReactivateAfter;
    }

    /// <summary>Bat dau mot luot kiem; <c>false</c> = dang co luot khac, hoac da khoa ⇒ bo qua.</summary>
    public bool TryBeginCheck()
    {
        if (checking || IsLocked) return false;
        checking = true;
        return true;
    }

    /// <summary>Ket thuc luot kiem.</summary>
    /// <returns>Quyet dinh can hien modal NGAY; <c>null</c> neu khong khoa, da hien, hoac dang ban (cho <see cref="Idle"/>).</returns>
    public GateDecision? EndCheck(GateDecision? decision, bool busy)
    {
        checking = false;
        if (decision is { IsLocked: true } && Lock is null)
            Lock = decision;
        return busy ? null : TakePending();
    }

    /// <summary>Thao tac dia vua xong. Tra quyet dinh khoa dang cho (neu co) de hien modal.</summary>
    public GateDecision? Idle() => TakePending();

    private GateDecision? TakePending()
    {
        if (Lock is null || Shown) return null;
        Shown = true;
        return Lock;
    }
}

/// <summary>Nut "Cap nhat ngay" theo kenh (ADR-0012 muc 10).</summary>
/// <param name="OpenUrl">GitHub: mo trang nay (luon https).</param>
/// <param name="Command">Scoop/winget: lenh hien kem nut sao chep.</param>
public sealed record UpdateAction(Uri? OpenUrl, string? Command)
{
    public static readonly Uri ReleasesPage = new("https://github.com/tsudev-org/tsudisk/releases/latest");

    public static UpdateAction For(string channel, Uri? url) => channel switch
    {
        "scoop" or "winget" => new UpdateAction(null, CommandGate.UpdateHint(channel, url)),
        // url den tu ban ke da kiem chu ky hoac 426 cua tsudev.com (da loc https); van chot lai: khong phai https ⇒ trang Release.
        _ => new UpdateAction(url is { Scheme: "https" } ? url : ReleasesPage, null),
    };
}
