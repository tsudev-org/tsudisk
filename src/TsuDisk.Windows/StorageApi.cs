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

    public static long GetMinSize(int diskNumber, int partitionNumber)
    {
        using var partition = Partition(diskNumber, partitionNumber);
        var result = partition.InvokeMethod("GetSupportedSize", null, null);
        EnsureSuccess(result, "GetSupportedSize");
        return checked((long)(ulong)result["SizeMin"]);
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

    /// <summary>Chu cai dang dung: moi volume Storage API biet + GetLogicalDrives (o mang, subst cua phien hien tai).</summary>
    public static IReadOnlySet<char> LettersInUse()
    {
        var letters = new HashSet<char>();
        foreach (var volume in Query("SELECT DriveLetter FROM MSFT_Volume"))
        {
            using (volume)
            {
                var letter = Convert.ToChar(volume["DriveLetter"] ?? '\0', CultureInfo.InvariantCulture);
                if (char.IsAsciiLetter(letter))
                    letters.Add(char.ToUpperInvariant(letter));
            }
        }
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.Name.Length > 0 && char.IsAsciiLetter(drive.Name[0]))
                letters.Add(char.ToUpperInvariant(drive.Name[0]));
        }
        return letters;
    }

    /// <summary>
    /// MSFT_Partition.RemoveAccessPath("X:\") roi AddAccessPath("Y:\"). Gan chu cai moi loi thi gan lai chu cai cu ngay
    /// trong ham nay truoc khi nem loi - o khong bao gio bi bo lai khong co chu cai vi TsuDisk.
    /// </summary>
    public static void SetDriveLetter(int diskNumber, int partitionNumber, char? oldLetter, char newLetter)
    {
        if (oldLetter is { } removed)
            InvokeAccessPath(diskNumber, partitionNumber, "RemoveAccessPath", removed);
        try
        {
            InvokeAccessPath(diskNumber, partitionNumber, "AddAccessPath", newLetter);
        }
        catch (Exception ex) when (ex is DiskOperationException or ManagementException)
        {
            if (oldLetter is not { } restoreLetter)
                throw new DiskOperationException($"Gán {newLetter}: thất bại - {ex.Message}", ex);
            try
            {
                InvokeAccessPath(diskNumber, partitionNumber, "AddAccessPath", restoreLetter);
            }
            catch (Exception restore) when (restore is DiskOperationException or ManagementException)
            {
                throw new DiskOperationException(
                    $"Gán {newLetter}: thất bại ({ex.Message}) và KHÔNG gán lại được {oldLetter}: ({restore.Message}) - gán lại bằng Disk Management.", ex);
            }
            throw new DiskOperationException($"Gán {newLetter}: thất bại, đã gán lại {oldLetter}: - {ex.Message}", ex);
        }
    }

    private static void InvokeAccessPath(int diskNumber, int partitionNumber, string method, char letter)
    {
        using var partition = Partition(diskNumber, partitionNumber);
        using var input = partition.GetMethodParameters(method);
        input["AccessPath"] = Inv($"{char.ToUpperInvariant(letter)}:\\");
        EnsureSuccess(partition.InvokeMethod(method, input, null), method);
    }

    /// <summary>MSFT_Disk.CreatePartition dung offset + kich thuoc, loai du lieu co ban, khong gan chu cai (gan sau khi dinh dang).</summary>
    public static void CreateDataPartition(int diskNumber, long offset, long size, PartitionStyle style)
    {
        using var disk = Query(Inv($"SELECT * FROM MSFT_Disk WHERE Number = {diskNumber}")).SingleOrDefault()
            ?? throw new DiskOperationException($"Không tìm thấy đĩa {diskNumber}.");
        using var input = disk.GetMethodParameters("CreatePartition");
        input["Offset"] = checked((ulong)offset);
        input["Size"] = checked((ulong)size);
        input["AssignDriveLetter"] = false;
        if (style == PartitionStyle.Gpt)
            input["GptType"] = "{" + PartitionTypes.BasicData.ToString() + "}";
        else
            input["MbrType"] = (ushort)PartitionTypes.MbrIfs;
        EnsureSuccess(disk.InvokeMethod("CreatePartition", input, null), "CreatePartition");
    }

    /// <summary>MSFT_Volume.Format (nhanh) tren volume cua phan vung, roi AddAccessPath chu cai.</summary>
    public static void FormatVolume(int diskNumber, int partitionNumber, string fileSystem, string label, char? driveLetter)
    {
        // Volume cua phan vung vua tao co the xuat hien tre vai giay sau CreatePartition.
        for (var attempt = 0; ; attempt++)
        {
            var volume = FindVolume(diskNumber, partitionNumber);
            if (volume is not null)
            {
                using (volume)
                {
                    using var input = volume.GetMethodParameters("Format");
                    input["FileSystem"] = fileSystem;
                    input["FileSystemLabel"] = label;
                    input["Full"] = false;
                    EnsureSuccess(volume.InvokeMethod("Format", input, null), "Format");
                }
                break;
            }
            if (attempt >= 20)
                throw new DiskOperationException($"Không thấy volume của phân vùng {partitionNumber} trên đĩa {diskNumber} để định dạng.");
            Thread.Sleep(500);
        }
        if (driveLetter is { } letter)
            InvokeAccessPath(diskNumber, partitionNumber, "AddAccessPath", letter);
    }

    private static ManagementObject? FindVolume(int diskNumber, int partitionNumber)
    {
        string[] accessPaths;
        using (var partition = Partition(diskNumber, partitionNumber))
            accessPaths = partition["AccessPaths"] as string[] ?? [];
        var volumes = Query("SELECT * FROM MSFT_Volume");
        var match = volumes.FirstOrDefault(v => v["Path"] is string path && accessPaths.Contains(path, StringComparer.OrdinalIgnoreCase));
        foreach (var other in volumes.Where(v => !ReferenceEquals(v, match)))
            other.Dispose();
        return match;
    }

    /// <summary>MSFT_Volume.SetFileSystemLabel tren volume nam tren phan vung (tim theo AccessPaths, khong theo chu cai).</summary>
    public static void SetVolumeLabel(int diskNumber, int partitionNumber, string label)
    {
        using var volume = FindVolume(diskNumber, partitionNumber)
            ?? throw new DiskOperationException($"Không tìm thấy volume của phân vùng {partitionNumber} trên đĩa {diskNumber}.");
        using var input = volume.GetMethodParameters("SetFileSystemLabel");
        input["FileSystemLabel"] = label;
        EnsureSuccess(volume.InvokeMethod("SetFileSystemLabel", input, null), "SetFileSystemLabel");
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
