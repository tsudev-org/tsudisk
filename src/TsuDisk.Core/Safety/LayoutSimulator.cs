using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Safety;

/// <summary>
/// Chay "tren giay" cac buoc cua ke hoach len mo hinh bo cuc de ra bo cuc DU KIEN. Bo cuc du kien la can cu cho ca
/// SafetyGuard luc lap ke hoach (truoc khi cham o) lan luc doi chieu sau khi chay that.
/// </summary>
public static class LayoutSimulator
{
    public static DiskLayout Apply(DiskLayout layout, IEnumerable<PlanStep> steps)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(steps);
        var partitions = layout.Partitions.ToList();
        int? freedNumber = null;

        foreach (var step in steps.Where(s => s.ChangesDisk))
        {
            switch (step.Kind)
            {
                case StepKind.DeletePartition:
                {
                    var victim = partitions.Single(p => p.Offset == step.Offset);
                    freedNumber = victim.PartitionNumber;
                    partitions.Remove(victim);
                    break;
                }
                case StepKind.ExtendPartition:
                case StepKind.ShrinkPartition:
                {
                    var index = partitions.FindIndex(p => p.Offset == step.Offset);
                    var old = partitions[index];
                    var growth = step.Size!.Value - old.Size;
                    partitions[index] = old with { Size = step.Size.Value, FreeBytes = old.FreeBytes + growth };
                    break;
                }
                case StepKind.CreateRecovery:
                {
                    var gpt = layout.Disk.Style == PartitionStyle.Gpt;
                    var number = freedNumber ?? (partitions.Count == 0 ? 1 : partitions.Max(p => p.PartitionNumber) + 1);
                    partitions.Add(new PartitionInfo(
                        DiskNumber: layout.Disk.Number,
                        PartitionNumber: number,
                        Offset: step.Offset!.Value,
                        Size: step.Size!.Value,
                        GptType: gpt ? PartitionTypes.Recovery : null,
                        MbrType: gpt ? null : PartitionTypes.MbrRecovery,
                        DriveLetter: null,
                        IsSystem: false,
                        IsBoot: false,
                        FileSystem: "NTFS",
                        Label: "Recovery",
                        FreeBytes: null));
                    break;
                }
                case StepKind.SetDriveLetter:
                {
                    var index = partitions.FindIndex(p => p.Offset == step.Offset);
                    partitions[index] = partitions[index] with { DriveLetter = step.NewDriveLetter };
                    break;
                }
                case StepKind.SetVolumeLabel:
                {
                    var index = partitions.FindIndex(p => p.Offset == step.Offset);
                    partitions[index] = partitions[index] with { Label = string.IsNullOrEmpty(step.NewLabel) ? null : step.NewLabel };
                    break;
                }
                case StepKind.CreatePartition:
                {
                    var gpt = layout.Disk.Style == PartitionStyle.Gpt;
                    var used = partitions.Select(p => p.PartitionNumber).ToHashSet();
                    partitions.Add(new PartitionInfo(
                        DiskNumber: layout.Disk.Number,
                        PartitionNumber: Enumerable.Range(1, 128).First(n => !used.Contains(n)),
                        Offset: step.Offset!.Value,
                        Size: step.Size!.Value,
                        GptType: gpt ? PartitionTypes.BasicData : null,
                        MbrType: gpt ? null : PartitionTypes.MbrIfs,
                        DriveLetter: null,
                        IsSystem: false,
                        IsBoot: false,
                        FileSystem: null,
                        Label: null,
                        FreeBytes: null));
                    break;
                }
                case StepKind.FormatVolume:
                {
                    var index = partitions.FindIndex(p => p.Offset == step.Offset);
                    partitions[index] = partitions[index] with
                    {
                        FileSystem = step.FileSystem,
                        Label = string.IsNullOrEmpty(step.NewLabel) ? null : step.NewLabel,
                        DriveLetter = step.NewDriveLetter,
                    };
                    break;
                }
                default:
                    throw new InvalidOperationException($"Bước {step.Kind} được đánh dấu thay đổi đĩa nhưng mô phỏng không hỗ trợ.");
            }
        }
        return layout with { Partitions = [.. partitions.OrderBy(p => p.Offset)] };
    }
}
