using TsuDisk.Core.Model;

namespace TsuDisk.Core.Gpt;

/// <summary>Anh tho vung GPT: dau dia (LBA 0..33) va cuoi dia (33 sector cuoi + header du phong).</summary>
public sealed record GptImage(int SectorSize, long DiskSizeBytes, byte[] Head, byte[] Tail)
{
    /// <summary>LBA dau tien chua trong <see cref="Tail"/>.</summary>
    public long TailFirstLba => (DiskSizeBytes / SectorSize) - (Tail.Length / SectorSize);

    /// <summary>
    /// Doc <paramref name="count"/> sector tu <paramref name="lba"/>; ngoai vung da doc (hoac tham so vo ly tu header
    /// hong: am, qua lon, tran so) thi tra rong - khong bao gio nem.
    /// </summary>
    public ReadOnlySpan<byte> Lba(long lba, int count = 1)
    {
        if (lba < 0 || count <= 0 || SectorSize <= 0)
            return [];
        long headSectors = Head.Length / SectorSize;
        if (lba < headSectors && count <= headSectors - lba)
            return Head.AsSpan((int)lba * SectorSize, count * SectorSize);
        long tailSectors = Tail.Length / SectorSize;
        var rel = lba - TailFirstLba;
        if (rel >= 0 && rel < tailSectors && count <= tailSectors - rel)
            return Tail.AsSpan((int)rel * SectorSize, count * SectorSize);
        return [];
    }
}

public enum GptCheckLevel
{
    Ok,
    Warning,
    Error,
}

public sealed record GptFinding(GptCheckLevel Level, string Message);

public sealed record GptReport(GptHeader? Primary, GptHeader? Backup, IReadOnlyList<GptEntry> Entries, IReadOnlyList<GptFinding> Findings)
{
    public bool Healthy => Findings.All(f => f.Level != GptCheckLevel.Error);
}

