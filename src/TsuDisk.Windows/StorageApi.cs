using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;

namespace TsuDisk.Windows;

/// <summary>
/// Doc/ghi qua Windows Storage Management API (lop MSFT_Disk / MSFT_Partition / MSFT_Volume) - cung API ma
/// Get-Partition, Resize-Partition, Disk Management dung. Doc khong can quyen quan tri; ghi thi can.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StorageApi
{
    private const string Namespace = @"\\.\root\Microsoft\Windows\Storage";

    private static ManagementScope Scope()
    {
        var scope = new ManagementScope(Namespace);
        scope.Connect();
        return scope;
    }

    private static List<ManagementObject> Query(string wql)
    {
        using var searcher = new ManagementObjectSearcher(Scope(), new ObjectQuery(wql));
        return [.. searcher.Get().Cast<ManagementObject>()];
    }

    public static IReadOnlyList<DiskInfo> ListDisks() =>
        [.. Query("SELECT * FROM MSFT_Disk").Select(ToDisk).OrderBy(d => d.Number)];

    public static DiskLayout ReadLayout(int diskNumber)
    {
        var disk = Query(Inv($"SELECT * FROM MSFT_Disk WHERE Number = {diskNumber}")).Select(ToDisk).SingleOrDefault()
            ?? throw new DiskOperationException($"Không tìm thấy đĩa {diskNumber}.");
        var volumes = Query("SELECT Path, FileSystem, FileSystemLabel, SizeRemaining FROM MSFT_Volume")
            .Select(v => (Path: v["Path"] as string, FileSystem: v["FileSystem"] as string, Label: v["FileSystemLabel"] as string,
                Free: v["SizeRemaining"] is ulong free ? (long?)free : null))
            .Where(v => !string.IsNullOrEmpty(v.Path))
            .ToDictionary(v => v.Path!, StringComparer.OrdinalIgnoreCase);

        var partitions = Query(Inv($"SELECT * FROM MSFT_Partition WHERE DiskNumber = {diskNumber}")).Select(p =>
        {
            var accessPaths = p["AccessPaths"] as string[] ?? [];
            var volume = accessPaths.Select(a => volumes.TryGetValue(a, out var v) ? v : default).FirstOrDefault(v => v.Path is not null);
            var letter = Convert.ToChar(p["DriveLetter"] ?? '\0', CultureInfo.InvariantCulture);
            var gpt = p["GptType"] as string;
            return new PartitionInfo(
                DiskNumber: diskNumber,
                PartitionNumber: Convert.ToInt32(p["PartitionNumber"], CultureInfo.InvariantCulture),
                Offset: checked((long)(ulong)p["Offset"]),
                Size: checked((long)(ulong)p["Size"]),
                GptType: string.IsNullOrEmpty(gpt) ? null : Guid.Parse(gpt),
                MbrType: disk.Style == PartitionStyle.Mbr ? Convert.ToByte(p["MbrType"] ?? 0, CultureInfo.InvariantCulture) : null,
                DriveLetter: char.IsLetter(letter) ? char.ToUpperInvariant(letter) : null,
                IsSystem: p["IsSystem"] is true,
                IsBoot: p["IsBoot"] is true,
                FileSystem: string.IsNullOrEmpty(volume.FileSystem) ? null : volume.FileSystem,
                Label: string.IsNullOrEmpty(volume.Label) ? null : volume.Label,
                FreeBytes: volume.Free);
        }).OrderBy(p => p.Offset).ToList();
        return new DiskLayout(disk, partitions);
    }

    public static long GetMaxSize(int diskNumber, int partitionNumber)
    {
        using var partition = Partition(diskNumber, partitionNumber);
        var result = partition.InvokeMethod("GetSupportedSize", null, null);
        EnsureSuccess(result, "GetSupportedSize");
        return checked((long)(ulong)result["SizeMax"]);
    }

    public static void Resize(int diskNumber, int partitionNumber, long newSize)
    {
        using var partition = Partition(diskNumber, partitionNumber);
        using var input = partition.GetMethodParameters("Resize");
        input["Size"] = checked((ulong)newSize);
        EnsureSuccess(partition.InvokeMethod("Resize", input, null), "Resize");
    }

    public static void Delete(int diskNumber, int partitionNumber)
    {
        using var partition = Partition(diskNumber, partitionNumber);
        EnsureSuccess(partition.InvokeMethod("DeleteObject", null, null), "DeleteObject");
    }

    /// <summary>MSFT_Volume.Repair voi Scan=true: quet truc tuyen, chi doc. Output 0 = NoErrorsFound.</summary>
    public static (bool Healthy, string Detail) ScanVolume(char driveLetter)
    {
        var volume = Query(Inv($"SELECT * FROM MSFT_Volume WHERE DriveLetter = '{char.ToUpperInvariant(driveLetter)}'")).SingleOrDefault()
            ?? throw new DiskOperationException($"Không tìm thấy ổ {driveLetter}:.");
        using (volume)
        {
            using var input = volume.GetMethodParameters("Repair");
            input["Scan"] = true;
            var result = volume.InvokeMethod("Repair", input, null);
            EnsureSuccess(result, "Repair -Scan");
            var output = Convert.ToUInt32(result["Output"] ?? 0u, CultureInfo.InvariantCulture);
            return output == 0 ? (true, "NoErrorsFound") : (false, Inv($"RepairResult={output}"));
        }
    }

    private static ManagementObject Partition(int diskNumber, int partitionNumber) =>
        Query(Inv($"SELECT * FROM MSFT_Partition WHERE DiskNumber = {diskNumber} AND PartitionNumber = {partitionNumber}")).SingleOrDefault()
        ?? throw new DiskOperationException($"Không tìm thấy phân vùng {partitionNumber} trên đĩa {diskNumber}.");

    private static void EnsureSuccess(ManagementBaseObject result, string method)
    {
        var code = Convert.ToUInt32(result["ReturnValue"] ?? 0u, CultureInfo.InvariantCulture);
        if (code == 0)
            return;
        var detail = result.Properties.Cast<PropertyData>().Any(p => p.Name == "ExtendedStatus") && result["ExtendedStatus"] is ManagementBaseObject status
            ? status["Message"] as string
            : null;
        throw new DiskOperationException(Inv($"Storage API {method} trả mã {code}{(detail is null ? "" : $": {detail}")}"));
    }

    private static DiskInfo ToDisk(ManagementObject d)
    {
        var bus = Convert.ToUInt16(d["BusType"] ?? (ushort)0, CultureInfo.InvariantCulture);
        return new DiskInfo(
            Number: Convert.ToInt32(d["Number"], CultureInfo.InvariantCulture),
            FriendlyName: d["FriendlyName"] as string ?? "",
            Size: checked((long)(ulong)d["Size"]),
            LogicalSectorSize: Convert.ToInt32(d["LogicalSectorSize"] ?? 512u, CultureInfo.InvariantCulture),
            Style: (PartitionStyle)Convert.ToInt32(d["PartitionStyle"] ?? (ushort)0, CultureInfo.InvariantCulture),
            IsSystem: d["IsSystem"] is true,
            IsBoot: d["IsBoot"] is true,
            IsReadOnly: d["IsReadOnly"] is true,
            IsOffline: d["IsOffline"] is true,
            IsVirtual: bus is 14 or 15,
            BusType: BusName(bus));
    }

    private static string BusName(ushort bus) => bus switch
    {
        1 => "SCSI", 3 => "ATA", 7 => "USB", 8 => "RAID", 9 => "iSCSI", 10 => "SAS", 11 => "SATA", 12 => "SD",
        14 => "Virtual", 15 => "FileBackedVirtual", 16 => "StorageSpaces", 17 => "NVMe",
        _ => Inv($"Bus{bus}"),
    };

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
