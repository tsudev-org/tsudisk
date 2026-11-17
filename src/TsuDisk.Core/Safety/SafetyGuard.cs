using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Safety;

/// <summary>
/// Bat bien an toan. Day la "hop dong" bao ve du lieu cua TsuDisk; vi pham bat ky dieu nao thi KHONG chay (luc lap ke
/// hoach) hoac BAO DONG (luc doi chieu sau khi chay):
/// <list type="number">
/// <item>Moi phan vung khong nam trong danh sach duoc cham giu nguyen offset, kich thuoc va loai.</item>
/// <item>Khong buoc nao cham phan vung duoc bao ve (EFI, MSR, o dong, Storage Spaces, Extended).</item>
/// <item>Chi xoa duoc phan vung Recovery; chi mo rong duoc o du lieu, va khong bao gio thu nho.</item>
/// <item>Khong chong lan, khong vuot vung dung duoc cua dia, moi offset moi deu can le 1 MiB.</item>
/// <item>Doi chu cai/nhan: chi o du lieu, khong doi chu cai o he thong/khoi dong, chu cai C-Z khong trung, nhan hop le;
/// phan vung do van giu nguyen offset/kich thuoc/loai.</item>
/// <item>Tao phan vung: nam tron trong mot vung trong cua bo cuc cu; dinh dang: chi phan vung vua tao trong cung ke hoach.</item>
/// <item>Xoa o du lieu (ADR-0005): chi trong thao tac delete-partition, dung MOT o du lieu co chu cai, khong he thong/khoi dong,
/// buoc ghi dung chu cai + kich thuoc + so phan vung cua o do.</item>
/// </list>
/// </summary>
public static class SafetyGuard
{
    /// <summary>Sai so cho phep khi doi chieu kich thuoc that (Windows co the lam tron xuong theo cluster/can le).</summary>
    public const long SizeTolerance = Units.MiB;

    public static IReadOnlyList<string> CheckPlan(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var violations = new List<string>();
        var before = plan.Before;

        foreach (var step in plan.Steps.Where(s => s.ChangesDisk))
        {
            if (step.Offset is not { } offset || step.Size is not { } size || size <= 0)
            {
                violations.Add($"Bước {step.Index} ({step.Kind}) thiếu offset/kích thước hợp lệ.");
                continue;
            }
            // Can le chi la dieu kien cho offset MOI; doi ten khong tao offset nao (o MBR cu bat dau o sector 63 van doi duoc).
            if (!step.IsMetadataOnly && !Units.IsAligned(offset, Units.PartitionAlignment))
                violations.Add($"Bước {step.Index}: offset {offset} không căn lề 1 MiB.");

            var existing = before.FindByOffset(offset);
            switch (step.Kind)
            {
                case StepKind.DeletePartition when existing?.Kind != PartitionKind.Recovery:
                    violations.Add($"Bước {step.Index}: chỉ được xóa phân vùng Recovery.");
                    break;
                case StepKind.ExtendPartition when existing is null || existing.Kind != PartitionKind.BasicData:
                    violations.Add($"Bước {step.Index}: chỉ được mở rộng ổ dữ liệu.");
                    break;
                case StepKind.ExtendPartition when size < existing.Size:
                    violations.Add($"Bước {step.Index}: kế hoạch thu nhỏ {existing.DisplayName} - bị cấm.");
                    break;
                case StepKind.ShrinkPartition when plan.Operation is not (Planning.WinReGrowPlanner.Operation or Planning.ShrinkPlanner.Operation):
                    violations.Add($"Bước {step.Index}: thu nhỏ chỉ được phép trong thao tác nới rộng WinRE hoặc thu nhỏ ổ (ADR-0003, ADR-0004).");
                    break;
                case StepKind.ShrinkPartition when existing is null || existing.Kind != PartitionKind.BasicData:
                    violations.Add($"Bước {step.Index}: chỉ được thu nhỏ ổ dữ liệu.");
                    break;
                case StepKind.ShrinkPartition when plan.Operation == Planning.ShrinkPlanner.Operation:
                    violations.AddRange(CheckGeneralShrink(step, existing));
                    break;
                case StepKind.ShrinkPartition when size >= existing.Size || existing.Size - size > Planning.WinReGrowRequest.MaxShrink:
                    violations.Add($"Bước {step.Index}: mức thu nhỏ {existing.DisplayName} không hợp lệ (phải nhỏ hơn hiện tại và không quá 4 GB).");
                    break;
                case StepKind.CreateRecovery when existing is not null && plan.Steps.All(s => s.Kind != StepKind.DeletePartition || s.Offset != offset):
                    violations.Add($"Bước {step.Index}: tạo phân vùng đè lên {existing.DisplayName}.");
                    break;
                case StepKind.SetDriveLetter or StepKind.SetVolumeLabel:
                    violations.AddRange(CheckRename(step, existing));
                    break;
                case StepKind.CreatePartition:
                    violations.AddRange(CheckCreate(step, before, existing));
                    break;
                case StepKind.FormatVolume:
                    violations.AddRange(CheckFormat(step, plan, existing));
                    break;
                case StepKind.DeleteDataPartition:
                    violations.AddRange(CheckDeleteData(step, plan, existing));
                    break;
            }
            if (existing is not null && PartitionTypes.IsProtected(existing.Kind))
                violations.Add($"Bước {step.Index}: chạm vào phân vùng được bảo vệ {existing.DisplayName}.");
        }

        violations.AddRange(CheckUntouchedPreserved(before, plan.ExpectedAfter, plan.TouchedOffsets, exactSizes: true));
        violations.AddRange(CheckGeometry(plan.ExpectedAfter));
        violations.AddRange(CheckUniqueLetters(plan.ExpectedAfter));
        if (plan.ExpectedAfter.Disk.Style == PartitionStyle.Mbr && plan.ExpectedAfter.Partitions.Count > 4)
            violations.Add("Đĩa MBR chỉ có tối đa 4 phân vùng chính.");
        return violations;
    }

