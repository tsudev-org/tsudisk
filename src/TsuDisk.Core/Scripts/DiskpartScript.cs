using System.Globalization;
using TsuDisk.Core.Model;

namespace TsuDisk.Core.Scripts;

/// <summary>
/// Sinh kich ban diskpart. Chi dung cho DUY NHAT viec Storage Management API khong lam duoc: dat thuoc tinh GPT
/// 0x8000000000000001 cho phan vung Recovery (MSFT_Partition khong co bit "platform required"). Sinh o day (thuan)
/// de bo test kiem tung dong; tang Windows chi ghi ra tep va chay `diskpart /s`.
/// </summary>
public static class DiskpartScript
{
    public static IReadOnlyList<string> CreateRecovery(int diskNumber, long offsetBytes, PartitionStyle style)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(diskNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(offsetBytes);
        if (!Units.IsAligned(offsetBytes, Units.PartitionAlignment))
            throw new ArgumentException("Offset phải căn lề 1 MiB.", nameof(offsetBytes));
        if (style is not (PartitionStyle.Gpt or PartitionStyle.Mbr))
            throw new ArgumentException("Chỉ hỗ trợ đĩa GPT hoặc MBR.", nameof(style));

        // diskpart nhan offset theo KB. Khong ghi size: lay het vung trong lien ke tai offset (ke hoach da tinh dung).
        var lines = new List<string>
        {
            Inv($"select disk {diskNumber}"),
            Inv($"create partition primary offset={offsetBytes / Units.KiB}"),
            "format quick fs=ntfs label=\"Recovery\"",
        };
        if (style == PartitionStyle.Gpt)
        {
            lines.Add($"set id={PartitionTypes.Recovery:D} override");
            lines.Add(Inv($"gpt attributes=0x{PartitionTypes.RecoveryGptAttributes:x16}"));
        }
        else
        {
            lines.Add(Inv($"set id={PartitionTypes.MbrRecovery:x2} override"));
        }
        return lines;
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
