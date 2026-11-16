namespace TsuDisk.Core.Model;

public enum PartitionStyle
{
    Unknown = 0,
    Mbr = 1,
    Gpt = 2,
}

/// <summary>Vai tro cua phan vung, suy ra tu GPT type GUID hoac MBR type byte.</summary>
public enum PartitionKind
{
    Unknown = 0,
    EfiSystem,
    MicrosoftReserved,
    BasicData,
    Recovery,
    LdmMetadata,
    LdmData,
    StorageSpaces,
    LinuxFilesystem,
    LinuxSwap,
    LinuxLvm,
    MbrExtended,
}

/// <summary>Bang GUID loai phan vung GPT va byte loai MBR chinh thuc (UEFI spec + tai lieu Microsoft).</summary>
public static class PartitionTypes
{
    public static readonly Guid EfiSystem = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    public static readonly Guid MicrosoftReserved = new("e3c9e316-0b5c-4db8-817d-f92df00215ae");
    public static readonly Guid BasicData = new("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");
    public static readonly Guid Recovery = new("de94bba4-06d1-4d40-a16a-bfd50179d6ac");
    public static readonly Guid LdmMetadata = new("5808c8aa-7e8f-42e0-85d2-e1e90434cfb3");
    public static readonly Guid LdmData = new("af9b60a0-1431-4f62-bc68-3311714a69ad");
    public static readonly Guid StorageSpaces = new("e75caf8f-f680-4cee-afa3-b001e56efc2d");
    public static readonly Guid LinuxFilesystem = new("0fc63daf-8483-4772-8e79-3d69d8477de4");
    public static readonly Guid LinuxSwap = new("0657fd6d-a4ab-43c4-84e5-0933c84b4f4f");
    public static readonly Guid LinuxLvm = new("e6d6d379-f507-44c2-a23c-238f2a3df928");

    /// <summary>Thuoc tinh GPT cho Recovery: bit 0 (platform required) + bit 63 (no drive letter).</summary>
    public const ulong RecoveryGptAttributes = 0x8000000000000001;

    public const byte MbrRecovery = 0x27;

    public static PartitionKind FromGpt(Guid type)
    {
        if (type == EfiSystem) return PartitionKind.EfiSystem;
        if (type == MicrosoftReserved) return PartitionKind.MicrosoftReserved;
        if (type == BasicData) return PartitionKind.BasicData;
        if (type == Recovery) return PartitionKind.Recovery;
        if (type == LdmMetadata) return PartitionKind.LdmMetadata;
        if (type == LdmData) return PartitionKind.LdmData;
        if (type == StorageSpaces) return PartitionKind.StorageSpaces;
        if (type == LinuxFilesystem) return PartitionKind.LinuxFilesystem;
        if (type == LinuxSwap) return PartitionKind.LinuxSwap;
        if (type == LinuxLvm) return PartitionKind.LinuxLvm;
        return PartitionKind.Unknown;
    }

    public static PartitionKind FromMbr(byte type) => type switch
    {
        0x07 or 0x06 or 0x0B or 0x0C or 0x0E => PartitionKind.BasicData,
        MbrRecovery => PartitionKind.Recovery,
        0xEF => PartitionKind.EfiSystem,
        0x05 or 0x0F => PartitionKind.MbrExtended,
        0x42 => PartitionKind.LdmData,
        0x83 => PartitionKind.LinuxFilesystem,
        0x82 => PartitionKind.LinuxSwap,
        0x8E => PartitionKind.LinuxLvm,
        _ => PartitionKind.Unknown,
    };

    /// <summary>Phan vung TsuDisk khong bao gio duoc xoa, doi kich thuoc hay ghi de.</summary>
    public static bool IsProtected(PartitionKind kind) => kind is PartitionKind.EfiSystem
        or PartitionKind.MicrosoftReserved
        or PartitionKind.LdmMetadata
        or PartitionKind.LdmData
        or PartitionKind.StorageSpaces
        or PartitionKind.MbrExtended;

    public static string Describe(PartitionKind kind) => kind switch
    {
        PartitionKind.EfiSystem => "Phân vùng hệ thống EFI",
        PartitionKind.MicrosoftReserved => "Phân vùng dành riêng Microsoft (MSR)",
        PartitionKind.BasicData => "Dữ liệu cơ bản",
        PartitionKind.Recovery => "Phân vùng khôi phục (Recovery)",
        PartitionKind.LdmMetadata or PartitionKind.LdmData => "Ổ động (Dynamic disk)",
        PartitionKind.StorageSpaces => "Storage Spaces",
        PartitionKind.LinuxFilesystem => "Hệ thống tệp Linux",
        PartitionKind.LinuxSwap => "Linux swap",
        PartitionKind.LinuxLvm => "Linux LVM",
        PartitionKind.MbrExtended => "Phân vùng mở rộng MBR (Extended)",
        _ => "Không rõ",
    };
}