    /// <summary>ADR-0004: chi NTFS, nho hon hien tai, diem cuoi can le 1 MiB, con du cho trong sau khi thu nho.</summary>
    private static IEnumerable<string> CheckGeneralShrink(PlanStep step, PartitionInfo existing)
    {
        var size = step.Size!.Value;
        if (existing.FileSystem != "NTFS")
            yield return $"Bước {step.Index}: chỉ thu nhỏ được ổ NTFS.";
        if (size >= existing.Size)
            yield return $"Bước {step.Index}: kích thước mới của {existing.DisplayName} phải nhỏ hơn hiện tại.";
        if (!Units.IsAligned(existing.Offset + size, Units.MiB))
            yield return $"Bước {step.Index}: điểm cuối mới của {existing.DisplayName} không căn lề 1 MiB.";
        var minFree = Planning.ShrinkRequest.MinFreeAfter(existing.IsSystem || existing.IsBoot, size);
        if (existing.FreeBytes is not { } free || free - (existing.Size - size) < minFree)
            yield return $"Bước {step.Index}: {existing.DisplayName} sẽ còn dưới {Units.Format(minFree)} trống sau khi thu nhỏ.";
    }

    /// <summary>ADR-0005: mot o du lieu co chu cai, khong he thong/khoi dong, khop tung truong; chi trong delete-partition.</summary>
    private static IEnumerable<string> CheckDeleteData(PlanStep step, Plan plan, PartitionInfo? existing)
    {
        if (plan.Operation != Planning.DeletePartitionPlanner.Operation)
            yield return $"Bước {step.Index}: xóa ổ dữ liệu chỉ được phép trong thao tác xóa phân vùng (ADR-0005).";
        if (plan.Steps.Count(s => s.ChangesDisk) != 1)
            yield return $"Bước {step.Index}: kế hoạch xóa phân vùng chỉ được có đúng một bước ghi đĩa.";
        if (existing is null || existing.Kind != PartitionKind.BasicData)
        {
            yield return $"Bước {step.Index}: chỉ được xóa ổ dữ liệu cơ bản.";
            yield break;
        }
        if (existing.IsSystem || existing.IsBoot)
            yield return $"Bước {step.Index}: cấm xóa ổ hệ thống/khởi động {existing.DisplayName}.";
        if (existing.DriveLetter is null || step.DriveLetter != existing.DriveLetter)
            yield return $"Bước {step.Index}: chữ cái trong kế hoạch không khớp ổ sẽ xóa (chỉ xóa ổ có chữ cái).";
        if (step.Size != existing.Size || step.PartitionNumber != existing.PartitionNumber)
            yield return $"Bước {step.Index}: kích thước/số phân vùng trong kế hoạch không khớp {existing.DisplayName}.";
        if ((step.NewLabel ?? "") != (existing.Label ?? ""))
            yield return $"Bước {step.Index}: nhãn trong kế hoạch không khớp {existing.DisplayName}.";
    }

