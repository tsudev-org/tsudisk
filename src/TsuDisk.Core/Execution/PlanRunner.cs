using TsuDisk.Core.Health;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;

namespace TsuDisk.Core.Execution;

public sealed record RunOptions(bool DryRun, string RunDirectory, string ToolVersion);

/// <summary>
/// Chay ke hoach qua <see cref="IDiskBackend"/>. Nguyen tac:
/// <list type="bullet">
/// <item>Truoc khi bat dau: doc lai bo cuc, dau van tay phai trung voi luc lap ke hoach.</item>
/// <item>Truoc MOI buoc doi o: kiem lai dieu kien tien quyet tren bo cuc doc moi (dung phan vung, dung offset, dung loai).</item>
/// <item>Sau moi buoc: kiem chung hau qua (vd. kich thuoc that sau khi mo rong).</item>
/// <item>Loi giua chung: chay cac buoc bu (compensation) theo thu tu nguoc de tro ve trang thai an toan.</item>
/// <item>Cuoi cung: SafetyGuard.CheckActual doi chieu bo cuc that voi bo cuc du kien.</item>
/// </list>
/// </summary>
public sealed class PlanRunner(IDiskBackend backend, IJournalStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public RunJournal Run(Plan plan, RunOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);
        var session = new Session(plan, options, store, _clock);

        var current = backend.ReadLayout(plan.DiskNumber);
        if (LayoutFingerprint.Compute(current) != plan.LayoutFingerprint)
            return session.Finish(RunState.Aborted, "Bố cục đĩa đã thay đổi kể từ lúc lập kế hoạch - lập lại kế hoạch rồi chạy lại.");

        var compensations = new List<Compensation>();
        foreach (var step in plan.Steps)
        {
            if (options.DryRun && step.ChangesDisk || options.DryRun && step.Kind is StepKind.DisableWinRe or StepKind.EnableWinRe)
            {
                session.Mark(step, StepStatus.Skipped, "Chạy thử: bỏ qua bước thay đổi hệ thống.");
                continue;
            }
            if (options.DryRun && step.Kind == StepKind.VerifyLayout)
            {
                session.Mark(step, StepStatus.Skipped, "Chạy thử: không có gì để đối chiếu.");
                continue;
            }

            session.Mark(step, StepStatus.Running, null);
            try
            {
                var message = Execute(step, plan, options, compensations);
                session.Mark(step, StepStatus.Done, message);
            }
            catch (Exception ex)
            {
                // Bat rong co chu dich: moi loi (ke ca ManagementException/IOException cua backend) phai dan toi
                // rollback, khong duoc lot ra ngoai giua chung.
                session.Mark(step, StepStatus.Failed, ex.Message);
                return session.Finish(Rollback(compensations, session), $"Bước {step.Index} thất bại: {ex.Message}");
            }
        }

        return session.Finish(options.DryRun ? RunState.DryRunCompleted : RunState.Succeeded,
            options.DryRun ? "Chạy thử xong - không có thay đổi nào trên đĩa." : "Hoàn tất, bố cục thật khớp kế hoạch.");
    }

    private string? Execute(PlanStep step, Plan plan, RunOptions options, List<Compensation> compensations)
    {
        var disk = plan.DiskNumber;
        switch (step.Kind)
        {
            case StepKind.CheckHealth:
            {
                var verdict = HealthEvaluator.Evaluate(backend.ReadHealth(disk));
                if (!verdict.AllowsWrites)
                    throw new DiskOperationException("Ổ đĩa có dấu hiệu hỏng - không thay đổi bảng phân vùng. Sao lưu dữ liệu và thay ổ trước: " + string.Join(" ", verdict.Reasons));
                return $"{verdict.Level}: {string.Join(" ", verdict.Reasons)}";
            }
            case StepKind.ScanVolume:
            {
                var scan = backend.ScanVolume(step.DriveLetter!.Value);
                if (!scan.Healthy)
                    throw new DiskOperationException($"Hệ thống tệp {step.DriveLetter}: báo lỗi ({scan.Detail}). Chạy chkdsk /f trước.");
                return scan.Detail;
            }
            case StepKind.Backup:
                backend.SaveBackups(options.RunDirectory, plan.Before);
                return $"Đã lưu bản sao lưu vào {options.RunDirectory}";

            case StepKind.DisableWinRe:
                backend.DisableWinRe();
                compensations.Add(new Compensation("enable-winre", "Bật lại WinRE", backend.EnableWinRe));
                if (!backend.WinReImageOnSystemDrive())
                    throw new DiskOperationException("Sau khi tắt WinRE không thấy Winre.wim trên ổ hệ thống - dừng để không mất ảnh khôi phục.");
                return "Winre.wim đã nằm trên ổ hệ thống.";

            case StepKind.DeletePartition:
            {
                var victim = RequirePartition(disk, step.Offset!.Value, step.PartitionNumber);
                if (victim.Kind != PartitionKind.Recovery || victim.Size != step.Size)
                    throw new DiskOperationException($"Phân vùng {victim.PartitionNumber} không còn đúng là Recovery {step.Size} byte như kế hoạch.");
                backend.DeletePartition(disk, victim.PartitionNumber);
                var style = plan.Before.Disk.Style;
                compensations.Add(new Compensation("recreate-recovery", $"Tạo lại Recovery tại vị trí cũ (offset {victim.Offset})",
                    () => backend.CreateRecoveryPartition(disk, victim.Offset, style)));
                return null;
            }
            case StepKind.ExtendPartition:
            {
                var target = RequirePartition(disk, step.Offset!.Value, step.PartitionNumber);
                var max = backend.GetMaxPartitionSize(disk, target.PartitionNumber);
                if (max < step.Size!.Value)
                    throw new DiskOperationException($"Windows chỉ cho mở rộng tới {Units.Format(max)}, kế hoạch cần {Units.Format(step.Size.Value)}.");
                backend.ResizePartition(disk, target.PartitionNumber, step.Size.Value);
                var after = RequirePartition(disk, step.Offset.Value, target.PartitionNumber);
                if (Math.Abs(after.Size - step.Size.Value) > SafetyGuard.SizeTolerance)
                    throw new DiskOperationException($"Sau khi mở rộng, kích thước thật là {Units.Format(after.Size)}.");
                // Vi tri Recovery cu nay thuoc o dich: bo buoc bu "tao lai Recovery cu". Khong thu nho lai - o lon hon la
                // trang thai an toan. Buoc bu "bat lai WinRE" van giu: WinRE chay duoc tu Winre.wim tren o he thong.
                compensations.RemoveAll(c => c.Key == "recreate-recovery");
                return null;
            }
            case StepKind.CreateRecovery:
            {
                backend.CreateRecoveryPartition(disk, step.Offset!.Value, plan.Before.Disk.Style);
                var created = backend.ReadLayout(disk).FindByOffset(step.Offset.Value)
                    ?? throw new DiskOperationException($"Không thấy phân vùng Recovery mới tại offset {step.Offset}.");
                if (created.Kind != PartitionKind.Recovery)
                    throw new DiskOperationException("Phân vùng mới không mang loại Recovery.");
                return $"Phân vùng {created.PartitionNumber}, {Units.Format(created.Size)}";
            }
            case StepKind.EnableWinRe:
            {
                backend.EnableWinRe();
                var status = backend.ReadWinRe();
                if (!status.Enabled)
                    throw new DiskOperationException("WinRE vẫn tắt sau reagentc /enable.");
                var recovery = backend.ReadLayout(disk).Partitions.FirstOrDefault(p => status.LivesOn(p));
                return recovery is { Kind: PartitionKind.Recovery }
                    ? $"WinRE nằm trên phân vùng {recovery.PartitionNumber}."
                    : $"Cảnh báo: WinRE đang bật nhưng nằm ở đĩa {status.DiskNumber} phân vùng {status.PartitionNumber}, không phải Recovery mới.";
            }
            case StepKind.VerifyLayout:
            {
                var violations = SafetyGuard.CheckActual(plan, backend.ReadLayout(disk));
                if (violations.Count > 0)
                    throw new InvalidOperationException("Bố cục thật lệch kế hoạch: " + string.Join(" | ", violations));
                return "Mọi phân vùng khác giữ nguyên vị trí và kích thước.";
            }
            default:
                throw new InvalidOperationException($"Loại bước chưa hỗ trợ: {step.Kind}");
        }
    }

    private PartitionInfo RequirePartition(int disk, long offset, int? expectedNumber)
    {
        var found = backend.ReadLayout(disk).FindByOffset(offset)
            ?? throw new DiskOperationException($"Không còn phân vùng tại offset {offset} - bố cục đã đổi, dừng.");
        if (expectedNumber is { } n && found.PartitionNumber != n)
            throw new DiskOperationException($"Phân vùng tại offset {offset} mang số {found.PartitionNumber}, kế hoạch ghi {n} - dừng.");
        return found;
    }

    private sealed record Compensation(string Key, string Title, Action Undo);

    private static RunState Rollback(List<Compensation> compensations, Session session)
    {
        var clean = true;
        for (var i = compensations.Count - 1; i >= 0; i--)
        {
            var (_, title, undo) = compensations[i];
            try
            {
                undo();
                session.Note($"Đã hoàn tác: {title}");
            }
            catch (Exception ex)
            {
                // Bat rong co chu dich: mot buoc bu hong khong duoc chan cac buoc bu con lai.
                clean = false;
                session.Note($"KHÔNG hoàn tác được: {title} - {ex.Message}");
            }
        }
        return clean ? RunState.RolledBack : RunState.NeedsAttention;
    }

    private sealed class Session(Plan plan, RunOptions options, IJournalStore store, TimeProvider clock)
    {
        private readonly List<StepRecord> _steps = [.. plan.Steps.Select(s =>
            new StepRecord(s.Index, s.Kind, s.Title, StepStatus.Pending, null, null, null))];
        private readonly List<string> _messages = [];
        private readonly string _runId = Path.GetFileName(options.RunDirectory);

        public void Mark(PlanStep step, StepStatus status, string? message)
        {
            var i = step.Index - 1;
            var now = clock.GetLocalNow();
            _steps[i] = _steps[i] with
            {
                Status = status,
                StartedAt = status == StepStatus.Running ? now : _steps[i].StartedAt ?? now,
                FinishedAt = status == StepStatus.Running ? null : now,
                Message = message,
            };
            Save(RunState.Running);
        }

        public void Note(string message)
        {
            _messages.Add(message);
            Save(RunState.Running);
        }

        public RunJournal Finish(RunState state, string message)
        {
            _messages.Add(message);
            return Save(state);
        }

        private RunJournal Save(RunState state)
        {
            var journal = new RunJournal(_runId, plan, state, [.. _steps], [.. _messages], options.ToolVersion);
            store.Save(journal);
            return journal;
        }
    }
}
