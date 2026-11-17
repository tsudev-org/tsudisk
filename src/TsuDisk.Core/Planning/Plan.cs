using TsuDisk.Core.Model;

namespace TsuDisk.Core.Planning;

/// <summary>Loai buoc trong ke hoach. Thu tu khai bao KHONG mang nghia - thu tu chay nam o Plan.Steps.</summary>
public enum StepKind
{
    /// <summary>Doc suc khoe o (HealthStatus + S.M.A.R.T.); o bi danh gia hong thi dung truoc moi thao tac ghi.</summary>
    CheckHealth,
    /// <summary>Quet NTFS truc tuyen (chi doc) o dich; loi thi dung.</summary>
    ScanVolume,
    /// <summary>Luu anh bo cuc + BCD vao thu muc nhat ky.</summary>
    Backup,
    /// <summary>`reagentc /disable`: Windows chep Winre.wim ve C:\Windows\System32\Recovery.</summary>
    DisableWinRe,
    /// <summary>Xoa phan vung (chi cho phep voi Recovery, kiem lai offset + loai ngay truoc khi xoa).</summary>
    DeletePartition,
    /// <summary>Thu nho o du lieu - CHI trong thao tac grow-winre, toi da 4 GiB, o phai con du trong (WinReGrowPlanner).</summary>
    ShrinkPartition,
    /// <summary>Mo rong phan vung dich (NTFS/ReFS mo rong truc tuyen, khong doi du lieu).</summary>
    ExtendPartition,
    /// <summary>Tao phan vung Recovery moi tai offset chi dinh, NTFS, GPT type + thuoc tinh chuan.</summary>
    CreateRecovery,
    /// <summary>`reagentc /enable`: dua WinRE vao phan vung Recovery moi.</summary>
    EnableWinRe,
    /// <summary>Doc lai bo cuc va doi chieu voi bo cuc du kien + bat bien an toan.</summary>
    VerifyLayout,
    /// <summary>Doi chu cai o (RemoveAccessPath + AddAccessPath). Chi o du lieu, cam o he thong/khoi dong (RenamePlanner).</summary>
    SetDriveLetter,
    /// <summary>Doi nhan he thong tep (MSFT_Volume.SetFileSystemLabel). Khong doi hinh hoc phan vung.</summary>
    SetVolumeLabel,
    /// <summary>Tao phan vung du lieu (RAW, chua chu cai) CHI trong vung trong cua bo cuc luc lap ke hoach.</summary>
    CreatePartition,
    /// <summary>Dinh dang nhanh + gan nhan/chu cai - CHI phan vung vua tao trong cung ke hoach, va chi khi con RAW.</summary>
    FormatVolume,
}

/// <summary>Mot buoc cua ke hoach. Cac truong tham so la tuy chon, tuy loai buoc.</summary>
public sealed record PlanStep(
    int Index,
    StepKind Kind,
    string Title,
    bool ChangesDisk,
    int? PartitionNumber = null,
    long? Offset = null,
    long? Size = null,
    char? DriveLetter = null,
    char? NewDriveLetter = null,
    string? NewLabel = null,
    string? FileSystem = null)
{
    /// <summary>Buoc chi doi sieu du lieu cua o (chu cai, nhan): phan vung van phai giu nguyen offset/kich thuoc/loai.</summary>
    public bool IsMetadataOnly => Kind is StepKind.SetDriveLetter or StepKind.SetVolumeLabel;
}

/// <summary>Ke hoach da lap: bat bien, tu mo ta day du, luu duoc ra JSON de xem truoc/kiem toan.</summary>
public sealed record Plan(
    string Id,
    DateTimeOffset CreatedAt,
    string Operation,
    int DiskNumber,
    char TargetLetter,
    string LayoutFingerprint,
    DiskLayout Before,
    DiskLayout ExpectedAfter,
    IReadOnlyList<PlanStep> Steps,
    IReadOnlyList<string> Notes,
    long GainBytes)
{
    /// <summary>
    /// Offset cac phan vung ma ke hoach duoc phep doi HINH HOC (doi kich thuoc/xoa/tao). Buoc chi doi chu cai/nhan khong
    /// nam o day: phan vung do van bi bat bien "giu nguyen offset, kich thuoc, loai" kiem nhu moi phan vung khac.
    /// </summary>
    public IReadOnlySet<long> TouchedOffsets => Steps
        .Where(s => s.ChangesDisk && !s.IsMetadataOnly && s.Offset is not null)
        .Select(s => s.Offset!.Value)
        .ToHashSet();
}

public enum RefusalCode
{
    TargetNotFound,
    DiskNotWritable,
    UnsupportedPartitionStyle,
    DynamicDisk,
    UnsupportedFileSystem,
    ProtectedTarget,
    NothingToGain,
    NotAdjacent,
    RecoveryNotWinRe,
    RecoveryTooSmallGap,
    InvalidOption,
    SafetyViolation,
    LetterInUse,
}

public sealed record Refusal(RefusalCode Code, string Message, IReadOnlyList<string> Hints);

/// <summary>Ket qua lap ke hoach: hoac co Plan, hoac co Refusal giai thich ly do va goi y.</summary>
public sealed record PlanResult(Plan? Plan, Refusal? Refusal)
{
    public bool Success => Plan is not null;

    public static PlanResult Ok(Plan plan) => new(plan, null);

    public static PlanResult Refuse(RefusalCode code, string message, params string[] hints) =>
        new(null, new Refusal(code, message, hints));
}
