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
                long? oldSize = Units.IsAligned(victim.Size, Units.MiB) ? victim.Size : null;
                compensations.Add(new Compensation("recreate-recovery", $"Tạo lại Recovery tại vị trí cũ (offset {victim.Offset})",
                    () => backend.CreateRecoveryPartition(disk, victim.Offset, oldSize, style)));
                return null;
            }
            case StepKind.DeleteDataPartition:
            {
                var victim = RequirePartition(disk, step.Offset!.Value, step.PartitionNumber);
                if (victim.Kind != PartitionKind.BasicData || victim.IsSystem || victim.IsBoot || victim.Size != step.Size
                    || victim.DriveLetter != step.DriveLetter || (victim.Label ?? "") != (step.NewLabel ?? ""))
                {
                    throw new DiskOperationException($"Phân vùng {victim.PartitionNumber} không còn đúng là ổ {step.DriveLetter}: \"{step.NewLabel}\" " +
                        $"{Units.Format(step.Size!.Value)} như kế hoạch (chữ cái/nhãn/kích thước đã đổi) - dừng, không xóa.");
                }
                var letter = step.DriveLetter!.Value;
                backend.DeleteDataPartition(disk, victim.PartitionNumber, letter);
                // KHONG tu tao lai: Windows xoa trang dau phan vung moi (do o E2E) - tao lai chi lam mat boot sector. Doi chieu
                // loi sau buoc nay => can nguoi xu ly bang TestDisk (ADR-0005 muc 6).
                var hint = DeletePartitionPlanner.RecoveryHint(plan.Before, victim);
                compensations.Add(new Compensation("cannot-undo-delete", $"Ổ {letter}: đã xóa - không tự hoàn tác", () => throw new DiskOperationException(hint)));
                return $"Đã xóa ổ {letter}: ({Units.Format(victim.Size)} trống).";
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
            case StepKind.ShrinkPartition:
            {
                var target = RequirePartition(disk, step.Offset!.Value, step.PartitionNumber);
                var min = backend.GetMinPartitionSize(disk, target.PartitionNumber);
                if (min > step.Size!.Value)
                    throw new DiskOperationException($"Windows chỉ cho thu nhỏ {target.DriveLetter}: tới {Units.Format(min)} (dữ liệu không dời được nằm ở cuối ổ), kế hoạch cần {Units.Format(step.Size.Value)}.");
                var oldSize = target.Size;
                backend.ResizePartition(disk, target.PartitionNumber, step.Size.Value);
                compensations.Add(new Compensation("regrow", $"Nới {target.DriveLetter}: về lại {Units.Format(oldSize)}",
                    () => backend.ResizePartition(disk, target.PartitionNumber, oldSize)));
                var shrunk = RequirePartition(disk, step.Offset.Value, target.PartitionNumber);
                if (Math.Abs(shrunk.Size - step.Size.Value) > SafetyGuard.SizeTolerance)
                    throw new DiskOperationException($"Sau khi thu nhỏ, kích thước thật là {Units.Format(shrunk.Size)}.");
                return null;
            }
            case StepKind.CreateRecovery:
            {
                long? exact = plan.Operation == WinReGrowPlanner.Operation ? step.Size : null;
                backend.CreateRecoveryPartition(disk, step.Offset!.Value, exact, plan.Before.Disk.Style);
                // Recovery moi da nam dung cho: khong con duong lui "tao lai Recovery cu" hay "noi lai o" nua.
                compensations.RemoveAll(c => c.Key is "recreate-recovery" or "regrow");
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
            case StepKind.SetVolumeLabel:
            {
                var target = RequireRenameTarget(disk, step);
                var oldLabel = target.Label ?? "";
                backend.SetVolumeLabel(disk, target.PartitionNumber, step.NewLabel!);
                compensations.Add(new Compensation("restore-label", $"Đặt lại nhãn \"{oldLabel}\"",
                    () => backend.SetVolumeLabel(disk, target.PartitionNumber, oldLabel)));
                var now = RequirePartition(disk, step.Offset!.Value, target.PartitionNumber);
                if (!string.Equals(now.Label ?? "", step.NewLabel, StringComparison.OrdinalIgnoreCase))
                    throw new DiskOperationException($"Sau khi đổi, nhãn thật là \"{now.Label}\".");
                return null;
            }
            case StepKind.SetDriveLetter:
            {
                var target = RequireRenameTarget(disk, step);
                var next = step.NewDriveLetter!.Value;
                if (backend.DriveLettersInUse().Contains(next))
                    throw new DiskOperationException($"Chữ cái {next}: vừa bị chiếm kể từ lúc lập kế hoạch - dừng.");
                var old = target.DriveLetter!.Value;
                backend.SetDriveLetter(disk, target.PartitionNumber, old, next);
                // Bu theo chu cai THAT luc hoan tac (Windows co the da gan khac, hoac chua gan gi).
                compensations.Add(new Compensation("restore-letter", $"Gán lại chữ cái {old}:", () =>
                {
                    var actual = RequirePartition(disk, step.Offset!.Value, target.PartitionNumber).DriveLetter;
                    if (actual != old)
                        backend.SetDriveLetter(disk, target.PartitionNumber, actual, old);
                }));
                var now = RequirePartition(disk, step.Offset!.Value, target.PartitionNumber);
                if (now.DriveLetter != next)
                    throw new DiskOperationException($"Sau khi đổi, ổ mang chữ cái {now.DriveLetter}: thay vì {next}:.");
                return null;
            }
            case StepKind.CreatePartition:
            {
                var offset = step.Offset!.Value;
                var size = step.Size!.Value;
                if (backend.ReadLayout(disk).Partitions.Any(p => p.Offset < offset + size && p.End > offset))
                    throw new DiskOperationException($"Vùng từ offset {offset} không còn trống - bố cục đã đổi, dừng.");
                // Dang ky TRUOC khi tao: lenh tao loi giua chung van co the de lai phan vung do dang.
                compensations.Add(new Compensation("delete-new", $"Xóa phân vùng vừa tạo tại offset {offset}",
                    () => DeleteCreated(disk, plan, step)));
                backend.CreateDataPartition(disk, offset, size, plan.Before.Disk.Style);
                var created = backend.ReadLayout(disk).FindByOffset(offset)
                    ?? throw new DiskOperationException($"Không thấy phân vùng mới tại offset {offset}.");
                if (created.Kind != PartitionKind.BasicData || Math.Abs(created.Size - size) > SafetyGuard.SizeTolerance)
                    throw new DiskOperationException($"Phân vùng mới không đúng loại/kích thước ({Units.Format(created.Size)}).");
                return $"Phân vùng {created.PartitionNumber}, {Units.Format(created.Size)}";
            }
            case StepKind.FormatVolume:
            {
                var target = RequirePartition(disk, step.Offset!.Value, null);
                if (plan.Before.FindByOffset(target.Offset) is not null)
                    throw new DiskOperationException("Phân vùng này có từ trước kế hoạch - cấm định dạng.");
                if (target.FileSystem is not (null or "RAW"))
                {
                    throw new DiskOperationException($"Phân vùng mới hiện ra hệ tệp {target.FileSystem} - vùng trống này có thể còn dữ liệu của " +
                        "một phân vùng đã xóa trước đây. KHÔNG định dạng; phân vùng sẽ được gỡ để giữ nguyên dữ liệu đó (khôi phục bằng TestDisk nếu cần).");
                }
                if (step.NewDriveLetter is { } wanted && backend.DriveLettersInUse().Contains(wanted))
                    throw new DiskOperationException($"Chữ cái {wanted}: vừa bị chiếm kể từ lúc lập kế hoạch - dừng.");
                backend.FormatVolume(disk, target.PartitionNumber, step.FileSystem!, step.NewLabel ?? "", step.NewDriveLetter);
                var now = RequirePartition(disk, target.Offset, null);
                if (!string.Equals(now.FileSystem, step.FileSystem, StringComparison.OrdinalIgnoreCase) || now.DriveLetter != step.NewDriveLetter)
                    throw new DiskOperationException($"Sau khi định dạng: hệ tệp {now.FileSystem ?? "RAW"}, chữ cái {now.DriveLetter} - không như kế hoạch.");
                return $"{now.FileSystem}, {now.DriveLetter}:";
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

    /// <summary>Buoc bu cua CreatePartition: chi xoa khi chac chan la phan vung vua tao; khong co gi tai offset thi thoi.</summary>
    private void DeleteCreated(int disk, Plan plan, PlanStep step)
    {
        var offset = step.Offset!.Value;
        var found = backend.ReadLayout(disk).FindByOffset(offset);
        if (found is null)
            return;
        if (plan.Before.FindByOffset(offset) is not null || found.Kind != PartitionKind.BasicData
            || Math.Abs(found.Size - step.Size!.Value) > SafetyGuard.SizeTolerance)
            throw new DiskOperationException($"Phân vùng tại offset {offset} không khớp phân vùng vừa tạo - KHÔNG xóa.");
        backend.DeletePartition(disk, found.PartitionNumber);
    }

    /// <summary>Doi chu cai/nhan: phan vung tai offset phai con dung chu cai, dung kich thuoc, dung loai nhu ke hoach.</summary>
    private PartitionInfo RequireRenameTarget(int disk, PlanStep step)
    {
        var target = RequirePartition(disk, step.Offset!.Value, step.PartitionNumber);
        if (target.DriveLetter != step.DriveLetter || target.Size != step.Size || target.Kind != PartitionKind.BasicData)
            throw new DiskOperationException($"Phân vùng {target.PartitionNumber} không còn đúng là ổ {step.DriveLetter}: như kế hoạch - dừng.");
        return target;
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
