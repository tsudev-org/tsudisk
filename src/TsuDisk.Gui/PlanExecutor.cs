using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Planning;
using TsuDisk.Windows;

namespace TsuDisk.Gui;

/// <summary>
/// Duong GHI duy nhat cua giao dien (ADR-0009). Chi tao khi chay o that (khong co o che do du lieu mau/chup man hinh).
/// Moi lan chay di qua <see cref="PlanExecution.Run"/> - cung duong voi CLI: van tay bo cuc, kiem lai truoc moi buoc ghi,
/// hoan tac, SafetyGuard, nhat ky trong %ProgramData%\TsuDisk\nhat-ky.
/// </summary>
internal interface IPlanExecutor
{
    bool IsAdministrator { get; }

    /// <summary>Chay tren luong nen; <paramref name="progress"/> duoc goi tu luong do.</summary>
    PlanExecutionResult Run(Plan plan, Action<RunJournal> progress);

    /// <summary>Mo lai giao dien voi quyen quan tri (hop thoai UAC). false = nguoi dung tu choi / khong mo duoc.</summary>
    bool RelaunchAsAdministrator();
}

/// <param name="logRoot"><c>--thu-muc-nhat-ky</c>; null = %ProgramData%\TsuDisk\nhat-ky (cung thu muc voi CLI).</param>
internal sealed class WindowsPlanExecutor(string? logRoot = null) : IPlanExecutor
{
    /// <summary>Giai doan nay giao dien CHI ap dung thao tac rui ro Thap da duyet: doi chu cai/nhan (ADR-0009).</summary>
    public static readonly IReadOnlySet<string> AllowedOperations = new HashSet<string>(StringComparer.Ordinal) { RenamePlanner.Operation };

    private readonly WindowsDiskBackend _backend = new();

    private string LogRoot => logRoot ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TsuDisk", "nhat-ky");

    public bool IsAdministrator => SystemTools.IsAdministrator();

    public PlanExecutionResult Run(Plan plan, Action<RunJournal> progress)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!AllowedOperations.Contains(plan.Operation))
            throw new InvalidOperationException($"Giao diện chưa được phép áp dụng thao tác {plan.Operation}.");
        if (!IsAdministrator)
            throw new InvalidOperationException("Cần quyền quản trị.");
        return PlanExecution.Run(_backend, dir => new FileJournalStore(dir), plan, LogRoot, dryRun: false, App.Version, DateTime.Now, progress);
    }

    public bool RelaunchAsAdministrator()
    {
        if (Environment.ProcessPath is not { } exe)
            return false;
        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" })?.Dispose();
            return true;
        }
        catch (Win32Exception)
        {
            // 1223 = nguoi dung bam "Khong" o hop thoai UAC: o lai, khong bao loi.
            return false;
        }
    }
}
