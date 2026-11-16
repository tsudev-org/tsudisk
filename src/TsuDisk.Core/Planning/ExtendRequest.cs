using TsuDisk.Core.Model;

namespace TsuDisk.Core.Planning;

/// <summary>Cach xu ly phan vung Recovery dung chan giua o dich va vung trong.</summary>
public enum RecoveryPolicy
{
    /// <summary>Mac dinh: chi doi cho Recovery khi WinRE dang nam tren no (anh WinRE duoc Windows chep ve C: truoc).</summary>
    RelocateActiveWinReOnly,
    /// <summary>
    /// Recovery KHONG chua WinRE dang dung (sot lai sau nang cap, o ao thu nghiem): xoa va tao lai Recovery rong.
    /// Noi dung cu cua no se mat - nguoi dung phai chu dong bat co nay.
    /// </summary>
    RecreateEmpty,
}

/// <summary>Yeu cau "mo rong o X bang vung trong lien ke".</summary>
public sealed record ExtendRequest(char TargetLetter)
{
    /// <summary>Kich thuoc Recovery moi. Microsoft khuyen du cho ban cap nhat WinRE: 1 GiB la an toan.</summary>
    public long RecoverySize { get; init; } = Units.GiB;

    public RecoveryPolicy RecoveryPolicy { get; init; } = RecoveryPolicy.RelocateActiveWinReOnly;

    /// <summary>Muc tang toi thieu de dang lam; nho hon thi tu choi (tranh thao tac vo ich tren he thong).</summary>
    public long MinimumGain { get; init; } = 16 * Units.MiB;

    /// <summary>Chi cho phep thao tac tren o ao (VHD/VHDX). Bo kiem thu E2E bat co nay de khong bao gio cham o that.</summary>
    public bool VirtualDiskOnly { get; init; }

    public const long MinRecoverySize = 512 * Units.MiB;
    public const long MaxRecoverySize = 4 * Units.GiB;
}