/// <summary>
/// Kiem tra toan ven GPT kieu `gdisk v`: protective MBR, chu ky, CRC header, CRC bang entry, header du phong doi xung
/// voi header chinh, bang entry chinh va du phong giong nhau, entry nam trong vung dung duoc va khong chong lan, va
/// (neu co) khop voi bo cuc Windows bao qua Storage API.
/// </summary>
public static class GptVerifier
{
    public static GptReport Verify(GptImage image, DiskLayout? windowsLayout = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var findings = new List<GptFinding>();
        void Add(GptCheckLevel level, string message) => findings.Add(new GptFinding(level, message));
        var ss = image.SectorSize;
        var lastLba = (image.DiskSizeBytes / ss) - 1;

        if (!GptParser.HasProtectiveMbr(image.Lba(0)))
            Add(GptCheckLevel.Error, "LBA 0 không có protective MBR hợp lệ (chữ ký 0x55AA + mục loại 0xEE).");

        var primary = GptParser.ParseHeader(image.Lba(1));
        if (primary is null)
        {
            Add(GptCheckLevel.Error, "Không đọc được header GPT chính ở LBA 1 (sai chữ ký \"EFI PART\" hoặc kích thước).");
        }
        else
        {
            if (!primary.HeaderCrcValid) Add(GptCheckLevel.Error, "CRC32 của header GPT chính sai.");
            if (primary.MyLba != 1) Add(GptCheckLevel.Error, $"Header chính ghi MyLBA = {primary.MyLba}, phải là 1.");
            if (primary.AlternateLba != lastLba)
                Add(GptCheckLevel.Warning, $"Header dự phòng được khai ở LBA {primary.AlternateLba}, không phải sector cuối ({lastLba}) - đĩa có thể đã đổi kích thước.");
        }

        var backup = primary is null ? null : GptParser.ParseHeader(image.Lba(primary.AlternateLba));
        if (primary is not null && backup is null)
            Add(GptCheckLevel.Error, $"Không đọc được header GPT dự phòng ở LBA {primary.AlternateLba}.");
        if (backup is not null)
        {
            if (!backup.HeaderCrcValid) Add(GptCheckLevel.Error, "CRC32 của header GPT dự phòng sai.");
            if (primary is not null && (backup.MyLba != primary.AlternateLba || backup.AlternateLba != primary.MyLba))
                Add(GptCheckLevel.Error, "Header dự phòng không trỏ ngược về header chính (MyLBA/AlternateLBA lệch).");
            if (primary is not null && (backup.DiskGuid != primary.DiskGuid || backup.FirstUsableLba != primary.FirstUsableLba
                || backup.LastUsableLba != primary.LastUsableLba || backup.EntryCount != primary.EntryCount || backup.EntrySize != primary.EntrySize))
                Add(GptCheckLevel.Error, "Header dự phòng khác header chính (GUID đĩa, vùng dùng được hoặc kích thước bảng).");
        }

        IReadOnlyList<GptEntry> entries = [];
        if (primary is not null)
        {
            var bytes = GptParser.EntriesBytes(primary, ss);
            var primaryTable = bytes > 0 ? image.Lba(primary.EntriesLba, bytes / ss) : [];
            if (bytes <= 0)
            {
                Add(GptCheckLevel.Error, $"Header GPT chính khai bảng entry vô lý ({primary.EntryCount} entry × {primary.EntrySize} byte).");
            }
            else if (primaryTable.IsEmpty)
            {
                Add(GptCheckLevel.Error, "Không đọc được bảng entry GPT chính.");
            }
            else
            {
                var used = primaryTable[..(primary.EntryCount * primary.EntrySize)];
                if (Crc32.Compute(used) != primary.EntriesCrc) Add(GptCheckLevel.Error, "CRC32 của bảng entry GPT chính sai.");
                entries = GptParser.ParseEntries(used, primary.EntryCount, primary.EntrySize);
                if (backup is not null)
                {
                    var backupTable = image.Lba(backup.EntriesLba, bytes / ss);
                    if (backupTable.IsEmpty)
                        Add(GptCheckLevel.Error, "Không đọc được bảng entry GPT dự phòng.");
                    else if (!backupTable[..used.Length].SequenceEqual(used))
                        Add(GptCheckLevel.Error, "Bảng entry dự phòng khác bảng entry chính.");
                }
            }

            var ordered = entries.OrderBy(e => e.FirstLba).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var e = ordered[i];
                if (e.FirstLba < primary.FirstUsableLba || e.LastLba > primary.LastUsableLba || e.LastLba < e.FirstLba)
                    Add(GptCheckLevel.Error, $"Entry {e.Index} ({e.Name}) nằm ngoài vùng dùng được.");
                if (i > 0 && ordered[i - 1].LastLba >= e.FirstLba)
                    Add(GptCheckLevel.Error, $"Entry {ordered[i - 1].Index} chồng lấn entry {e.Index}.");
                if (e.FirstLba * ss % Units.PartitionAlignment != 0)
                    Add(GptCheckLevel.Warning, $"Entry {e.Index} ({e.Name}) không căn lề 1 MiB - hiệu năng SSD có thể giảm.");
            }
        }

        if (windowsLayout is not null && primary is not null)
            CompareWithWindows(entries, windowsLayout, ss, Add);

        if (findings.Count == 0)
            Add(GptCheckLevel.Ok, $"GPT toàn vẹn: protective MBR, 2 header, 2 bảng entry khớp CRC32; {entries.Count} phân vùng.");
        return new GptReport(primary, backup, entries, findings);
    }

    private static void CompareWithWindows(IReadOnlyList<GptEntry> entries, DiskLayout layout, int ss, Action<GptCheckLevel, string> add)
    {
        foreach (var p in layout.Partitions)
        {
            var match = entries.FirstOrDefault(e => e.OffsetBytes(ss) == p.Offset);
            if (match is null)
                add(GptCheckLevel.Error, $"Windows báo {p.DisplayName} tại offset {p.Offset} nhưng bảng GPT trên đĩa không có entry đó.");
            else if (match.SizeBytes(ss) != p.Size || (p.GptType is { } t && t != match.TypeGuid))
                add(GptCheckLevel.Error, $"{p.DisplayName}: kích thước/loại trên đĩa khác Windows báo.");
        }
        foreach (var e in entries.Where(e => layout.FindByOffset(e.OffsetBytes(ss)) is null))
            add(GptCheckLevel.Warning, $"Entry {e.Index} ({e.Name}) có trên đĩa nhưng Windows không liệt kê.");
    }
}