    private static IEnumerable<string> CheckCreate(PlanStep step, DiskLayout before, PartitionInfo? existing)
    {
        var offset = step.Offset!.Value;
        var size = step.Size!.Value;
        if (existing is not null)
            yield return $"Bước {step.Index}: tạo phân vùng đè lên {existing.DisplayName}.";
        if (!before.FreeExtents().Any(f => f.Offset <= offset && offset + size <= f.End))
            yield return $"Bước {step.Index}: phân vùng mới không nằm trọn trong một vùng trống.";
        if (size % Units.MiB != 0 || size < Planning.CreatePartitionRequest.MinSize)
            yield return $"Bước {step.Index}: kích thước phân vùng mới phải là bội số 1 MiB và từ 64 MiB.";
    }

    private static IEnumerable<string> CheckFormat(PlanStep step, Plan plan, PartitionInfo? existing)
    {
        if (existing is not null)
            yield return $"Bước {step.Index}: cấm định dạng phân vùng có sẵn {existing.DisplayName} - chỉ định dạng phân vùng vừa tạo.";
        if (!plan.Steps.Any(s => s.Kind == StepKind.CreatePartition && s.Offset == step.Offset && s.Index < step.Index))
            yield return $"Bước {step.Index}: định dạng phải đi sau bước tạo phân vùng tại cùng vị trí.";
        if (step.FileSystem is not { } fs || !Planning.CreatePartitionRequest.FileSystems.Contains(fs))
            yield return $"Bước {step.Index}: hệ tệp không được hỗ trợ.";
        else if (Planning.VolumeNaming.LabelProblem(step.NewLabel ?? "", fs) is { } problem)
            yield return $"Bước {step.Index}: {problem}";
        if (step.NewDriveLetter is { } letter && !Planning.VolumeNaming.IsAssignableLetter(letter))
            yield return $"Bước {step.Index}: chữ cái mới phải trong khoảng C-Z.";
    }

    private static IEnumerable<string> CheckRename(PlanStep step, PartitionInfo? existing)
    {
        if (existing is null || existing.Kind != PartitionKind.BasicData || existing.FileSystem is null)
        {
            yield return $"Bước {step.Index}: chỉ đổi chữ cái/nhãn của ổ dữ liệu có hệ thống tệp.";
            yield break;
        }
        if (step.DriveLetter is not { } current || existing.DriveLetter != current)
            yield return $"Bước {step.Index}: chữ cái hiện tại trong kế hoạch không khớp {existing.DisplayName}.";
        if (step.Kind == StepKind.SetDriveLetter)
        {
            if (existing.IsSystem || existing.IsBoot)
                yield return $"Bước {step.Index}: cấm đổi chữ cái ổ hệ thống/khởi động {existing.DisplayName}.";
            if (step.NewDriveLetter is not { } next || !Planning.VolumeNaming.IsAssignableLetter(next))
                yield return $"Bước {step.Index}: chữ cái mới phải trong khoảng C-Z.";
        }
        else if (step.NewLabel is not { } label)
        {
            yield return $"Bước {step.Index}: thiếu nhãn mới.";
        }
        else if (Planning.VolumeNaming.LabelProblem(label, existing.FileSystem) is { } problem)
        {
            yield return $"Bước {step.Index}: {problem}";
        }
    }

    private static IEnumerable<string> CheckUniqueLetters(DiskLayout layout) =>
        layout.Partitions.Where(p => p.DriveLetter is not null).GroupBy(p => p.DriveLetter).Where(g => g.Count() > 1)
            .Select(g => $"Chữ cái {g.Key}: bị gán cho nhiều phân vùng.");

    /// <summary>Doi chieu bo cuc THAT sau khi chay voi bo cuc du kien cua ke hoach.</summary>
    public static IReadOnlyList<string> CheckActual(Plan plan, DiskLayout actual)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(actual);
        var violations = new List<string>();
        violations.AddRange(CheckUntouchedPreserved(plan.Before, actual, plan.TouchedOffsets, exactSizes: true));
        violations.AddRange(CheckGeometry(actual));
        violations.AddRange(CheckUniqueLetters(actual));

