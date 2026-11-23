using System.Globalization;
using System.Text;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Serialization;

namespace TsuDisk.Core.Execution;

/// <summary>Ket qua mot lan chay: nhat ky + thu muc chua plan.json/journal.json/ban sao luu.</summary>
public sealed record PlanExecutionResult(RunJournal Journal, string RunDirectory);

/// <summary>
/// Mot duong chay ke hoach dung CHUNG cho CLI va giao dien (ADR-0009): tao thu muc lan chay
/// <c>&lt;goc&gt;/yyyyMMdd-HHmmss-&lt;id&gt;[-chay-thu]</c>, ghi <c>plan.json</c> TRUOC khi cham dia, roi giao cho
/// <see cref="PlanRunner"/> (van tay bo cuc, kiem lai truoc moi buoc ghi, hoan tac, SafetyGuard). Khong co nhanh rieng
/// cho giao dien: giao dien chi khac cach hoi xac nhan va cach hien tien do.
/// </summary>
public static class PlanExecution
{
    public static string RunDirectoryName(Plan plan, DateTime now, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return $"{now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{plan.Id}{(dryRun ? "-chay-thu" : "")}";
    }

    /// <param name="backend">Backend that (Windows) hoac gia (test).</param>
    /// <param name="storeFor">Tao kho nhat ky cho thu muc lan chay (ban that: FileJournalStore cua TsuDisk.Windows).</param>
    /// <param name="logRoot">Thu muc goc nhat ky, mac dinh %ProgramData%\TsuDisk\nhat-ky.</param>
    /// <param name="progress">Goi sau MOI lan luu nhat ky (tien do tung buoc); null = khong bao.</param>
    public static PlanExecutionResult Run(IDiskBackend backend, Func<string, IJournalStore> storeFor, Plan plan, string logRoot,
        bool dryRun, string toolVersion, DateTime now, Action<RunJournal>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(storeFor);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrEmpty(logRoot);
        var runDirectory = Path.Combine(logRoot, RunDirectoryName(plan, now, dryRun));
        Directory.CreateDirectory(runDirectory);
        File.WriteAllText(Path.Combine(runDirectory, "plan.json"), TsuDiskJson.Serialize(plan), Encoding.UTF8);
        var inner = storeFor(runDirectory);
        IJournalStore store = progress is null ? inner : new ReportingStore(inner, progress);
        var journal = new PlanRunner(backend, store).Run(plan, new RunOptions(dryRun, runDirectory, toolVersion));
        return new PlanExecutionResult(journal, runDirectory);
    }

    private sealed class ReportingStore(IJournalStore inner, Action<RunJournal> progress) : IJournalStore
    {
        public void Save(RunJournal journal)
        {
            inner.Save(journal);
            progress(journal);
        }
    }
}
