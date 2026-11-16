using TsuDisk.Core.Model;
using TsuDisk.Core.Safety;

namespace TsuDisk.Core.Planning;

/// <summary>
/// Lap ke hoach mo rong mot o bang vung trong LIEN KE. Hai mau duoc ho tro (tuong ung quy trinh Microsoft KB5028997):
/// <list type="number">
/// <item>[Dich][Vung trong] - mo rong truc tiep.</item>
/// <item>[Dich][Recovery][Vung trong] - tat WinRE, xoa Recovery, mo rong, tao Recovery moi ngay sau dich, bat WinRE.</item>
/// </list>
/// Moi bo cuc khac deu bi tu choi kem giai thich: mo rong qua mot phan vung du lieu doi hoi DOI CHO du lieu, la thao tac
/// rui ro cao nhat va TsuDisk co chu dich khong lam.
/// </summary>
public static class ExtendPlanner
{
    public static PlanResult Plan(DiskLayout layout, WinReStatus winRe, ExtendRequest request, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(winRe);
        ArgumentNullException.ThrowIfNull(request);
        clock ??= TimeProvider.System;

        if (request.RecoverySize is < ExtendRequest.MinRecoverySize or > ExtendRequest.MaxRecoverySize)
        {
            return PlanResult.Refuse(RefusalCode.InvalidOption,
                $"Kích thước Recovery phải nằm trong {Units.Format(ExtendRequest.MinRecoverySize)} - {Units.Format(ExtendRequest.MaxRecoverySize)}.");
        }

        var target = layout.FindByLetter(request.TargetLetter);
        if (target is null)
            return PlanResult.Refuse(RefusalCode.TargetNotFound, $"Không tìm thấy ổ {char.ToUpperInvariant(request.TargetLetter)}: trên đĩa {layout.Disk.Number}.");

        var precheck = CheckDiskAndTarget(layout, target, request);
        if (precheck is not null)
            return precheck;

        var next = layout.SegmentAfter(target);
        if (next is { IsFree: true })
            return BuildDirect(layout, target, next, request, clock);

        if (next?.Partition is { Kind: PartitionKind.Recovery } recovery)
        {
            var afterRecovery = layout.SegmentAfter(next);
            if (afterRecovery is { IsFree: true })
                return BuildRecoveryRelocation(layout, winRe, target, recovery, afterRecovery, request, clock);
        }

        return RefuseNotAdjacent(layout, target, next);
    }

