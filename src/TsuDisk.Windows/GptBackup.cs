using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using TsuDisk.Core.Gpt;
using TsuDisk.Core.Model;
using TsuDisk.Core.Serialization;

namespace TsuDisk.Windows;

/// <summary>
/// Sao luu vung GPT tho (kieu `sgdisk --backup`) + bo cuc + bao cao kiem tra + SHA256SUMS. Khoi phuc la thao tac rui ro
/// cao, CO CHU DICH khong tu dong hoa: tai lieu docs/HUONG-DAN-MO-RONG-O-C.md muc 7 huong dan lam tay khi can.
/// </summary>
[SupportedOSPlatform("windows")]
public static class GptBackup
{
    public static GptReport Save(DiskLayout layout, string directory)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Directory.CreateDirectory(directory);
        var image = RawDiskReader.ReadGptRegions(layout.Disk);
        var report = GptVerifier.Verify(image, layout);

        var files = new Dictionary<string, byte[]>
        {
            [$"gpt-dau-dia{layout.Disk.Number}.bin"] = image.Head,
            [$"gpt-cuoi-dia{layout.Disk.Number}.bin"] = image.Tail,
            [$"bo-cuc-dia{layout.Disk.Number}.json"] = Encoding.UTF8.GetBytes(TsuDiskJson.Serialize(new DiskLayout[] { layout })),
            [$"kiem-tra-gpt-dia{layout.Disk.Number}.json"] = Encoding.UTF8.GetBytes(TsuDiskJson.Serialize(report)),
        };
        var sums = new StringBuilder();
        foreach (var (name, bytes) in files)
        {
            File.WriteAllBytes(Path.Combine(directory, name), bytes);
            sums.Append(Convert.ToHexStringLower(SHA256.HashData(bytes))).Append("  ").Append(name).Append('\n');
        }
        sums.Append("# Vùng đầu: byte 0 - ").Append(image.Head.Length).Append("; vùng cuối bắt đầu ở byte ")
            .Append(image.TailFirstLba * image.SectorSize).Append("; sector ").Append(image.SectorSize).Append(" byte.\n");
        File.WriteAllText(Path.Combine(directory, $"SHA256SUMS-dia{layout.Disk.Number}.txt"), sums.ToString(), Encoding.UTF8);
        return report;
    }
}
