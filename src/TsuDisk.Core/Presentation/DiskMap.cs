using TsuDisk.Core.Model;

namespace TsuDisk.Core.Presentation;

/// <summary>Vai tro hien thi cua mot doan tren ban do dia - quyet dinh mau, khong quyet dinh thao tac nao.</summary>
public enum MapRole
{
    Free,
    Data,
    System,
    Recovery,
    Other,
}

/// <summary>Mot khoi tren thanh ban do dia: doan goc + ti le chieu rong (tong moi khoi = 1).</summary>
public sealed record MapBlock(Segment Segment, double Fraction, MapRole Role);

/// <summary>
/// Dung thanh ban do dia kieu Disk Management. Phan vung nho (EFI 100 MB, MSR 16 MB tren o 1 TB) duoc nong toi
/// <c>minFraction</c> de con nhin thay va bam duoc; phan con lai chia theo dung luong that.
/// </summary>
public static class DiskMap
{
    public const double DefaultMinFraction = 0.035;

    public static IReadOnlyList<MapBlock> Build(DiskLayout layout, double minFraction = DefaultMinFraction)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var segments = layout.Segments();
        if (segments.Count == 0)
            return [];
        var fractions = Widths([.. segments.Select(s => (double)Math.Max(1, s.Size))], minFraction);
        return [.. segments.Select((s, i) => new MapBlock(s, fractions[i], RoleOf(s)))];
    }

    public static MapRole RoleOf(Segment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return segment.Partition?.Kind switch
        {
            null => MapRole.Free,
            PartitionKind.BasicData => MapRole.Data,
            PartitionKind.EfiSystem or PartitionKind.MicrosoftReserved => MapRole.System,
            PartitionKind.Recovery => MapRole.Recovery,
            _ => MapRole.Other,
        };
    }

    /// <summary>Ti le tung khoi: khoi duoi muc toi thieu duoc nang len, cac khoi con lai co lai theo ti le; tong = 1.</summary>
    internal static double[] Widths(IReadOnlyList<double> sizes, double minFraction)
    {
        var n = sizes.Count;
        if (minFraction * n >= 1)
            return [.. Enumerable.Repeat(1.0 / n, n)];
        var total = sizes.Sum();
        var result = sizes.Select(s => s / total).ToArray();
        var pinned = new bool[n];
        // Lap toi khi khong con khoi nao bi co xuong duoi muc toi thieu (moi vong ghim them it nhat mot khoi).
        for (var round = 0; round < n; round++)
        {
            var pinnedShare = pinned.Count(p => p) * minFraction;
            var freeTotal = Enumerable.Range(0, n).Where(i => !pinned[i]).Sum(i => sizes[i]);
            var changed = false;
            for (var i = 0; i < n; i++)
            {
                if (pinned[i])
                    continue;
                result[i] = sizes[i] / freeTotal * (1 - pinnedShare);
                if (result[i] < minFraction)
                {
                    pinned[i] = true;
                    changed = true;
                }
            }
            if (!changed)
                break;
            for (var i = 0; i < n; i++)
            {
                if (pinned[i])
                    result[i] = minFraction;
            }
        }
        return result;
    }
}
