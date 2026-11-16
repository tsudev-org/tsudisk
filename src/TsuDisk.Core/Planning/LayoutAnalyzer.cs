using TsuDisk.Core.Model;

namespace TsuDisk.Core.Planning;

/// <summary>Danh gia mot vung trong: o nao co the hap thu no an toan, va vi sao.</summary>
public sealed record FreeSpaceAdvice(Segment Extent, PartitionInfo? Absorber, bool ViaRecoveryRelocation, string Advice);

/// <summary>
/// Phan tich bo cuc: voi moi vung trong, tim o du lieu dung NGAY TRUOC no (hoac truoc Recovery dung truoc no) - do la
/// o duy nhat hap thu duoc vung trong ma khong phai doi cho du lieu.
/// </summary>
public static class LayoutAnalyzer
{
    public static IReadOnlyList<FreeSpaceAdvice> Analyze(DiskLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var segments = layout.Segments();
        var result = new List<FreeSpaceAdvice>();
        for (var i = 0; i < segments.Count; i++)
        {
            var free = segments[i];
            if (!free.IsFree)
                continue;

            var before = i > 0 ? segments[i - 1].Partition : null;
            if (IsExtendable(before))
            {
                result.Add(new FreeSpaceAdvice(free, before, false,
                    $"{Units.Format(free.Size)} trống nằm ngay sau {before!.DisplayName} - mở rộng trực tiếp: tsudisk mo-rong {before.DriveLetter}:"));
                continue;
            }

            var twoBefore = i > 1 ? segments[i - 2].Partition : null;
            if (before?.Kind == PartitionKind.Recovery && IsExtendable(twoBefore))
            {
                result.Add(new FreeSpaceAdvice(free, twoBefore, true,
                    $"{Units.Format(free.Size)} trống bị Recovery chắn sau {twoBefore!.DisplayName} - dời Recovery rồi mở rộng: tsudisk mo-rong {twoBefore.DriveLetter}:"));
                continue;
            }

            var reason = before is null
                ? "nằm đầu đĩa, không có phân vùng nào đứng trước"
                : $"đứng sau {before.DisplayName} ({PartitionTypes.Describe(before.Kind)}), không mở rộng được";
            result.Add(new FreeSpaceAdvice(free, null, false,
                $"{Units.Format(free.Size)} trống {reason}. Có thể tạo phân vùng mới tại đây; gộp vào ổ khác phải dời dữ liệu - không khuyến nghị."));
        }
        return result;
    }

    private static bool IsExtendable(PartitionInfo? partition) =>
        partition is { Kind: PartitionKind.BasicData, DriveLetter: not null, FileSystem: "NTFS" or "ReFS" };
}