        foreach (var step in plan.Steps.Where(s => s.ChangesDisk))
        {
            var found = actual.FindByOffset(step.Offset!.Value);
            switch (step.Kind)
            {
                case StepKind.DeletePartition when plan.Steps.All(s => s.Kind != StepKind.CreateRecovery || s.Offset != step.Offset):
                case StepKind.DeleteDataPartition:
                    if (found is not null)
                        violations.Add($"Phân vùng tại offset {step.Offset} lẽ ra đã bị xóa nhưng vẫn còn.");
                    break;
                case StepKind.ExtendPartition:
                case StepKind.ShrinkPartition:
                case StepKind.CreateRecovery:
                case StepKind.CreatePartition:
                    if (found is null)
                    {
                        violations.Add($"Không thấy phân vùng tại offset {step.Offset} sau bước {step.Kind}.");
                    }
                    else if (Math.Abs(found.Size - step.Size!.Value) > SizeTolerance)
                    {
                        violations.Add($"{found.DisplayName}: kích thước thật {Units.Format(found.Size)} khác dự kiến {Units.Format(step.Size.Value)}.");
                    }
                    if (step.Kind == StepKind.CreateRecovery && found is not null && found.Kind != PartitionKind.Recovery)
                        violations.Add($"Phân vùng mới tại offset {step.Offset} không mang loại Recovery.");
                    if (step.Kind == StepKind.CreatePartition && found is not null && found.Kind != PartitionKind.BasicData)
                        violations.Add($"Phân vùng mới tại offset {step.Offset} không mang loại dữ liệu cơ bản.");
                    break;
                case StepKind.FormatVolume:
                    if (!string.Equals(found?.FileSystem, step.FileSystem, StringComparison.OrdinalIgnoreCase))
                        violations.Add($"Phân vùng mới tại offset {step.Offset} mang hệ tệp {found?.FileSystem ?? "RAW"}, lẽ ra là {step.FileSystem}.");
                    if (!string.Equals(found?.Label ?? "", step.NewLabel ?? "", StringComparison.OrdinalIgnoreCase))
                        violations.Add($"Phân vùng mới tại offset {step.Offset} mang nhãn \"{found?.Label}\", lẽ ra là \"{step.NewLabel}\".");
                    if (found?.DriveLetter != step.NewDriveLetter)
                        violations.Add($"Phân vùng mới tại offset {step.Offset} mang chữ cái {found?.DriveLetter}, lẽ ra là {step.NewDriveLetter}.");
                    break;
                case StepKind.SetDriveLetter when found?.DriveLetter != step.NewDriveLetter:
                    violations.Add($"Phân vùng tại offset {step.Offset} mang chữ cái {found?.DriveLetter}, lẽ ra là {step.NewDriveLetter}.");
                    break;
                // FAT32 luu nhan IN HOA: so khong phan biet hoa thuong.
                case StepKind.SetVolumeLabel when !string.Equals(found?.Label ?? "", step.NewLabel ?? "", StringComparison.OrdinalIgnoreCase):
                    violations.Add($"Phân vùng tại offset {step.Offset} mang nhãn \"{found?.Label}\", lẽ ra là \"{step.NewLabel}\".");
                    break;
            }
        }
        return violations;
    }

    private static IEnumerable<string> CheckUntouchedPreserved(DiskLayout before, DiskLayout after, IReadOnlySet<long> touched, bool exactSizes)
    {
        foreach (var original in before.Partitions.Where(p => !touched.Contains(p.Offset)))
        {
            var now = after.FindByOffset(original.Offset);
            if (now is null)
            {
                yield return $"{original.DisplayName} (offset {original.Offset}) biến mất - phân vùng này lẽ ra không bị chạm.";
                continue;
            }
            if (exactSizes && now.Size != original.Size)
                yield return $"{original.DisplayName}: kích thước đổi từ {original.Size} thành {now.Size} - lẽ ra không bị chạm.";
            if (now.Kind != original.Kind)
                yield return $"{original.DisplayName}: loại phân vùng đổi - lẽ ra không bị chạm.";
        }
    }

    private static IEnumerable<string> CheckGeometry(DiskLayout layout)
    {
        var ordered = layout.Ordered;
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            if (p.Offset < 0 || p.Size <= 0 || p.End > layout.Disk.Size)
                yield return $"{p.DisplayName} nằm ngoài phạm vi đĩa.";
            if (i > 0 && ordered[i - 1].End > p.Offset)
                yield return $"{ordered[i - 1].DisplayName} chồng lấn {p.DisplayName}.";
        }
    }
}
