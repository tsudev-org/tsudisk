namespace TsuDisk.Core.Model;

/// <summary>Thong tin mot o dia vat ly (hoac o ao VHD/VHDX).</summary>
public sealed record DiskInfo(
    int Number,
    string FriendlyName,
    long Size,
    int LogicalSectorSize,
    PartitionStyle Style,
    bool IsSystem,
    bool IsBoot,
    bool IsReadOnly,
    bool IsOffline,
    bool IsVirtual,
    string BusType)
{
    /// <summary>Byte dau tien co the dat phan vung: sau MBR bao ve + header + bang GPT (34 sector), hoac sau MBR (1 sector).</summary>
    public long FirstUsableByte => (Style == PartitionStyle.Gpt ? 34L : 1L) * Math.Max(LogicalSectorSize, 512);

    /// <summary>Byte sau cung (khong bao gom) co the dat phan vung. GPT giu 33 sector cuoi cho header du phong.</summary>
    public long LastUsableByteExclusive => Style == PartitionStyle.Gpt
        ? Size - (33L * Math.Max(LogicalSectorSize, 512))
        : Size;
}

/// <summary>Mot phan vung. Danh tinh on dinh cua phan vung la <see cref="Offset"/> - so thu tu co the doi tren MBR.</summary>
public sealed record PartitionInfo(
    int DiskNumber,
    int PartitionNumber,
    long Offset,
    long Size,
    Guid? GptType,
    byte? MbrType,
    char? DriveLetter,
    bool IsSystem,
    bool IsBoot,
    string? FileSystem,
    string? Label,
    long? FreeBytes)
{
    public long End => Offset + Size;

    public PartitionKind Kind => GptType is { } g
        ? PartitionTypes.FromGpt(g)
        : MbrType is { } m ? PartitionTypes.FromMbr(m) : PartitionKind.Unknown;

    public string DisplayName => DriveLetter is { } letter
        ? $"{letter}: (phân vùng {PartitionNumber})"
        : $"phân vùng {PartitionNumber} - {PartitionTypes.Describe(Kind)}";
}

/// <summary>Mot doan lien tuc tren o: hoac la phan vung, hoac la vung trong (Unallocated).</summary>
public sealed record Segment(long Offset, long Size, PartitionInfo? Partition)
{
    public long End => Offset + Size;
    public bool IsFree => Partition is null;
}

/// <summary>Anh chup bo cuc mot o tai mot thoi diem. Bat bien.</summary>
public sealed record DiskLayout(DiskInfo Disk, IReadOnlyList<PartitionInfo> Partitions)
{
    public IReadOnlyList<PartitionInfo> Ordered => [.. Partitions.OrderBy(p => p.Offset)];

    /// <summary>Chia o thanh cac doan lien tuc theo thu tu offset; vung trong nho hon 1 MiB bi bo qua.</summary>
    public IReadOnlyList<Segment> Segments()
    {
        var result = new List<Segment>();
        var cursor = Disk.FirstUsableByte;
        foreach (var part in Ordered)
        {
            if (part.Offset - cursor >= Units.MinUsefulGap)
                result.Add(new Segment(cursor, part.Offset - cursor, null));
            result.Add(new Segment(part.Offset, part.Size, part));
            cursor = Math.Max(cursor, part.End);
        }
        var end = Disk.LastUsableByteExclusive;
        if (end - cursor >= Units.MinUsefulGap)
            result.Add(new Segment(cursor, end - cursor, null));
        return result;
    }

    public IReadOnlyList<Segment> FreeExtents() => [.. Segments().Where(s => s.IsFree)];

    public PartitionInfo? FindByLetter(char letter) =>
        Partitions.FirstOrDefault(p => p.DriveLetter is { } l && char.ToUpperInvariant(l) == char.ToUpperInvariant(letter));

    public PartitionInfo? FindByOffset(long offset) => Partitions.FirstOrDefault(p => p.Offset == offset);

    /// <summary>Doan ngay sau phan vung (vung trong hoac phan vung ke tiep), null neu la doan cuoi.</summary>
    public Segment? SegmentAfter(PartitionInfo partition)
    {
        var segments = Segments();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            if (segments[i].Partition?.Offset == partition.Offset)
                return segments[i + 1];
        }
        return null;
    }

    public Segment? SegmentAfter(Segment segment)
    {
        var segments = Segments();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            if (segments[i].Offset == segment.Offset)
                return segments[i + 1];
        }
        return null;
    }

    public long UnallocatedTotal => FreeExtents().Sum(s => s.Size);
}
