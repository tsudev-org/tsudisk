using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Execution;

public enum StepStatus
{
    Pending,
    Running,
    Done,
    Skipped,
    Failed,
    RolledBack,
}

public enum RunState
{
    Running,
    Succeeded,
    /// <summary>That bai nhung da rollback ve trang thai an toan.</summary>
    RolledBack,
    /// <summary>That bai va rollback khong tron ven - can nguoi xu ly theo goi y trong nhat ky.</summary>
    NeedsAttention,
    /// <summary>Chay thu (--chay-thu): chi cac buoc khong doi o duoc thuc hien.</summary>
    DryRunCompleted,
    Aborted,
}

public sealed record StepRecord(int Index, StepKind Kind, string Title, StepStatus Status, DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt, string? Message);

/// <summary>
/// Nhat ky lan chay. Ghi xuong dia SAU MOI chuyen trang thai (ghi tep tam roi doi ten - nguyen tu), de mat dien giua
/// chung van biet chinh xac buoc nao da xong va can lam gi tiep (xem RecoveryAdvisor).
/// </summary>
public sealed record RunJournal(
    string RunId,
    Plan Plan,
    RunState State,
    IReadOnlyList<StepRecord> Steps,
    IReadOnlyList<string> Messages,
    string ToolVersion);

/// <summary>Noi luu nhat ky. Ban that ghi JSON vao thu muc lan chay; ban test giu trong bo nho.</summary>
public interface IJournalStore
{
    void Save(RunJournal journal);
}
