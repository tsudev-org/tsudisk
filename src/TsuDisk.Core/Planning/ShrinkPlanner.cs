using TsuDisk.Core.Model;
using TsuDisk.Core.Safety;

namespace TsuDisk.Core.Planning;

/// <summary>Yeu cau thu nho mot o NTFS, nhuong vung trong ra phia sau o (ADR-0004).</summary>
/// <param name="TargetLetter">O can thu nho.</param>
/// <param name="Reduce">So byte muon nhuong (lam tron len de diem cuoi moi can le 1 MiB).</param>
public sealed record ShrinkRequest(char TargetLetter, long Reduce)
{
    public bool VirtualDiskOnly { get; init; }

    /// <summary>%SystemDrive% - o Windows chiu nguong cho trong cao hon.</summary>
    public char? SystemDrive { get; init; }

    /// <summary>SizeMin cua GetSupportedSize do Windows bao luc lap ke hoach; null = khong doc duoc (PlanRunner van kiem lai).</summary>
    public long? WindowsMinSize { get; init; }

    /// <summary>Muc nhuong nho nhat co nghia (bang co toi thieu cua phan vung moi).</summary>
    public const long MinReduce = CreatePartitionRequest.MinSize;

    /// <summary>O Windows/khoi dong phai con it nhat 10 GiB trong (Windows Update).</summary>
    public const long MinFreeSystem = 10 * Units.GiB;

    /// <summary>Cho trong toi thieu sau khi thu nho (ADR-0004 muc 3).</summary>
    public static long MinFreeAfter(bool isSystem, long newSize) =>
        isSystem ? MinFreeSystem : WinReGrowRequest.MinFreeAfterShrink(newSize);
}

/// <summary>Lap ke hoach thu nho o NTFS tong quat (ADR-0004).</summary>
public static class ShrinkPlanner
{
    public const string Operation = "shrink-volume";

