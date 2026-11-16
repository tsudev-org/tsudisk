using System.Text.RegularExpressions;

namespace TsuDisk.Core.Model;

/// <summary>Trang thai Windows Recovery Environment, doc tu `reagentc /info`.</summary>
public sealed record WinReStatus(bool Enabled, int? DiskNumber, int? PartitionNumber)
{
    public static readonly WinReStatus Disabled = new(false, null, null);

    public bool LivesOn(PartitionInfo partition) =>
        Enabled && DiskNumber == partition.DiskNumber && PartitionNumber == partition.PartitionNumber;
}

/// <summary>
/// Phan tich ket qua `reagentc /info`. Ket qua cua reagentc duoc dich theo ngon ngu Windows, nen KHONG dua vao chu
/// "Enabled"; chi dua vao duong dan `\\?\GLOBALROOT\device\harddiskN\partitionM\...` - chi xuat hien khi WinRE dang bat.
/// </summary>
public static partial class ReAgentInfoParser
{
    [GeneratedRegex(@"harddisk(\d+)\\partition(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocationPattern();

    public static WinReStatus Parse(string reagentcOutput)
    {
        ArgumentNullException.ThrowIfNull(reagentcOutput);
        var match = LocationPattern().Match(reagentcOutput);
        if (!match.Success)
            return WinReStatus.Disabled;
        var disk = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var partition = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        return new WinReStatus(true, disk, partition);
    }
}
