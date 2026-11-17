using System.Buffers;
using TsuDisk.Core.Model;
using TsuDisk.Core.Safety;

namespace TsuDisk.Core.Planning;

/// <summary>Yeu cau doi chu cai va/hoac nhan cua mot o du lieu.</summary>
/// <param name="TargetLetter">O can doi (chu cai hien tai).</param>
/// <param name="NewLetter">Chu cai moi; null = giu nguyen.</param>
/// <param name="NewLabel">Nhan moi; null = giu nguyen, chuoi rong = xoa nhan.</param>
public sealed record RenameRequest(char TargetLetter, char? NewLetter = null, string? NewLabel = null)
{
    public bool VirtualDiskOnly { get; init; }

    /// <summary>Chu cai o chua thu muc Windows (%SystemDrive%); khong bao gio doi chu cai o nay.</summary>
    public char? SystemDrive { get; init; }
}

/// <summary>Luat chu cai va nhan o hop le (dung chung cho RenamePlanner va SafetyGuard).</summary>
public static class VolumeNaming
{
    /// <summary>NTFS/ReFS/exFAT: toi da 32 ky tu; FAT/FAT32: toi da 11 ky tu (tai lieu lenh `label` cua Windows).</summary>
    public static int MaxLabelLength(string? fileSystem) => fileSystem is "FAT" or "FAT32" ? 11 : 32;

    /// <summary>Ky tu FAT cam trong nhan (tai lieu lenh `label`).</summary>
    private const string FatForbidden = "*?/\\|.,;:+=[]<>\"";

    private static readonly SearchValues<char> FatForbiddenValues = SearchValues.Create(FatForbidden);

    /// <summary>A va B danh cho o mem theo quy uoc Windows; TsuDisk chi gan C-Z.</summary>
    public static bool IsAssignableLetter(char letter) => letter is >= 'C' and <= 'Z';

    /// <summary>Tra ve ly do nhan khong hop le, hoac null neu hop le. Chuoi rong (xoa nhan) luon hop le.</summary>
    public static string? LabelProblem(string label, string? fileSystem)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (label.Length == 0)
            return null;
        var max = MaxLabelLength(fileSystem);
        if (label.Length > max)
            return $"Nhãn {fileSystem ?? ""} tối đa {max} ký tự (đang có {label.Length}).";
        if (label.Any(char.IsControl))
            return "Nhãn không được chứa ký tự điều khiển (Tab, xuống dòng...).";
        if (label != label.Trim())
            return "Nhãn không được bắt đầu hoặc kết thúc bằng khoảng trắng.";
        if (fileSystem is "FAT" or "FAT32" && label.AsSpan().ContainsAny(FatForbiddenValues))
            return $"Nhãn FAT32 không được chứa các ký tự {FatForbidden}.";
        if (fileSystem is "FAT" or "FAT32" && label.Any(c => c > 127))
            return "Nhãn FAT32 chỉ nhận ký tự ASCII (không dấu) - dùng NTFS/exFAT nếu cần tiếng Việt có dấu.";
        return null;
    }
}

/// <summary>
/// Lap ke hoach doi chu cai va/hoac nhan o (giai doan 2, rui ro thap: khong doi hinh hoc phan vung). Bat bien rieng:
/// <list type="bullet">
/// <item>Chi o du lieu co he thong tep; khong doi chu cai o he thong/khoi dong/o chua Windows.</item>
/// <item>Chu cai moi trong khoang C-Z va chua duoc dung o bat ky dau (moi dia, o mang, subst).</item>
/// <item>Nhan dung luat cua he thong tep (do dai, ky tu cam).</item>
/// </list>
/// </summary>
public static class RenamePlanner
{
    public const string Operation = "rename-volume";

    public static PlanResult Plan(DiskLayout layout, IReadOnlySet<char> lettersInUse, RenameRequest request, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(lettersInUse);
        ArgumentNullException.ThrowIfNull(request);
        clock ??= TimeProvider.System;
        var disk = layout.Disk;
        var current = char.ToUpperInvariant(request.TargetLetter);
        var newLetter = request.NewLetter is { } nl ? char.ToUpperInvariant(nl) : (char?)null;

        if (newLetter is null && request.NewLabel is null)
            return PlanResult.Refuse(RefusalCode.InvalidOption, "Cần ít nhất một thay đổi: chữ cái mới (--chu-cai) hoặc nhãn mới (--nhan).");

        var target = layout.FindByLetter(current);
        if (target is null)
            return PlanResult.Refuse(RefusalCode.TargetNotFound, $"Không tìm thấy ổ {current}: trên đĩa {disk.Number}.");
        if (disk.IsReadOnly || disk.IsOffline)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} đang chỉ-đọc hoặc offline.");
        if (request.VirtualDiskOnly && !disk.IsVirtual)
            return PlanResult.Refuse(RefusalCode.DiskNotWritable, $"Đĩa {disk.Number} không phải ổ ảo, mà chế độ chỉ-ổ-ảo đang bật.");
        if (layout.Partitions.Any(p => p.Kind is PartitionKind.LdmMetadata or PartitionKind.LdmData or PartitionKind.StorageSpaces))
        {
            return PlanResult.Refuse(RefusalCode.DynamicDisk,
                $"Đĩa {disk.Number} là ổ động (Dynamic) hoặc thuộc Storage Spaces.", "Dùng Disk Management cho đĩa này.");
        }
        if (target.Kind != PartitionKind.BasicData)
        {
            return PlanResult.Refuse(RefusalCode.ProtectedTarget,
                $"{target.DisplayName} là {PartitionTypes.Describe(target.Kind)}, không phải ổ dữ liệu - không đổi.");
        }
        if (target.FileSystem is null)
        {
            return PlanResult.Refuse(RefusalCode.UnsupportedFileSystem,
                $"{target.DisplayName} chưa có hệ thống tệp Windows đọc được - không đổi chữ cái/nhãn.");
        }

