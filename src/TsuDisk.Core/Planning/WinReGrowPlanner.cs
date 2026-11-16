using TsuDisk.Core.Model;
using TsuDisk.Core.Safety;

namespace TsuDisk.Core.Planning;

/// <summary>Yeu cau "noi rong phan vung Recovery cho WinRE" (loi cap nhat 0x80070643 - Microsoft KB5034441).</summary>
public sealed record WinReGrowRequest
{
    /// <summary>Kich thuoc Recovery mong muon; null = tu tinh de con it nhat <see cref="RequiredFree"/> trong.</summary>
    public long? TargetSize { get; init; }

    public RecoveryPolicy RecoveryPolicy { get; init; } = RecoveryPolicy.RelocateActiveWinReOnly;

    public bool VirtualDiskOnly { get; init; }

    /// <summary>KB5034441: ban cap nhat WinRE can 250 MB trong trong phan vung Recovery.</summary>
    public const long RequiredFree = 250 * Units.MiB;

    /// <summary>Tran thu nho o dung truoc: lon hon la bat thuong voi muc dich nay, tu choi.</summary>
    public const long MaxShrink = 4 * Units.GiB;

    /// <summary>O bi thu nho phai con trong it nhat min(2 GiB, 25% dung luong o) SAU khi thu nho.</summary>
    public static long MinFreeAfterShrink(long partitionSize) => Math.Min(2 * Units.GiB, partitionSize / 4);
}

/// <summary>
/// Lap ke hoach noi rong Recovery theo quy trinh KB5034441, hai mau:
/// <list type="number">
/// <item>[Recovery][Vung trong du lon] - xoa va tao lai Recovery lon hon tai CHO CU, khong thu nho o nao.</item>
/// <item>[O du lieu][Recovery] - thu nho o dung truoc dung phan can, tao lai Recovery lon hon ngay sau o do.</item>
/// </list>
/// Thu nho chi duoc phep o thao tac nay, toi da <see cref="WinReGrowRequest.MaxShrink"/>, va o phai con du trong.
/// </summary>
public static class WinReGrowPlanner
{
    public const string Operation = "grow-winre";

    public static PlanResult Plan(DiskLayout layout, WinReStatus winRe, WinReGrowRequest request, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(winRe);
        ArgumentNullException.ThrowIfNull(request);
        clock ??= TimeProvider.System;
        var disk = layout.Disk;

        if (disk.IsReadOnly || disk.IsOffline || request.VirtualDiskOnly && !disk.IsVirtual)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} không ghi được (chỉ-đọc, offline, hoặc không phải ổ ảo ở chế độ chỉ-ổ-ảo).");
        if (disk.Style != PartitionStyle.Gpt)
            return PlanResult.Refuse(RefusalCode.UnsupportedPartitionStyle, "Nới rộng WinRE hiện chỉ hỗ trợ đĩa GPT.");

        var recovery = layout.Partitions.FirstOrDefault(winRe.LivesOn);
        if (recovery is null && request.RecoveryPolicy == RecoveryPolicy.RecreateEmpty)
            recovery = layout.Ordered.LastOrDefault(p => p.Kind == PartitionKind.Recovery);
        if (recovery is null || recovery.Kind != PartitionKind.Recovery)
        {
            return PlanResult.Refuse(RefusalCode.RecoveryNotWinRe,
                winRe.Enabled
                    ? $"WinRE đang ở đĩa {winRe.DiskNumber} phân vùng {winRe.PartitionNumber}, không phải một phân vùng Recovery trên đĩa {disk.Number}."
                    : "WinRE đang tắt - không có phân vùng Recovery nào để nới rộng.",
                "Bật WinRE trước: reagentc /enable.");
        }
        var winReHere = winRe.LivesOn(recovery);

        long target;
        if (request.TargetSize is { } wanted)
        {
            target = Units.AlignUp(wanted, Units.MiB);
        }
        else
        {
            if (recovery.FreeBytes is { } free && free >= WinReGrowRequest.RequiredFree)
            {
                return PlanResult.Refuse(RefusalCode.NothingToGain,
                    $"Recovery còn trống {Units.Format(free)} - đã đủ {Units.Format(WinReGrowRequest.RequiredFree)} mà bản cập nhật WinRE (KB5034441) cần.");
            }
            var missing = WinReGrowRequest.RequiredFree - (recovery.FreeBytes ?? 0);
            target = Units.AlignUp(recovery.Size + Math.Max(missing, 0) + 16 * Units.MiB, Units.MiB);
        }
        if (target <= recovery.Size)
            return PlanResult.Refuse(RefusalCode.NothingToGain, $"Recovery đã {Units.Format(recovery.Size)}, không nhỏ hơn mức yêu cầu {Units.Format(target)}.");
        if (target > ExtendRequest.MaxRecoverySize)
            return PlanResult.Refuse(RefusalCode.InvalidOption, $"Recovery tối đa {Units.Format(ExtendRequest.MaxRecoverySize)}.");
        var delta = target - recovery.Size;

        var steps = new List<PlanStep>();
        void Add(StepKind kind, string title, bool changes, int? number = null, long? offset = null, long? size = null, char? letter = null) =>
            steps.Add(new PlanStep(steps.Count + 1, kind, title, changes, number, offset, size, letter));