    public static PlanResult Plan(DiskLayout layout, ShrinkRequest request, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(request);
        clock ??= TimeProvider.System;
        var disk = layout.Disk;
        var letter = char.ToUpperInvariant(request.TargetLetter);

        var target = layout.FindByLetter(letter);
        if (target is null)
            return PlanResult.Refuse(RefusalCode.TargetNotFound, $"Không tìm thấy ổ {letter}: trên đĩa {disk.Number}.");
        if (disk.IsReadOnly || disk.IsOffline)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} đang chỉ-đọc hoặc offline.");
        if (request.VirtualDiskOnly && !disk.IsVirtual)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} không phải ổ ảo, mà chế độ chỉ-ổ-ảo đang bật.");
        if (layout.Partitions.Any(p => p.Kind is PartitionKind.LdmMetadata or PartitionKind.LdmData or PartitionKind.StorageSpaces))
            return PlanResult.Refuse(RefusalCode.DynamicDisk, $"Đĩa {disk.Number} là ổ động (Dynamic) hoặc thuộc Storage Spaces.", "Dùng Disk Management cho đĩa này.");
        if (target.Kind != PartitionKind.BasicData)
            return PlanResult.Refuse(RefusalCode.ProtectedTarget, $"{target.DisplayName} là {PartitionTypes.Describe(target.Kind)}, không phải ổ dữ liệu - không thu nhỏ.");
        if (target.FileSystem != "NTFS")
        {
            return PlanResult.Refuse(RefusalCode.UnsupportedFileSystem,
                $"{target.DisplayName} dùng {target.FileSystem ?? "hệ thống tệp không rõ"}; Windows chỉ thu nhỏ trực tuyến NTFS.",
                "ReFS/FAT32/exFAT: sao lưu dữ liệu, xóa và tạo lại phân vùng với kích thước mới.");
        }
        if (request.Reduce < ShrinkRequest.MinReduce)
            return PlanResult.Refuse(RefusalCode.InvalidOption, $"Mức thu nhỏ tối thiểu {Units.Format(ShrinkRequest.MinReduce)}.");
        if (target.FreeBytes is not { } free)
            return PlanResult.Refuse(RefusalCode.InvalidOption, $"Không đọc được dung lượng trống của {letter}: - không thể đánh giá an toàn, từ chối.");

        var newEnd = Units.AlignDown(target.End - request.Reduce, Units.MiB);
        var newSize = newEnd - target.Offset;
        var gain = target.Size - newSize;
        var isSystem = target.IsSystem || target.IsBoot || request.SystemDrive is { } sys && char.ToUpperInvariant(sys) == letter;
        var minFree = ShrinkRequest.MinFreeAfter(isSystem, newSize);
        if (newSize <= 0 || free - gain < minFree)
        {
            var maxGain = Math.Max(0, Units.AlignDown(free - minFree, Units.MiB));
            return PlanResult.Refuse(RefusalCode.InvalidOption,
                $"{letter}: còn trống {Units.Format(free)}; nhường {Units.Format(gain)} sẽ còn dưới {Units.Format(minFree)}" +
                $"{(isSystem ? " (ổ Windows cần chỗ cho Windows Update)" : "")}.",
                maxGain >= ShrinkRequest.MinReduce ? $"Tối đa nhường được khoảng {Units.Format(maxGain)}." : "Dọn bớt dữ liệu trên ổ trước.");
        }
        if (request.WindowsMinSize is { } windowsMin && windowsMin > newSize)
        {
            return PlanResult.Refuse(RefusalCode.InvalidOption,
                $"Windows chỉ cho thu nhỏ {letter}: tới {Units.Format(windowsMin)} (có tệp không dời được nằm gần cuối ổ), kế hoạch cần {Units.Format(newSize)}.",
                $"Tối đa nhường được {Units.Format(Math.Max(0, target.End - Units.AlignUp(target.Offset + windowsMin, Units.MiB)))} lúc này.",
                "Tạm tắt ngủ đông (powercfg /h off), tạm tắt pagefile trên ổ này, xóa bớt điểm khôi phục, rồi lập lại kế hoạch.");
        }

        var steps = new List<PlanStep>
        {
            new(1, StepKind.CheckHealth, "Kiểm tra sức khỏe ổ đĩa (S.M.A.R.T., chỉ đọc)", false),
            new(2, StepKind.ScanVolume, $"Quét hệ thống tệp {letter}: (chỉ đọc)", false, DriveLetter: letter),
            new(3, StepKind.Backup, "Lưu ảnh bảng phân vùng, BCD và sector GPT thô vào thư mục nhật ký", false),
            new(4, StepKind.ShrinkPartition, $"Thu nhỏ {letter}: {Units.Format(gain)} (từ {Units.Format(target.Size)} xuống {Units.Format(newSize)})", true,
                target.PartitionNumber, target.Offset, newSize, letter),
            new(5, StepKind.VerifyLayout, "Đọc lại bảng phân vùng, đối chiếu với kết quả dự kiến", false),
        };
        var notes = new List<string>
        {
            "Windows tự dời dữ liệu ở cuối ổ (NTFS trực tuyến, có giao dịch) - TsuDisk không tự di chuyển dữ liệu.",
            $"Sau khi thu nhỏ, {letter}: còn trống khoảng {Units.Format(free - gain)}; vùng trống {Units.Format(gain)} nằm ngay sau {letter}: - dùng `tsudisk tao-phan-vung` để tạo ổ mới.",
        };
        var plan = new Plan(
            Id: Guid.NewGuid().ToString("N")[..12],
            CreatedAt: clock.GetLocalNow(),
            Operation: Operation,
            DiskNumber: disk.Number,
            TargetLetter: letter,
            LayoutFingerprint: LayoutFingerprint.Compute(layout),
            Before: layout,
            ExpectedAfter: LayoutSimulator.Apply(layout, steps),
            Steps: steps,
            Notes: notes,
            GainBytes: gain);
        var violations = SafetyGuard.CheckPlan(plan);
        return violations.Count > 0
            ? PlanResult.Refuse(RefusalCode.SafetyViolation, "Kế hoạch vi phạm bất biến an toàn - đã hủy.", [.. violations])
            : PlanResult.Ok(plan);
    }
}
