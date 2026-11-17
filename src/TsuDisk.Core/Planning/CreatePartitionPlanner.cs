using TsuDisk.Core.Model;
using TsuDisk.Core.Safety;

namespace TsuDisk.Core.Planning;

/// <summary>Yeu cau tao + dinh dang mot phan vung du lieu trong vung trong.</summary>
public sealed record CreatePartitionRequest
{
    /// <summary>So thu tu (tu 1) cua vung trong dung duoc, theo offset; null = tu chon khi dia chi co dung mot vung.</summary>
    public int? ExtentIndex { get; init; }

    /// <summary>Kich thuoc (boi so 1 MiB); null = chiem het vung trong.</summary>
    public long? Size { get; init; }

    public string FileSystem { get; init; } = "NTFS";

    public string Label { get; init; } = "";

    /// <summary>Chu cai muon gan; null = tu chon chu cai trong dau tien tu D.</summary>
    public char? Letter { get; init; }

    public bool VirtualDiskOnly { get; init; }

    /// <summary>Nho hon muc nay thi khong dang tao phan vung du lieu (FAT32 can toi thieu ~33 MB).</summary>
    public const long MinSize = 64 * Units.MiB;

    /// <summary>Windows chi dinh dang FAT32 toi da 32 GB.</summary>
    public const long MaxFat32Size = 32 * Units.GiB;

    public static readonly IReadOnlyList<string> FileSystems = ["NTFS", "exFAT", "FAT32"];
}

/// <summary>Mot vung trong da can le 1 MiB hai dau, du lon de tao phan vung du lieu.</summary>
public sealed record UsableExtent(int Index, long Offset, long Size)
{
    public long End => Offset + Size;
}

/// <summary>
/// Lap ke hoach tao + dinh dang phan vung du lieu (giai doan 2, rui ro vua). Bat bien rieng:
/// <list type="bullet">
/// <item>Phan vung moi nam tron trong MOT vung trong cua bo cuc luc lap ke hoach, can le 1 MiB - khong cham phan vung co san.</item>
/// <item>Chi dinh dang phan vung vua tao trong cung ke hoach, va luc chay chi khi no con RAW.</item>
/// <item>Loi giua chung: xoa dung phan vung vua tao (kiem lai offset, kich thuoc, loai, khong co trong bo cuc cu).</item>
/// </list>
/// </summary>
public static class CreatePartitionPlanner
{
    public const string Operation = "create-partition";

    /// <summary>Vung trong dung duoc, danh so tu 1 theo offset (cung so voi tham so --vung).</summary>
    public static IReadOnlyList<UsableExtent> UsableExtents(DiskLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var result = new List<UsableExtent>();
        foreach (var free in layout.FreeExtents())
        {
            var start = Units.AlignUp(free.Offset, Units.MiB);
            var end = Units.AlignDown(free.End, Units.MiB);
            if (end - start >= CreatePartitionRequest.MinSize)
                result.Add(new UsableExtent(result.Count + 1, start, end - start));
        }
        return result;
    }

