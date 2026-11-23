using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Windows;

namespace TsuDisk.Gui;

/// <summary>Nguon bo cuc dia cho giao dien. CHI DOC: thao tac ghi di qua <see cref="IPlanExecutor"/> rieng (ADR-0009).</summary>
internal interface IDiskSource
{
    bool IsSample { get; }

    /// <summary><c>--chi-o-ao</c>: chi o ao, va ke hoach ghi mang VirtualDiskOnly (RenamePlanner tu choi o that).</summary>
    bool VirtualOnly => false;

    IReadOnlyList<DiskLayout> ReadAll();

    /// <summary>Trang thai WinRE; khong co quyen quan tri => Disabled (giong CLI) va <c>known = false</c>.</summary>
    (WinReStatus Status, bool Known) ReadWinRe();

    /// <summary>Chu cai dang dung o bat ky dau (moi dia, o mang, subst) - RenamePlanner can de tu choi chu cai trung.</summary>
    IReadOnlySet<char> LettersInUse();

    /// <summary>Chu cai o chua Windows (%SystemDrive%); khong bao gio doi chu cai o nay.</summary>
    char? SystemDrive { get; }
}

/// <param name="virtualOnly"><c>--chi-o-ao</c>: chi liet ke o ao VHD/VHDX (E2E lai giao dien khong bao gio thay o that).</param>
internal sealed class WindowsDiskSource(bool virtualOnly = false) : IDiskSource
{
    private readonly WindowsDiskBackend _backend = new();

    public bool IsSample => false;

    public bool VirtualOnly => virtualOnly;

    public IReadOnlyList<DiskLayout> ReadAll() =>
        [.. _backend.ListDisks().Where(d => !virtualOnly || d.IsVirtual).Select(d => _backend.ReadLayout(d.Number))];

    public (WinReStatus Status, bool Known) ReadWinRe()
    {
        if (!SystemTools.IsAdministrator())
            return (WinReStatus.Disabled, false);
        try
        {
            return (_backend.ReadWinRe(), true);
        }
        catch (DiskOperationException)
        {
            return (WinReStatus.Disabled, false);
        }
    }

    public IReadOnlySet<char> LettersInUse() => _backend.DriveLettersInUse();

    public char? SystemDrive => Environment.GetEnvironmentVariable("SystemDrive") is [var sys, ..] ? sys : null;
}

/// <summary>
/// Du lieu mau (<c>--du-lieu-mau</c>): anh chup man hinh cho website/PR va kiem giao dien tren CI ma khong phu thuoc o
/// that. Dia 0 la so do that do 16/11/2026 (giong Fixtures.RealMachine20261116 cua bo test).
/// </summary>
internal sealed class SampleDiskSource : IDiskSource
{
    public bool IsSample => true;

    public IReadOnlyList<DiskLayout> ReadAll()
    {
        var system = new DiskInfo(0, "NVMe SSD 512GB", 512_110_190_592, 512, PartitionStyle.Gpt, true, true, false, false, false, "NVMe");
        var data = new DiskInfo(1, "SATA HDD 1TB", 1_000_204_886_016, 512, PartitionStyle.Gpt, false, false, false, false, false, "SATA");
        return
        [
            new DiskLayout(system,
            [
                Part(0, 1, 1_048_576, 209_715_200, PartitionTypes.EfiSystem, null, "FAT32", null),
                Part(0, 2, 210_763_776, 16_777_216, PartitionTypes.MicrosoftReserved, null, null, null),
                Part(0, 3, 227_540_992, 284_663_218_176, PartitionTypes.BasicData, 'C', "NTFS", 68_610_000_000),
                Part(0, 4, 284_890_759_168, 961_544_192, PartitionTypes.Recovery, null, "NTFS", null),
                Part(0, 5, 350_276_812_800, 128_847_970_304, PartitionTypes.BasicData, 'G', "NTFS", 73_030_000_000),
                Part(0, 6, 479_124_783_104, 32_212_254_720, PartitionTypes.BasicData, 'E', "NTFS", 16_240_000_000),
            ]),
            new DiskLayout(data,
            [
                Part(1, 1, 1_048_576, 600_000_000_000, PartitionTypes.BasicData, 'D', "NTFS", 210_000_000_000),
            ]),
        ];
    }

    public (WinReStatus Status, bool Known) ReadWinRe() => (new WinReStatus(true, 0, 4), true);

    public IReadOnlySet<char> LettersInUse() =>
        ReadAll().SelectMany(l => l.Partitions).Select(p => p.DriveLetter).OfType<char>().ToHashSet();

    public char? SystemDrive => 'C';

    private static PartitionInfo Part(int disk, int number, long offset, long size, Guid type, char? letter, string? fs, long? free) =>
        new(disk, number, offset, size, type, null, letter, false, false, fs, letter == 'C' ? "Windows" : null, free);
}
