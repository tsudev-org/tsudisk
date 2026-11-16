using TsuDisk.Core.Model;

namespace TsuDisk.Core.Tests;

/// <summary>Bo cuc mau. <see cref="RealMachine20261116"/> la so do THAT do bang Get-Partition ngay 16/11/2026.</summary>
internal static class Fixtures
{
    public const long RealDiskSize = 512_110_190_592;

    public static DiskInfo Disk(long size = RealDiskSize, PartitionStyle style = PartitionStyle.Gpt, bool isVirtual = false,
        bool readOnly = false) =>
        new(0, "NVMe SSD 512GB", size, 512, style, IsSystem: true, IsBoot: true, IsReadOnly: readOnly, IsOffline: false,
            IsVirtual: isVirtual, BusType: isVirtual ? "FileBackedVirtual" : "RAID");

    public static PartitionInfo Part(int number, long offset, long size, Guid type, char? letter = null,
        string? fs = null, long? free = null) =>
        new(0, number, offset, size, type, null, letter, false, false, fs, null, free);

    /// <summary>EFI | MSR | C: | Recovery 917 MB | 60 GB trong | G: | E: | 737 MB trong.</summary>
    public static DiskLayout RealMachine20261116(DiskInfo? disk = null) => new(disk ?? Disk(),
    [
        Part(1, 1_048_576, 209_715_200, PartitionTypes.EfiSystem, fs: "FAT32"),
        Part(2, 210_763_776, 16_777_216, PartitionTypes.MicrosoftReserved),
        Part(3, 227_540_992, 284_663_218_176, PartitionTypes.BasicData, 'C', "NTFS", 68_610_000_000),
        Part(4, 284_890_759_168, 961_544_192, PartitionTypes.Recovery, fs: "NTFS"),
        Part(5, 350_276_812_800, 128_847_970_304, PartitionTypes.BasicData, 'G', "NTFS", 73_030_000_000),
        Part(6, 479_124_783_104, 32_212_254_720, PartitionTypes.BasicData, 'E', "NTFS", 16_240_000_000),
    ]);

    public static readonly WinReStatus WinReOnPartition4 = new(true, 0, 4);

    /// <summary>EFI | C: | 10 GB trong | D:.</summary>
    public static DiskLayout DirectGap() => new(Disk(100 * Units.GiB),
    [
        Part(1, Units.MiB, 100 * Units.MiB, PartitionTypes.EfiSystem, fs: "FAT32"),
        Part(2, 101 * Units.MiB, 40 * Units.GiB, PartitionTypes.BasicData, 'C', "NTFS"),
        Part(3, 101 * Units.MiB + 50 * Units.GiB, 40 * Units.GiB, PartitionTypes.BasicData, 'D', "NTFS"),
    ]);
}