        if (newLetter is { } letter)
        {
            if (IsSystemTarget(target, request))
            {
                return PlanResult.Refuse(RefusalCode.ProtectedTarget,
                    $"{current}: là ổ hệ thống/khởi động - đổi chữ cái sẽ làm Windows không khởi động được.",
                    "Chỉ đổi được chữ cái của ổ dữ liệu. Nhãn của ổ hệ thống vẫn đổi được (--nhan).");
            }
            if (!VolumeNaming.IsAssignableLetter(letter))
                return PlanResult.Refuse(RefusalCode.InvalidOption, $"Chữ cái mới phải trong khoảng C-Z (nhận: {letter}).");
            if (letter != current && (lettersInUse.Contains(letter) || layout.FindByLetter(letter) is not null))
            {
                var free = Enumerable.Range('D', 'Z' - 'D' + 1).Select(c => (char)c)
                    .Where(c => !lettersInUse.Contains(c) && layout.FindByLetter(c) is null).Take(6);
                return PlanResult.Refuse(RefusalCode.LetterInUse, $"Chữ cái {letter}: đang được dùng (ổ đĩa, ổ mạng hoặc subst).",
                    $"Chữ cái còn trống: {string.Join(", ", free.Select(c => $"{c}:"))}");
            }
        }
        if (request.NewLabel is { } label && VolumeNaming.LabelProblem(label, target.FileSystem) is { } problem)
            return PlanResult.Refuse(RefusalCode.InvalidOption, problem);

        var changeLetter = newLetter is { } nl2 && nl2 != current;
        var changeLabel = request.NewLabel is { } l2 && !string.Equals(l2, target.Label ?? "", StringComparison.Ordinal);
        if (!changeLetter && !changeLabel)
            return PlanResult.Refuse(RefusalCode.NothingToGain, $"{current}: đã mang đúng chữ cái và nhãn yêu cầu - không có gì để đổi.");

        var steps = new List<PlanStep>();
        void Add(StepKind kind, string title, bool changes, char? next = null, string? nextLabel = null) =>
            steps.Add(new PlanStep(steps.Count + 1, kind, title, changes,
                changes ? target.PartitionNumber : null, changes ? target.Offset : null, changes ? target.Size : null,
                current, next, nextLabel));

        var notes = new List<string> { "Chỉ đổi tên gọi của ổ - không đổi vị trí, kích thước hay dữ liệu của bất kỳ phân vùng nào." };
        Add(StepKind.CheckHealth, "Kiểm tra sức khỏe ổ đĩa (S.M.A.R.T., chỉ đọc)", false);
        // Nhan doi truoc chu cai: nhan gan theo phan vung, khong phu thuoc chu cai - neu buoc chu cai loi, nhan tu hoan tac.
        if (changeLabel)
        {
            var text = request.NewLabel!.Length == 0 ? $"Xóa nhãn {current}: (đang là \"{target.Label}\")"
                : $"Đổi nhãn {current}: từ \"{target.Label ?? ""}\" thành \"{request.NewLabel}\"";
            Add(StepKind.SetVolumeLabel, text, true, nextLabel: request.NewLabel);
        }
        if (changeLetter)
        {
            Add(StepKind.SetDriveLetter, $"Đổi chữ cái {current}: thành {newLetter}:", true, next: newLetter);
            notes.Add($"Lối tắt, thư viện, chương trình cài trên {current}: và đường dẫn trong cấu hình sẽ không tự đổi theo - " +
                $"kiểm tra lại sau khi đổi sang {newLetter}:.");
            notes.Add("Ổ mạng ánh xạ trong phiên người dùng thường có thể không hiện trong cửa sổ quản trị - tránh chọn chữ cái đang dùng cho ổ mạng.");
        }
        Add(StepKind.VerifyLayout, "Đọc lại bảng phân vùng, đối chiếu với kết quả dự kiến", false);

        var plan = new Plan(
            Id: Guid.NewGuid().ToString("N")[..12],
            CreatedAt: clock.GetLocalNow(),
            Operation: Operation,
            DiskNumber: disk.Number,
            TargetLetter: current,
            LayoutFingerprint: LayoutFingerprint.Compute(layout),
            Before: layout,
            ExpectedAfter: LayoutSimulator.Apply(layout, steps),
            Steps: steps,
            Notes: notes,
            GainBytes: 0);
        var violations = SafetyGuard.CheckPlan(plan);
        return violations.Count > 0
            ? PlanResult.Refuse(RefusalCode.SafetyViolation, "Kế hoạch vi phạm bất biến an toàn - đã hủy.", [.. violations])
            : PlanResult.Ok(plan);
    }

    private static bool IsSystemTarget(PartitionInfo target, RenameRequest request) =>
        target.IsSystem || target.IsBoot
        || request.SystemDrive is { } sys && char.ToUpperInvariant(sys) == target.DriveLetter;
}
