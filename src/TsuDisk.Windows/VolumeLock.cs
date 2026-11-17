using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using TsuDisk.Core.Execution;

namespace TsuDisk.Windows;

/// <summary>
/// Khoa doc quyen mot volume (FSCTL_LOCK_VOLUME) roi go gan ket (FSCTL_DISMOUNT_VOLUME) - cach Windows bao dam khong
/// con chuong trinh nao dang mo tep tren o. Khoa that bai = o dang duoc dung (tep mo, pagefile, thu muc hien hanh cua
/// mot cua so lenh...) - ADR-0005 muc 4: dung, khong ep.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class VolumeLock : IDisposable
{
    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlDismountVolume = 0x00090020;
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;

    private readonly SafeFileHandle _handle;

    private VolumeLock(SafeFileHandle handle) => _handle = handle;

    /// <summary>Khoa + go gan ket; giu khoa cho toi khi Dispose. Nem <see cref="DiskOperationException"/> neu o dang dung.</summary>
    public static VolumeLock Acquire(char driveLetter)
    {
        var path = $@"\\.\{char.ToUpperInvariant(driveLetter)}:";
        SafeFileHandle handle;
        try
        {
            handle = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DiskOperationException($"Không mở được ổ {driveLetter}: để khóa ({ex.Message}).", ex);
        }

        // Windows co the tra "dang dung" thoang qua khi trinh quet (Defender, Search) vua cham o: thu lai vai lan.
        for (var attempt = 1; ; attempt++)
        {
            if (NativeMethods.DeviceIoControl(handle, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                break;
            var error = Marshal.GetLastPInvokeError();
            if (attempt >= 5 || error is not (ErrorAccessDenied or ErrorSharingViolation))
            {
                handle.Dispose();
                throw new DiskOperationException($"Ổ {driveLetter}: đang được dùng (còn chương trình mở tệp trên ổ, hoặc pagefile/cửa sổ " +
                    $"lệnh đang đứng ở ổ này) - đóng chúng rồi chạy lại. Không xóa gì. ({new Win32Exception(error).Message})");
            }
            Thread.Sleep(500);
        }
        if (!NativeMethods.DeviceIoControl(handle, FsctlDismountVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new DiskOperationException($"Không gỡ gắn kết được ổ {driveLetter}: ({new Win32Exception(error).Message}) - không xóa gì.");
        }
        return new VolumeLock(handle);
    }

    public void Dispose() => _handle.Dispose();

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, IntPtr inBuffer, uint inBufferSize,
            IntPtr outBuffer, uint outBufferSize, out uint bytesReturned, IntPtr overlapped);
    }
}
