using System.Buffers.Binary;
using System.Text;

namespace TsuDisk.Core.Gpt;

/// <summary>Header GPT (UEFI 2.10 muc 5.3.2) da phan tich, kem ket qua kiem CRC.</summary>
public sealed record GptHeader(
    long MyLba,
    long AlternateLba,
    long FirstUsableLba,
    long LastUsableLba,
    Guid DiskGuid,
    long EntriesLba,
    int EntryCount,
    int EntrySize,
    uint EntriesCrc,
    bool HeaderCrcValid);

public sealed record GptEntry(int Index, Guid TypeGuid, Guid UniqueGuid, long FirstLba, long LastLba, ulong Attributes, string Name)
{
    public long OffsetBytes(int sectorSize) => FirstLba * sectorSize;

    public long SizeBytes(int sectorSize) => (LastLba - FirstLba + 1) * sectorSize;
}

/// <summary>
/// Phan tich byte tho cua vung GPT. Thuan (khong I/O): tang Windows doc sector roi dua vao day. Moi loi cau truc tra ve
/// null/thong bao, khong nem ngoai le - dau vao la du lieu tren dia, co the hong bat ky cach nao.
/// </summary>
public static class GptParser
{
    public const string Signature = "EFI PART";
    public const int MinHeaderSize = 92;

    /// <summary>LBA0: protective MBR co chu ky 0x55AA va mot muc loai 0xEE.</summary>
    public static bool HasProtectiveMbr(ReadOnlySpan<byte> lba0)
    {
        if (lba0.Length < 512 || lba0[510] != 0x55 || lba0[511] != 0xAA)
            return false;
        for (var i = 0; i < 4; i++)
        {
            if (lba0[446 + (i * 16) + 4] == 0xEE)
                return true;
        }
        return false;
    }

    public static GptHeader? ParseHeader(ReadOnlySpan<byte> sector)
    {
        if (sector.Length < MinHeaderSize || Encoding.ASCII.GetString(sector[..8]) != Signature)
            return null;
        var headerSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(sector[12..]);
        if (headerSize < MinHeaderSize || headerSize > sector.Length)
            return null;

        var copy = sector[..headerSize].ToArray();
        var storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(copy.AsSpan(16));
        copy.AsSpan(16, 4).Clear();
        var crcValid = Crc32.Compute(copy) == storedCrc;

        return new GptHeader(
            MyLba: (long)BinaryPrimitives.ReadUInt64LittleEndian(sector[24..]),
            AlternateLba: (long)BinaryPrimitives.ReadUInt64LittleEndian(sector[32..]),
            FirstUsableLba: (long)BinaryPrimitives.ReadUInt64LittleEndian(sector[40..]),
            LastUsableLba: (long)BinaryPrimitives.ReadUInt64LittleEndian(sector[48..]),
            DiskGuid: new Guid(sector.Slice(56, 16)),
            EntriesLba: (long)BinaryPrimitives.ReadUInt64LittleEndian(sector[72..]),
            EntryCount: (int)BinaryPrimitives.ReadUInt32LittleEndian(sector[80..]),
            EntrySize: (int)BinaryPrimitives.ReadUInt32LittleEndian(sector[84..]),
            EntriesCrc: BinaryPrimitives.ReadUInt32LittleEndian(sector[88..]),
            HeaderCrcValid: crcValid);
    }

    /// <summary>Doc cac entry dang dung (type GUID khac 0).</summary>
    public static IReadOnlyList<GptEntry> ParseEntries(ReadOnlySpan<byte> entries, int count, int entrySize)
    {
        var result = new List<GptEntry>();
        if (entrySize < 128 || count <= 0)
            return result;
        for (var i = 0; i < count && (i + 1) * entrySize <= entries.Length; i++)
        {
            var e = entries.Slice(i * entrySize, entrySize);
            var type = new Guid(e[..16]);
            if (type == Guid.Empty)
                continue;
            var name = Encoding.Unicode.GetString(e.Slice(56, 72)).TrimEnd('\0');
            result.Add(new GptEntry(
                Index: i + 1,
                TypeGuid: type,
                UniqueGuid: new Guid(e.Slice(16, 16)),
                FirstLba: (long)BinaryPrimitives.ReadUInt64LittleEndian(e[32..]),
                LastLba: (long)BinaryPrimitives.ReadUInt64LittleEndian(e[40..]),
                Attributes: BinaryPrimitives.ReadUInt64LittleEndian(e[48..]),
                Name: name));
        }
        return result;
    }

    /// <summary>So byte can doc cho bang entry (lam tron len theo sector).</summary>
    public static int EntriesBytes(GptHeader header, int sectorSize)
    {
        var raw = (long)header.EntryCount * header.EntrySize;
        return (int)((raw + sectorSize - 1) / sectorSize * sectorSize);
    }
}
