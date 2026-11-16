using System.Runtime.Versioning;
using System.Text;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Gpt;
using TsuDisk.Core.Health;
using TsuDisk.Core.Model;
using TsuDisk.Core.Scripts;
using TsuDisk.Core.Serialization;

namespace TsuDisk.Windows;

/// <summary>Hien thuc IDiskBackend tren Windows that.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDiskBackend : IDiskBackend
{
    private string? _runDirectory;

    public IReadOnlyList<DiskInfo> ListDisks() => StorageApi.ListDisks();

    public DiskLayout ReadLayout(int diskNumber) => StorageApi.ReadLayout(diskNumber);

    public WinReStatus ReadWinRe() => ReAgentInfoParser.Parse(SystemTools.RunChecked("reagentc.exe", "/info").Output);

    public DiskHealth? ReadHealth(int diskNumber) => HealthReader.Read(diskNumber);

    public VolumeScanResult ScanVolume(char driveLetter)
    {
        var (healthy, detail) = StorageApi.ScanVolume(driveLetter);
        return new VolumeScanResult(healthy, detail);
    }

    public void SaveBackups(string runDirectory, DiskLayout layout)
    {
        _runDirectory = runDirectory;
        Directory.CreateDirectory(runDirectory);
        File.WriteAllText(Path.Combine(runDirectory, "layout-before.json"), TsuDiskJson.Serialize(new DiskLayout[] { layout }), Encoding.UTF8);
        File.WriteAllText(Path.Combine(runDirectory, "reagentc-before.txt"), SystemTools.Run("reagentc.exe", ["/info"]).Output, Encoding.UTF8);
        SystemTools.RunChecked("bcdedit.exe", "/export", Path.Combine(runDirectory, "bcd-backup"));
        if (layout.Disk.Style == PartitionStyle.Gpt)
        {
            var report = GptBackup.Save(layout, runDirectory);
            if (!report.Healthy)
                throw new DiskOperationException("Bảng GPT trên đĩa không toàn vẹn - không thay đổi gì: " +
                    string.Join(" | ", report.Findings.Where(f => f.Level == GptCheckLevel.Error).Select(f => f.Message)));
        }
    }

    public void DisableWinRe()
    {
        SystemTools.RunChecked("reagentc.exe", "/disable");
        // Ban sao thu hai cua anh WinRE nam ngoai thu muc Windows: neu Winre.wim tren C: hong/mat, van khoi phuc duoc.
        if (_runDirectory is not null && File.Exists(SystemTools.WinReImagePath))
            File.Copy(SystemTools.WinReImagePath, Path.Combine(_runDirectory, "Winre-backup.wim"), overwrite: true);
    }

    public void EnableWinRe() => SystemTools.RunChecked("reagentc.exe", "/enable");

    public bool WinReImageOnSystemDrive() => File.Exists(SystemTools.WinReImagePath);

    public long GetMaxPartitionSize(int diskNumber, int partitionNumber) => StorageApi.GetMaxSize(diskNumber, partitionNumber);

    public void DeletePartition(int diskNumber, int partitionNumber)
    {
        try
        {
            StorageApi.Delete(diskNumber, partitionNumber);
        }
        catch (DiskOperationException)
        {
            // Mot so Recovery mang thuoc tinh "platform required" bi Storage API tu choi xoa: dung duong diskpart
            // `delete partition override` - van tren DUNG so dia/phan vung da kiem lai ngay truoc do (PlanRunner).
            RunDiskpart([$"select disk {diskNumber}", $"select partition {partitionNumber}", "delete partition override"]);
        }
    }

    public void ResizePartition(int diskNumber, int partitionNumber, long newSize) =>
        StorageApi.Resize(diskNumber, partitionNumber, newSize);

    public void CreateRecoveryPartition(int diskNumber, long offset, long? size, PartitionStyle style) =>
        RunDiskpart(DiskpartScript.CreateRecovery(diskNumber, offset, style, size));

    public long GetMinPartitionSize(int diskNumber, int partitionNumber) => StorageApi.GetMinSize(diskNumber, partitionNumber);

    private void RunDiskpart(IReadOnlyList<string> lines)
    {
        var directory = _runDirectory ?? Path.Combine(Path.GetTempPath(), "tsudisk");
        Directory.CreateDirectory(directory);
        var script = Path.Combine(directory, $"diskpart-{DateTime.Now:HHmmssfff}.txt");
        File.WriteAllLines(script, lines, Encoding.ASCII);
        var result = SystemTools.Run("diskpart.exe", ["/s", script]);
        File.WriteAllText(Path.ChangeExtension(script, ".log"), result.Output, Encoding.UTF8);
        if (result.ExitCode != 0)
            throw new DiskOperationException($"diskpart thất bại (mã {result.ExitCode}), xem {Path.ChangeExtension(script, ".log")}");
    }
}

/// <summary>Ghi nhat ky JSON nguyen tu: ghi tep tam roi doi ten de, de mat dien giua chung khong de lai tep hong.</summary>
public sealed class FileJournalStore(string runDirectory) : IJournalStore
{
    public string JournalPath => Path.Combine(runDirectory, "journal.json");

    public void Save(RunJournal journal)
    {
        Directory.CreateDirectory(runDirectory);
        var temp = JournalPath + ".tmp";
        File.WriteAllText(temp, TsuDiskJson.Serialize(journal), Encoding.UTF8);
        File.Move(temp, JournalPath, overwrite: true);
    }
}
