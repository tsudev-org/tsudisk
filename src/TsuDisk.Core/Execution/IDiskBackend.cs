using TsuDisk.Core.Health;
using TsuDisk.Core.Model;

namespace TsuDisk.Core.Execution;

/// <summary>Ket qua quet he thong tep truc tuyen (chi doc).</summary>
public sealed record VolumeScanResult(bool Healthy, string Detail);

/// <summary>
/// Cong (port) toi he dieu hanh. Ban that: TsuDisk.Windows.WindowsDiskBackend (Storage Management API + cong cu he thong).
/// Ban gia trong bo test mo phong o dia bang bo nho va tiem loi tung buoc de kiem chung rollback.
/// Moi phuong thuc nem <see cref="DiskOperationException"/> khi that bai.
/// </summary>
public interface IDiskBackend
{
    IReadOnlyList<DiskInfo> ListDisks();

    DiskLayout ReadLayout(int diskNumber);

    WinReStatus ReadWinRe();

    VolumeScanResult ScanVolume(char driveLetter);

    /// <summary>Suc khoe o vat ly chua dia; null khi driver khong cung cap (RAID, USB).</summary>
    DiskHealth? ReadHealth(int diskNumber);

    /// <summary>Luu ban sao luu (anh bo cuc, BCD, Winre.wim neu co) vao thu muc nhat ky cua lan chay.</summary>
    void SaveBackups(string runDirectory, DiskLayout layout);

    void DisableWinRe();

    void EnableWinRe();

    /// <summary>True neu Winre.wim dang nam tren o he thong (sau khi tat WinRE).</summary>
    bool WinReImageOnSystemDrive();

    /// <summary>Kich thuoc toi da Windows cho phep mo rong phan vung toi.</summary>
    long GetMaxPartitionSize(int diskNumber, int partitionNumber);

    void DeletePartition(int diskNumber, int partitionNumber);

    void ResizePartition(int diskNumber, int partitionNumber, long newSize);

    /// <summary>Tao phan vung Recovery (NTFS, loai + thuoc tinh chuan) chiem tu offset toi het vung trong lien ke.</summary>
    void CreateRecoveryPartition(int diskNumber, long offset, PartitionStyle style);
}

public sealed class DiskOperationException : Exception
{
    public DiskOperationException() { }

    public DiskOperationException(string message) : base(message) { }

    public DiskOperationException(string message, Exception inner) : base(message, inner) { }
}
