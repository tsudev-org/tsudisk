using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TsuDisk.Core.Model;

namespace TsuDisk.Core.Safety;

/// <summary>
/// Dau van tay cua bo cuc: SHA-256 tren phan BAT BIEN (dia + offset/kich thuoc/loai tung phan vung). Bo qua dung luong
/// con trong va nhan o vi chung doi lien tuc. Ke hoach mang dau van tay luc lap; bo chay tu choi neu bo cuc that da khac
/// (co nguoi/cong cu khac vua sua o) - kiem tra dong thoi kieu "optimistic concurrency".
/// </summary>
public static class LayoutFingerprint
{
    public static string Compute(DiskLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var builder = new StringBuilder();
        var disk = layout.Disk;
        builder.Append(CultureInfo.InvariantCulture, $"disk={disk.Number};size={disk.Size};style={disk.Style};sector={disk.LogicalSectorSize}\n");
        foreach (var p in layout.Ordered)
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"{p.PartitionNumber}:{p.Offset}:{p.Size}:{p.GptType?.ToString() ?? "-"}:{p.MbrType?.ToString(CultureInfo.InvariantCulture) ?? "-"}\n");
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
