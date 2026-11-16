using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Gpt;
using TsuDisk.Core.Health;
using TsuDisk.Core.Model;

namespace TsuDisk.Windows;

/// <summary>Doc suc khoe o (MSFT_PhysicalDisk + MSFT_StorageReliabilityCounter). Chi doc.</summary>
[SupportedOSPlatform("windows")]
internal static class HealthReader
{
    private const string Namespace = @"\\.\root\Microsoft\Windows\Storage";

    public static DiskHealth? Read(int diskNumber)
    {
        var scope = new ManagementScope(Namespace);
        scope.Connect();
        using var searcher = new ManagementObjectSearcher(scope,
            new ObjectQuery($"SELECT * FROM MSFT_PhysicalDisk WHERE DeviceId = '{diskNumber.ToString(CultureInfo.InvariantCulture)}'"));
        using var disk = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        if (disk is null)
            return null;

        int? temp = null, tempMax = null, wear = null;
        long? readErr = null, writeErr = null, hours = null;
        try
        {
            // Can quyen quan tri; driver RAID/USB thuong khong tra - khi do giu null (HealthEvaluator coi la "khong ro").
            var output = disk.InvokeMethod("GetReliabilityCounter", null, null);
            if (output["ReliabilityCounter"] is ManagementBaseObject counter)
            {
                temp = ToInt(counter["Temperature"]);
                tempMax = ToInt(counter["TemperatureMax"]);
                wear = ToInt(counter["Wear"]);
                readErr = ToLong(counter["ReadErrorsUncorrected"]);
                writeErr = ToLong(counter["WriteErrorsUncorrected"]);
                hours = ToLong(counter["PowerOnHours"]);
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        var operational = (disk["OperationalStatus"] as ushort[] ?? []).Select(OperationalName).ToList();
        return new DiskHealth(
            DiskNumber: diskNumber,
            Model: (disk["FriendlyName"] as string ?? "").Trim(),
            MediaType: Convert.ToUInt16(disk["MediaType"] ?? (ushort)0, CultureInfo.InvariantCulture) switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => "Không rõ" },
            HealthStatus: Convert.ToUInt16(disk["HealthStatus"] ?? (ushort)5, CultureInfo.InvariantCulture) switch { 0 => "Healthy", 1 => "Warning", 2 => "Unhealthy", _ => "Unknown" },
            OperationalStatus: operational,
            TemperatureCelsius: temp is > 0 ? temp : null,
            TemperatureMaxCelsius: tempMax is > 0 ? tempMax : null,
            WearPercent: wear,
            ReadErrorsUncorrected: readErr,
            WriteErrorsUncorrected: writeErr,
            PowerOnHours: hours);
    }

    private static int? ToInt(object? value) => value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    private static long? ToLong(object? value) => value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);

    private static string OperationalName(ushort code) => code switch
    {
        2 => "OK", 3 => "Degraded", 4 => "Stressed", 5 => "Predictive Failure", 6 => "Error", 7 => "Non-Recoverable Error",
        8 => "Starting", 9 => "Stopping", 10 => "Stopped", 11 => "In Service", 12 => "No Contact", 13 => "Lost Communication",
        0xD002 => "Online", 0xD004 => "Not Usable", 0xD012 => "Failed Media",
        _ => $"Mã {code.ToString(CultureInfo.InvariantCulture)}",
    };
}

/// <summary>
/// Doc tho 1 MiB dau va 1 MiB cuoi dia qua \\.\PhysicalDriveN - CHI DOC (FileAccess.Read). Vung nay chua protective
/// MBR, header + bang entry GPT chinh (dau) va bang entry + header du phong (cuoi) voi moi kich thuoc sector thong dung.
/// </summary>
[SupportedOSPlatform("windows")]
public static class RawDiskReader
{
    public const int RegionBytes = 1024 * 1024;

    public static GptImage ReadGptRegions(DiskInfo disk)
    {
        ArgumentNullException.ThrowIfNull(disk);
        var path = $@"\\.\PhysicalDrive{disk.Number.ToString(CultureInfo.InvariantCulture)}";
        var sector = Math.Max(disk.LogicalSectorSize, 512);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 0);
            var head = ReadAt(stream, 0);
            var tailOffset = Units.AlignDown(disk.Size - RegionBytes, sector);
            var tail = ReadAt(stream, tailOffset);
            // GptImage tinh LBA dau cua Tail tu kich thuoc dia: cat Tail cho dung den sector cuoi.
            var tailLength = (int)(disk.Size / sector * sector - tailOffset);
            return new GptImage(sector, disk.Size / sector * sector, head, tail[..tailLength]);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DiskOperationException("Đọc trực tiếp đĩa cần quyền quản trị: mở Terminal (Admin).", ex);
        }
        catch (IOException ex)
        {
            throw new DiskOperationException($"Không đọc được {path}: {ex.Message}", ex);
        }
    }

    private static byte[] ReadAt(FileStream stream, long offset)
    {
        var buffer = new byte[RegionBytes];
        stream.Seek(offset, SeekOrigin.Begin);
        var read = 0;
        while (read < buffer.Length)
        {
            var n = stream.Read(buffer, read, buffer.Length - read);
            if (n == 0)
                break;
            read += n;
        }
        return read == buffer.Length ? buffer : buffer[..read];
    }
}
