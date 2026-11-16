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
            if (!Units.IsAligned(offset, Units.PartitionAlignment))
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
                case StepKind.ShrinkPartition when plan.Operation != Planning.WinReGrowPlanner.Operation:
                    violations.Add($"Bước {step.Index}: thu nhỏ chỉ được phép trong thao tác nới rộng WinRE.");
                    break;
                case StepKind.ShrinkPartition when existing is null || existing.Kind != PartitionKind.BasicData:
                    violations.Add($"Bước {step.Index}: chỉ được thu nhỏ ổ dữ liệu.");
                    break;
                case StepKind.ShrinkPartition when size >= existing.Size || existing.Size - size > Planning.WinReGrowRequest.MaxShrink:
                    violations.Add($"Bước {step.Index}: mức thu nhỏ {existing.DisplayName} không hợp lệ (phải nhỏ hơn hiện tại và không quá 4 GB).");
                    break;
                case StepKind.CreateRecovery when existing is not null && plan.Steps.All(s => s.Kind != StepKind.DeletePartition || s.Offset != offset):
                    violations.Add($"Bước {step.Index}: tạo phân vùng đè lên {existing.DisplayName}.");
                    break;
            }
            if (existing is not null && PartitionTypes.IsProtected(existing.Kind))
                violations.Add($"Bước {step.Index}: chạm vào phân vùng được bảo vệ {existing.DisplayName}.");
        }

        violations.AddRange(CheckUntouchedPreserved(before, plan.ExpectedAfter, plan.TouchedOffsets, exactSizes: true));
        violations.AddRange(CheckGeometry(plan.ExpectedAfter));
        return violations;
    }

    /// <summary>Doi chieu bo cuc THAT sau khi chay voi bo cuc du kien cua ke hoach.</summary>
    public static IReadOnlyList<string> CheckActual(Plan plan, DiskLayout actual)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(actual);
        var violations = new List<string>();
        violations.AddRange(CheckUntouchedPreserved(plan.Before, actual, plan.TouchedOffsets, exactSizes: true));
        violations.AddRange(CheckGeometry(actual));

        foreach (var step in plan.Steps.Where(s => s.ChangesDisk))
        {
            var found = actual.FindByOffset(step.Offset!.Value);
            switch (step.Kind)
            {
                case StepKind.DeletePartition when plan.Steps.All(s => s.Kind != StepKind.CreateRecovery || s.Offset != step.Offset):
                    if (found is not null)
                        violations.Add($"Phân vùng tại offset {step.Offset} lẽ ra đã bị xóa nhưng vẫn còn.");
                    break;
                case StepKind.ExtendPartition:
                case StepKind.ShrinkPartition:
                case StepKind.CreateRecovery:
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
