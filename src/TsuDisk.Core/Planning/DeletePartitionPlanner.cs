using System.Globalization;
using TsuDisk.Core.Model;
using TsuDisk.Core.Safety;

namespace TsuDisk.Core.Planning;

/// <summary>Yeu cau xoa mot o du lieu, goi bang chu cai.</summary>
public sealed record DeletePartitionRequest(char Letter)
{
    public bool VirtualDiskOnly { get; init; }
}

/// <summary>
/// Lap ke hoach xoa phan vung du lieu (ADR-0005). Bat bien rieng:
/// <list type="bullet">
/// <item>Chi o du lieu co ban co chu cai, khong phai he thong/khoi dong; moi ke hoach dung MOT phan vung.</item>
/// <item>Xac nhan bang chuoi "X: nhan" (<see cref="ConfirmationText"/>) - khong phai "co/khong".</item>
/// <item>Luc chay: kiem lai chu cai + nhan + hinh hoc, khoa doc quyen volume (o dang dung thi dung).</item>
/// </list>
/// </summary>
public static class DeletePartitionPlanner
{
    public const string Operation = "delete-partition";

    /// <summary>Chuoi nguoi dung phai go lai de xac nhan: "E: Dữ liệu", o khong nhan thi "E:".</summary>
    public static string ConfirmationText(PartitionInfo target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var letter = target.DriveLetter ?? throw new ArgumentException("Phân vùng không có chữ cái.", nameof(target));
        return string.IsNullOrEmpty(target.Label) ? $"{letter}:" : $"{letter}: {target.Label}";
    }

    /// <summary>So khop xac nhan: bo khoang trang dau/cuoi, chu cai khong phan biet hoa thuong, nhan phai dung tung ky tu.</summary>
    public static bool ConfirmationMatches(PartitionInfo target, string? typed)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (typed is null)
            return false;
        var expected = ConfirmationText(target);
        var t = typed.Trim().Normalize();
        return t.Length == expected.Length
            && char.ToUpperInvariant(t[0]) == expected[0]
            && string.Equals(t[1..], expected[1..].Normalize(), StringComparison.Ordinal);
    }

    /// <summary>Lenh diskpart tao lai dung phan vung (offset tinh bang KB, kich thuoc bang MB) - in trong ghi chu ke hoach.</summary>
    public static IReadOnlyList<string> ManualRestoreCommands(DiskLayout layout, PartitionInfo target)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(target);
        var inv = CultureInfo.InvariantCulture;
        var lines = new List<string>
        {
            string.Create(inv, $"select disk {layout.Disk.Number}"),
            string.Create(inv, $"create partition primary offset={target.Offset / 1024} size={target.Size / Units.MiB}"),
        };
        if (target.GptType is { } gpt && gpt != PartitionTypes.BasicData)
            lines.Add(string.Create(inv, $"set id={gpt:D}"));
        else if (target.MbrType is { } mbr && mbr != PartitionTypes.MbrIfs)
            lines.Add(string.Create(inv, $"set id={mbr:X2}"));
        lines.Add($"assign letter={target.DriveLetter}");
        return lines;
    }

    public static PlanResult Plan(DiskLayout layout, DeletePartitionRequest request, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(request);
        clock ??= TimeProvider.System;
        var disk = layout.Disk;
        var letter = char.ToUpperInvariant(request.Letter);

        var target = layout.FindByLetter(letter);
        if (target is null)
            return PlanResult.Refuse(RefusalCode.TargetNotFound, $"Không tìm thấy ổ {letter}: trên đĩa {disk.Number}.");
        if (disk.IsReadOnly || disk.IsOffline)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} đang chỉ-đọc hoặc offline.");
        if (request.VirtualDiskOnly && !disk.IsVirtual)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} không phải ổ ảo, mà chế độ chỉ-ổ-ảo đang bật.");
        if (layout.Partitions.Any(p => p.Kind is PartitionKind.LdmMetadata or PartitionKind.LdmData or PartitionKind.StorageSpaces))
            return PlanResult.Refuse(RefusalCode.DynamicDisk, $"Đĩa {disk.Number} là ổ động (Dynamic) hoặc thuộc Storage Spaces.", "Dùng Disk Management cho đĩa này.");
        if (target.IsSystem || target.IsBoot)
        {
            return PlanResult.Refuse(RefusalCode.ProtectedTarget, $"{target.DisplayName} là ổ hệ thống/khởi động - không bao giờ xóa.",
                "Muốn cài lại Windows thì dùng bộ cài Windows, không dùng TsuDisk.");
        }
        if (target.Kind != PartitionKind.BasicData)
            return PlanResult.Refuse(RefusalCode.ProtectedTarget, $"{target.DisplayName} không phải ổ dữ liệu cơ bản ({PartitionTypes.Describe(target.Kind)}).");

        var steps = new List<PlanStep>
        {
            new(1, StepKind.CheckHealth, "Kiểm tra sức khỏe ổ đĩa (S.M.A.R.T., chỉ đọc)", false),
            new(2, StepKind.Backup, "Lưu ảnh bảng phân vùng (và sector GPT thô) vào thư mục nhật ký", false),
            new(3, StepKind.DeleteDataPartition, $"Xóa {target.DisplayName} ({Units.Format(target.Size)}) - ổ phải không còn chương trình nào mở", true,
                PartitionNumber: target.PartitionNumber, Offset: target.Offset, Size: target.Size, DriveLetter: letter, NewLabel: target.Label ?? ""),
            new(4, StepKind.VerifyLayout, "Đọc lại bảng phân vùng, đối chiếu với kết quả dự kiến", false),
        };
        var notes = new List<string>
        {
            $"MỌI dữ liệu trên {target.DisplayName} sẽ không còn truy cập được. Sao lưu những gì cần giữ trước.",
            $"Vùng trống sinh ra: {Units.Format(target.Size)} - dùng ngay bằng `tsudisk ke-hoach <ổ>` hoặc `tsudisk tao-phan-vung --dia {disk.Number}`.",
        };
        notes.Add(target.Offset % 1024 == 0 && target.Size % Units.MiB == 0
            ? "Đổi ý khi CHƯA ghi gì vào vùng trống: diskpart (Admin) với các lệnh sau sẽ đưa ổ và dữ liệu trở lại: "
                + string.Join(" ; ", ManualRestoreCommands(layout, target))
            : "Đổi ý khi CHƯA ghi gì vào vùng trống: ổ này không căn lề 1 MiB, khôi phục bằng TestDisk (quét phân vùng mất) thay vì diskpart.");

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
            GainBytes: target.Size);
        var violations = SafetyGuard.CheckPlan(plan);
        return violations.Count > 0
            ? PlanResult.Refuse(RefusalCode.SafetyViolation, "Kế hoạch vi phạm bất biến an toàn - đã hủy.", [.. violations])
            : PlanResult.Ok(plan);
    }
}