        var after = layout.SegmentAfter(recovery);
        var notes = new List<string> { "Quy trình Microsoft KB5034441: phân vùng Recovery cần đủ chỗ trống để Windows Update cập nhật WinRE (lỗi 0x80070643)." };
        Add(StepKind.CheckHealth, "Kiểm tra sức khỏe ổ đĩa (S.M.A.R.T., chỉ đọc)", false);

        if (after is { IsFree: true } free2 && Units.AlignDown(free2.End, Units.MiB) - recovery.Offset >= target)
        {
            Add(StepKind.Backup, "Lưu ảnh bảng phân vùng, BCD và ảnh WinRE vào thư mục nhật ký", false);
            if (winReHere) Add(StepKind.DisableWinRe, "Tắt WinRE (Windows chép Winre.wim về ổ hệ thống)", false);
            Add(StepKind.DeletePartition, $"Xóa {recovery.DisplayName} ({Units.Format(recovery.Size)})", true, recovery.PartitionNumber, recovery.Offset, recovery.Size);
            Add(StepKind.CreateRecovery, $"Tạo lại Recovery {Units.Format(target)} tại chỗ cũ (dùng thêm vùng trống phía sau)", true, null, recovery.Offset, target);
            notes.Add($"Có {Units.Format(free2.Size)} trống ngay sau Recovery - không cần thu nhỏ ổ nào.");
        }
        else
        {
            var segments = layout.Segments();
            var index = segments.ToList().FindIndex(s => s.Partition?.Offset == recovery.Offset);
            var before = index > 0 ? segments[index - 1].Partition : null;
            if (before is not { Kind: PartitionKind.BasicData, FileSystem: "NTFS", DriveLetter: not null })
            {
                return PlanResult.Refuse(RefusalCode.NotAdjacent,
                    "Không có vùng trống sau Recovery và phân vùng ngay trước Recovery không phải ổ NTFS thu nhỏ được.");
            }
            if (delta > WinReGrowRequest.MaxShrink)
                return PlanResult.Refuse(RefusalCode.InvalidOption, $"Cần thu nhỏ {before.DriveLetter}: {Units.Format(delta)}, vượt trần {Units.Format(WinReGrowRequest.MaxShrink)}.");
            var minFree = WinReGrowRequest.MinFreeAfterShrink(before.Size);
            if (before.FreeBytes is { } bf && bf - delta < minFree)
            {
                return PlanResult.Refuse(RefusalCode.InvalidOption,
                    $"{before.DriveLetter}: chỉ còn trống {Units.Format(bf)}; sau khi nhường {Units.Format(delta)} sẽ dưới {Units.Format(minFree)}. Dọn bớt dữ liệu trước.");
            }
            var newSize = before.Size - delta;
            var newOffset = before.Offset + newSize;
            Add(StepKind.ScanVolume, $"Quét hệ thống tệp {before.DriveLetter}: (chỉ đọc)", false, letter: before.DriveLetter);
            Add(StepKind.Backup, "Lưu ảnh bảng phân vùng, BCD và ảnh WinRE vào thư mục nhật ký", false);
            if (winReHere) Add(StepKind.DisableWinRe, "Tắt WinRE (Windows chép Winre.wim về ổ hệ thống)", false);
            Add(StepKind.ShrinkPartition, $"Thu nhỏ {before.DriveLetter}: {Units.Format(delta)} (từ {Units.Format(before.Size)} xuống {Units.Format(newSize)})", true,
                before.PartitionNumber, before.Offset, newSize, before.DriveLetter);
            Add(StepKind.DeletePartition, $"Xóa {recovery.DisplayName} ({Units.Format(recovery.Size)})", true, recovery.PartitionNumber, recovery.Offset, recovery.Size);
            Add(StepKind.CreateRecovery, $"Tạo Recovery mới {Units.Format(target)} ngay sau {before.DriveLetter}:", true, null, newOffset, target);
            notes.Add($"Không có vùng trống sau Recovery - nhường {Units.Format(delta)} từ cuối {before.DriveLetter}: (chỉ phần trống, không dời dữ liệu đã có vị trí).");
        }
        if (winReHere) Add(StepKind.EnableWinRe, "Bật lại WinRE vào phân vùng Recovery mới", false);
        Add(StepKind.VerifyLayout, "Đọc lại bảng phân vùng, đối chiếu với kết quả dự kiến", false);

        var plan = new Plan(
            Id: Guid.NewGuid().ToString("N")[..12],
            CreatedAt: clock.GetLocalNow(),
            Operation: Operation,
            DiskNumber: disk.Number,
            TargetLetter: steps.FirstOrDefault(s => s.Kind == StepKind.ShrinkPartition)?.DriveLetter ?? 'R',
            LayoutFingerprint: LayoutFingerprint.Compute(layout),
            Before: layout,
            ExpectedAfter: LayoutSimulator.Apply(layout, steps),
            Steps: steps,
            Notes: notes,
            GainBytes: delta);
        var violations = SafetyGuard.CheckPlan(plan);
        return violations.Count > 0
            ? PlanResult.Refuse(RefusalCode.SafetyViolation, "Kế hoạch vi phạm bất biến an toàn - đã hủy.", [.. violations])
            : PlanResult.Ok(plan);
    }
}