    public static PlanResult Plan(DiskLayout layout, IReadOnlySet<char> lettersInUse, CreatePartitionRequest request, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(lettersInUse);
        ArgumentNullException.ThrowIfNull(request);
        clock ??= TimeProvider.System;
        var disk = layout.Disk;

        if (disk.IsReadOnly || disk.IsOffline)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} đang chỉ-đọc hoặc offline.");
        if (request.VirtualDiskOnly && !disk.IsVirtual)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} không phải ổ ảo, mà chế độ chỉ-ổ-ảo đang bật.");
        if (disk.Style is not (PartitionStyle.Gpt or PartitionStyle.Mbr))
            return PlanResult.Refuse(RefusalCode.UnsupportedPartitionStyle, $"Đĩa {disk.Number} chưa khởi tạo (RAW).", "Khởi tạo đĩa (GPT) bằng Disk Management trước.");
        if (layout.Partitions.Any(p => p.Kind is PartitionKind.LdmMetadata or PartitionKind.LdmData or PartitionKind.StorageSpaces))
            return PlanResult.Refuse(RefusalCode.DynamicDisk, $"Đĩa {disk.Number} là ổ động (Dynamic) hoặc thuộc Storage Spaces.", "Dùng Disk Management cho đĩa này.");
        if (disk.Style == PartitionStyle.Mbr && (layout.Partitions.Count >= 4 || layout.Partitions.Any(p => p.Kind == PartitionKind.MbrExtended)))
        {
            return PlanResult.Refuse(RefusalCode.UnsupportedPartitionStyle,
                $"Đĩa MBR {disk.Number} đã đủ 4 phân vùng chính hoặc có phân vùng Extended - TsuDisk không tạo phân vùng logic.",
                "Chuyển đĩa sang GPT (mbr2gpt cho đĩa hệ thống) để có tới 128 phân vùng.");
        }

        var fileSystem = CreatePartitionRequest.FileSystems.FirstOrDefault(f => string.Equals(f, request.FileSystem, StringComparison.OrdinalIgnoreCase));
        if (fileSystem is null)
            return PlanResult.Refuse(RefusalCode.UnsupportedFileSystem, $"Hệ tệp {request.FileSystem} không được hỗ trợ - chọn NTFS, exFAT hoặc FAT32.");
        if (VolumeNaming.LabelProblem(request.Label, fileSystem) is { } labelProblem)
            return PlanResult.Refuse(RefusalCode.InvalidOption, labelProblem);

        var extents = UsableExtents(layout);
        if (extents.Count == 0)
            return PlanResult.Refuse(RefusalCode.NothingToGain, $"Đĩa {disk.Number} không có vùng trống nào từ {Units.Format(CreatePartitionRequest.MinSize)} trở lên.");
        var listing = extents.Select(e => $"--vung {e.Index}: {Units.Format(e.Size)} tại vị trí {Units.Format(e.Offset)}").ToArray();
        UsableExtent extent;
        if (request.ExtentIndex is { } wanted)
        {
            if (wanted < 1 || wanted > extents.Count)
                return PlanResult.Refuse(RefusalCode.InvalidOption, $"Không có vùng trống số {wanted} trên đĩa {disk.Number}.", listing);
            extent = extents[wanted - 1];
        }
        else if (extents.Count == 1)
        {
            extent = extents[0];
        }
        else
        {
            return PlanResult.Refuse(RefusalCode.InvalidOption, $"Đĩa {disk.Number} có {extents.Count} vùng trống - chọn một bằng --vung.", listing);
        }

        var size = request.Size ?? extent.Size;
        if (size % Units.MiB != 0 || size < CreatePartitionRequest.MinSize)
            return PlanResult.Refuse(RefusalCode.InvalidOption, $"Kích thước phải là bội số 1 MB và từ {Units.Format(CreatePartitionRequest.MinSize)} trở lên.");
        if (size > extent.Size)
            return PlanResult.Refuse(RefusalCode.InvalidOption, $"Vùng trống chỉ có {Units.Format(extent.Size)}, yêu cầu {Units.Format(size)}.");
        if (fileSystem == "FAT32" && size > CreatePartitionRequest.MaxFat32Size)
            return PlanResult.Refuse(RefusalCode.InvalidOption, "Windows chỉ định dạng FAT32 tới 32 GB.", "Dùng exFAT cho USB/thẻ nhớ lớn, NTFS cho ổ trong máy.");

        char letter;
        if (request.Letter is { } requested)
        {
            letter = char.ToUpperInvariant(requested);
            if (!VolumeNaming.IsAssignableLetter(letter))
                return PlanResult.Refuse(RefusalCode.InvalidOption, $"Chữ cái phải trong khoảng C-Z (nhận: {letter}).");
            if (lettersInUse.Contains(letter) || layout.FindByLetter(letter) is not null)
                return PlanResult.Refuse(RefusalCode.LetterInUse, $"Chữ cái {letter}: đang được dùng (ổ đĩa, ổ mạng hoặc subst).");
        }
        else
        {
            var free = Enumerable.Range('D', 'Z' - 'D' + 1).Select(c => (char)c)
                .Where(c => !lettersInUse.Contains(c) && layout.FindByLetter(c) is null).ToList();
            if (free.Count == 0)
                return PlanResult.Refuse(RefusalCode.LetterInUse, "Không còn chữ cái nào trống từ D đến Z.");
            letter = free[0];
        }

        var steps = new List<PlanStep>
        {
            new(1, StepKind.CheckHealth, "Kiểm tra sức khỏe ổ đĩa (S.M.A.R.T., chỉ đọc)", false),
            new(2, StepKind.Backup, "Lưu ảnh bảng phân vùng (và sector GPT thô) vào thư mục nhật ký", false),
            new(3, StepKind.CreatePartition, $"Tạo phân vùng dữ liệu {Units.Format(size)} trong vùng trống tại vị trí {Units.Format(extent.Offset)}", true,
                Offset: extent.Offset, Size: size),
            new(4, StepKind.FormatVolume, $"Định dạng nhanh {fileSystem}{(request.Label.Length > 0 ? $" nhãn \"{request.Label}\"" : "")}, gán {letter}:", true,
                Offset: extent.Offset, Size: size, NewDriveLetter: letter, NewLabel: request.Label, FileSystem: fileSystem),
            new(5, StepKind.VerifyLayout, "Đọc lại bảng phân vùng, đối chiếu với kết quả dự kiến", false),
        };
        var notes = new List<string>
        {
            $"Chỉ dùng vùng trống: mọi phân vùng có sẵn giữ nguyên vị trí, kích thước, dữ liệu. Còn trống sau đó: {Units.Format(extent.Size - size)}.",
        };
        if (fileSystem == "FAT32")
            notes.Add("FAT32 không chứa được tệp lớn hơn 4 GB.");
        notes.Add("Windows xóa trắng phần đầu vùng trống khi tạo phân vùng: nếu đây là chỗ của một ổ vừa xóa nhầm, cứu nó bằng TestDisk TRƯỚC khi tạo.");

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
            GainBytes: size);
        var violations = SafetyGuard.CheckPlan(plan);
        return violations.Count > 0
            ? PlanResult.Refuse(RefusalCode.SafetyViolation, "Kế hoạch vi phạm bất biến an toàn - đã hủy.", [.. violations])
            : PlanResult.Ok(plan);
    }
}