    private static PlanResult? CheckDiskAndTarget(DiskLayout layout, PartitionInfo target, ExtendRequest request)
    {
        var disk = layout.Disk;
        if (disk.IsReadOnly || disk.IsOffline)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} đang chỉ-đọc hoặc offline.");
        if (request.VirtualDiskOnly && !disk.IsVirtual)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} không phải ổ ảo, mà chế độ chỉ-ổ-ảo đang bật.");
        if (disk.Style is not (PartitionStyle.Gpt or PartitionStyle.Mbr))
            return PlanResult.Refuse(RefusalCode.UnsupportedPartitionStyle, $"Đĩa {disk.Number} chưa khởi tạo (RAW).");
        if (layout.Partitions.Any(p => p.Kind is PartitionKind.LdmMetadata or PartitionKind.LdmData or PartitionKind.StorageSpaces))
        {
            return PlanResult.Refuse(RefusalCode.DynamicDisk,
                $"Đĩa {disk.Number} là ổ động (Dynamic) hoặc thuộc Storage Spaces - cấu trúc này không quản lý bằng bảng phân vùng thường.",
                "Dùng Disk Management hoặc Storage Spaces để quản lý đĩa này.");
        }
        if (PartitionTypes.IsProtected(target.Kind) || target.Kind != PartitionKind.BasicData)
        {
            return PlanResult.Refuse(RefusalCode.ProtectedTarget,
                $"{target.DisplayName} là {PartitionTypes.Describe(target.Kind)}, không phải ổ dữ liệu - không mở rộng.");
        }
        if (target.FileSystem is not ("NTFS" or "ReFS"))
        {
            return PlanResult.Refuse(RefusalCode.UnsupportedFileSystem,
                $"{target.DisplayName} dùng {target.FileSystem ?? "hệ thống tệp không rõ"}; Windows chỉ mở rộng trực tuyến NTFS và ReFS.",
                "FAT32/exFAT: sao lưu dữ liệu, xóa và tạo lại phân vùng với kích thước mới.");
        }
        return null;
    }

    private static PlanResult BuildDirect(DiskLayout layout, PartitionInfo target, Segment free, ExtendRequest request, TimeProvider clock)
    {
        var newEnd = Units.AlignDown(free.End, Units.PartitionAlignment);
        var newSize = newEnd - target.Offset;
        var gain = newSize - target.Size;
        if (gain < request.MinimumGain)
            return RefuseTooSmall(gain, request);

        var steps = new StepBuilder();
        steps.Add(StepKind.CheckHealth, "Kiểm tra sức khỏe ổ đĩa (S.M.A.R.T., chỉ đọc)", changesDisk: false);
        steps.Add(StepKind.ScanVolume, $"Quét hệ thống tệp {target.DriveLetter}: (chỉ đọc)", changesDisk: false, driveLetter: target.DriveLetter);
        steps.Add(StepKind.Backup, "Lưu ảnh bảng phân vùng và BCD vào thư mục nhật ký", changesDisk: false);
        steps.Add(StepKind.ExtendPartition,
            $"Mở rộng {target.DriveLetter}: từ {Units.Format(target.Size)} lên {Units.Format(newSize)}",
            changesDisk: true, partitionNumber: target.PartitionNumber, offset: target.Offset, size: newSize, driveLetter: target.DriveLetter);
        steps.Add(StepKind.VerifyLayout, "Đọc lại bảng phân vùng, đối chiếu với kết quả dự kiến", changesDisk: false);

        return Finish(layout, target, steps.Build(), gain, clock,
            [$"Vùng trống {Units.Format(free.Size)} nằm ngay sau {target.DriveLetter}: - mở rộng trực tiếp, không cần khởi động lại."]);
    }

    private static PlanResult BuildRecoveryRelocation(DiskLayout layout, WinReStatus winRe, PartitionInfo target,
        PartitionInfo recovery, Segment free, ExtendRequest request, TimeProvider clock)
    {
        var winReHere = winRe.LivesOn(recovery);
        if (!winReHere && request.RecoveryPolicy != RecoveryPolicy.RecreateEmpty)
        {
            return PlanResult.Refuse(RefusalCode.RecoveryNotWinRe,
                $"{recovery.DisplayName} không chứa WinRE đang dùng (WinRE: {(winRe.Enabled ? $"đĩa {winRe.DiskNumber} phân vùng {winRe.PartitionNumber}" : "đang tắt")}). Có thể là phân vùng khôi phục của hãng máy.",
                "Kiểm tra nội dung phân vùng trước. Nếu chắc chắn không cần, chạy lại với --recovery tao-lai-rong.");
        }

        var recoverySize = Math.Max(Units.AlignUp(request.RecoverySize, Units.MiB), Units.AlignUp(recovery.Size, Units.MiB));
        var freeEnd = Units.AlignDown(free.End, Units.PartitionAlignment);
        var newRecoveryOffset = Units.AlignDown(freeEnd - recoverySize, Units.PartitionAlignment);
        if (newRecoveryOffset <= recovery.Offset)
        {
            return PlanResult.Refuse(RefusalCode.RecoveryTooSmallGap,
                $"Vùng trống sau Recovery ({Units.Format(free.Size)}) không đủ để vừa dời Recovery ({Units.Format(recoverySize)}) vừa có lợi.");
        }
        var newSize = newRecoveryOffset - target.Offset;
        var gain = newSize - target.Size;
        if (gain < request.MinimumGain)
            return RefuseTooSmall(gain, request);

        var steps = new StepBuilder();
        steps.Add(StepKind.CheckHealth, "Kiểm tra sức khỏe ổ đĩa (S.M.A.R.T., chỉ đọc)", changesDisk: false);
        steps.Add(StepKind.ScanVolume, $"Quét hệ thống tệp {target.DriveLetter}: (chỉ đọc)", changesDisk: false, driveLetter: target.DriveLetter);
        steps.Add(StepKind.Backup, "Lưu ảnh bảng phân vùng, BCD và ảnh WinRE vào thư mục nhật ký", changesDisk: false);
        if (winReHere)
            steps.Add(StepKind.DisableWinRe, "Tắt WinRE (Windows chép Winre.wim về ổ hệ thống)", changesDisk: false);
        steps.Add(StepKind.DeletePartition, $"Xóa {recovery.DisplayName} ({Units.Format(recovery.Size)})",
            changesDisk: true, partitionNumber: recovery.PartitionNumber, offset: recovery.Offset, size: recovery.Size);
        steps.Add(StepKind.ExtendPartition, $"Mở rộng {target.DriveLetter}: từ {Units.Format(target.Size)} lên {Units.Format(newSize)}",
            changesDisk: true, partitionNumber: target.PartitionNumber, offset: target.Offset, size: newSize, driveLetter: target.DriveLetter);
        steps.Add(StepKind.CreateRecovery, $"Tạo phân vùng Recovery mới {Units.Format(freeEnd - newRecoveryOffset)} ngay sau {target.DriveLetter}:",
            changesDisk: true, offset: newRecoveryOffset, size: freeEnd - newRecoveryOffset);
        if (winReHere)
            steps.Add(StepKind.EnableWinRe, "Bật lại WinRE vào phân vùng Recovery mới", changesDisk: false);
        steps.Add(StepKind.VerifyLayout, "Đọc lại bảng phân vùng, đối chiếu với kết quả dự kiến", changesDisk: false);

        var notes = new List<string>
        {
            $"Phân vùng Recovery chắn giữa {target.DriveLetter}: và {Units.Format(free.Size)} trống - dời Recovery ra sau {target.DriveLetter}: theo quy trình Microsoft KB5028997.",
            winReHere
                ? "WinRE được bảo toàn: tắt trước khi xóa (ảnh WinRE nằm an toàn trên ổ hệ thống), bật lại sau khi tạo Recovery mới."
                : "Phân vùng Recovery cũ KHÔNG chứa WinRE đang dùng; Recovery mới được tạo rỗng (người dùng đã chọn --recovery tao-lai-rong).",
        };
        return Finish(layout, target, steps.Build(), gain, clock, notes);
    }

    private static PlanResult Finish(DiskLayout layout, PartitionInfo target, IReadOnlyList<PlanStep> steps, long gain,
        TimeProvider clock, IReadOnlyList<string> notes)
    {
        var expected = LayoutSimulator.Apply(layout, steps);
        var draft = new Plan(
            Id: Guid.NewGuid().ToString("N")[..12],
            CreatedAt: clock.GetLocalNow(),
            Operation: "extend",
            DiskNumber: layout.Disk.Number,
            TargetLetter: char.ToUpperInvariant(target.DriveLetter!.Value),
            LayoutFingerprint: LayoutFingerprint.Compute(layout),
            Before: layout,
            ExpectedAfter: expected,
            Steps: steps,
            Notes: notes,
            GainBytes: gain);

        var violations = SafetyGuard.CheckPlan(draft);
        if (violations.Count > 0)
            return PlanResult.Refuse(RefusalCode.SafetyViolation, "Kế hoạch vi phạm bất biến an toàn - đã hủy.", [.. violations]);
        return PlanResult.Ok(draft);
    }

    private static PlanResult RefuseTooSmall(long gain, ExtendRequest request) => PlanResult.Refuse(
        RefusalCode.NothingToGain,
        $"Chỉ tăng được {Units.Format(Math.Max(gain, 0))}, dưới ngưỡng {Units.Format(request.MinimumGain)} - không đáng thao tác trên hệ thống.");

    private static PlanResult RefuseNotAdjacent(DiskLayout layout, PartitionInfo target, Segment? next)
    {
        var blocker = next?.Partition is { } p
            ? $"ngay sau {target.DriveLetter}: là {p.DisplayName}{(p.DriveLetter is null ? "" : $" ({PartitionTypes.Describe(p.Kind)})")}"
            : $"{target.DriveLetter}: đã nằm cuối đĩa";
        var hints = LayoutAnalyzer.Analyze(layout).Select(a => a.Advice).ToArray();
        return PlanResult.Refuse(RefusalCode.NotAdjacent,
            $"Không có vùng trống liền kề {target.DriveLetter}: ({blocker}). Mở rộng qua phân vùng khác đòi hỏi DỜI dữ liệu - TsuDisk không làm thao tác rủi ro này.",
            hints);
    }

    private sealed class StepBuilder
    {
        private readonly List<PlanStep> _steps = [];

        public void Add(StepKind kind, string title, bool changesDisk, int? partitionNumber = null, long? offset = null,
            long? size = null, char? driveLetter = null) =>
            _steps.Add(new PlanStep(_steps.Count + 1, kind, title, changesDisk, partitionNumber, offset, size, driveLetter));

        public List<PlanStep> Build() => _steps;
    }
}
