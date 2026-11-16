using TsuDisk.Core.Execution;
using TsuDisk.Core.Health;
using TsuDisk.Core.Model;

namespace TsuDisk.Core.Tests;

/// <summary>
/// O dia mo phong trong bo nho, hanh xu nhu Windows (vd. tao phan vung chiem het vung trong lien ke, so thu tu moi
/// lay so nho nhat con trong). <see cref="FailOn"/> tiem loi vao mot thao tac de kiem chung rollback.
/// </summary>
internal sealed class FakeBackend(DiskLayout initial, WinReStatus winRe) : IDiskBackend
{
    public DiskLayout Layout { get; private set; } = initial;
    public WinReStatus WinRe { get; private set; } = winRe;
    public bool WinReImageOnC { get; set; }
    public bool ImageMissingAfterDisable { get; set; }
    public VolumeScanResult Scan { get; set; } = new(true, "NoErrorsFound");
    public HashSet<string> FailOn { get; } = [];
    public List<string> Calls { get; } = [];

    private void Enter(string operation)
    {
        Calls.Add(operation);
        if (FailOn.Contains(operation))
            throw new DiskOperationException($"Lỗi tiêm vào: {operation}");
    }

    public IReadOnlyList<DiskInfo> ListDisks() => [Layout.Disk];

    public DiskLayout ReadLayout(int diskNumber) => Layout;

    public WinReStatus ReadWinRe() => WinRe;

    public DiskHealth? Health { get; set; } = new(0, "NVMe SSD 512GB", "SSD", "Healthy", ["OK"], 40, 70, 3, 0, 0, 1200);

    public DiskHealth? ReadHealth(int diskNumber)
    {
        Enter("health");
        return Health;
    }

    public VolumeScanResult ScanVolume(char driveLetter)
    {
        Enter("scan");
        return Scan;
    }

    public void SaveBackups(string runDirectory, DiskLayout layout) => Enter("backup");

    public void DisableWinRe()
    {
        Enter("disable-winre");
        WinRe = WinReStatus.Disabled;
        WinReImageOnC = !ImageMissingAfterDisable;
    }

    public void EnableWinRe()
    {
        Enter("enable-winre");
        var recovery = Layout.Partitions.FirstOrDefault(p => p.Kind == PartitionKind.Recovery);
        WinRe = recovery is null
            ? new WinReStatus(true, Layout.Disk.Number, Layout.FindByLetter('C')?.PartitionNumber)
            : new WinReStatus(true, Layout.Disk.Number, recovery.PartitionNumber);
    }

    public bool WinReImageOnSystemDrive() => WinReImageOnC;

    public long GetMaxPartitionSize(int diskNumber, int partitionNumber)
    {
        var part = Layout.Partitions.Single(p => p.PartitionNumber == partitionNumber);
        var next = Layout.Partitions.Where(p => p.Offset > part.Offset).OrderBy(p => p.Offset).FirstOrDefault();
        var limit = next?.Offset ?? Layout.Disk.LastUsableByteExclusive;
        return Units.AlignDown(limit, Units.MiB) - part.Offset;
    }

    public void DeletePartition(int diskNumber, int partitionNumber)
    {
        Enter("delete");
        Layout = Layout with { Partitions = [.. Layout.Partitions.Where(p => p.PartitionNumber != partitionNumber)] };
    }

    public void ResizePartition(int diskNumber, int partitionNumber, long newSize)
    {
        Enter("resize");
        if (newSize > GetMaxPartitionSize(diskNumber, partitionNumber))
            throw new DiskOperationException("Vượt kích thước tối đa.");
        Layout = Layout with
        {
            Partitions = [.. Layout.Partitions.Select(p => p.PartitionNumber == partitionNumber ? p with { Size = newSize } : p)],
        };
    }

    public void CreateRecoveryPartition(int diskNumber, long offset, PartitionStyle style)
    {
        Enter("create-recovery");
        var next = Layout.Partitions.Where(p => p.Offset > offset).OrderBy(p => p.Offset).FirstOrDefault();
        var end = Units.AlignDown(next?.Offset ?? Layout.Disk.LastUsableByteExclusive, Units.MiB);
        if (Layout.Partitions.Any(p => p.Offset < end && p.End > offset))
            throw new DiskOperationException("Vùng đích không trống.");
        var used = Layout.Partitions.Select(p => p.PartitionNumber).ToHashSet();
        var number = Enumerable.Range(1, 128).First(n => !used.Contains(n));
        var created = new PartitionInfo(diskNumber, number, offset, end - offset, PartitionTypes.Recovery, null, null,
            false, false, "NTFS", "Recovery", null);
        Layout = Layout with { Partitions = [.. Layout.Partitions, created] };
    }
}

internal sealed class MemoryJournalStore : IJournalStore
{
    public List<RunJournal> Saved { get; } = [];

    public void Save(RunJournal journal) => Saved.Add(journal);
}
