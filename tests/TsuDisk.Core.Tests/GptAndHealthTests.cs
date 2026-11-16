using System.Buffers.Binary;
using System.Text;
using TsuDisk.Core.Gpt;
using TsuDisk.Core.Health;
using TsuDisk.Core.Model;

namespace TsuDisk.Core.Tests;

/// <summary>Dung anh GPT dung chuan UEFI (protective MBR, 2 header, 2 bang 128 entry) de test bo kiem tra.</summary>
internal static class GptImageBuilder
{
    public const int Sector = 512;

    public static GptImage Build(long diskBytes, IEnumerable<(Guid Type, long Offset, long Size, string Name)> parts)
    {
        var lastLba = (diskBytes / Sector) - 1;
        var entries = new byte[128 * 128];
        var i = 0;
        foreach (var (type, offset, size, name) in parts)
        {
            var e = entries.AsSpan(i * 128, 128);
            type.TryWriteBytes(e);
            Guid.NewGuid().TryWriteBytes(e[16..]);
            BinaryPrimitives.WriteUInt64LittleEndian(e[32..], (ulong)(offset / Sector));
            BinaryPrimitives.WriteUInt64LittleEndian(e[40..], (ulong)(((offset + size) / Sector) - 1));
            Encoding.Unicode.GetBytes(name).CopyTo(e[56..]);
            i++;
        }
        var entriesCrc = Crc32.Compute(entries);
        var diskGuid = Guid.NewGuid();

        var head = new byte[34 * Sector];
        head[510] = 0x55; head[511] = 0xAA; head[446 + 4] = 0xEE;
        WriteHeader(head.AsSpan(Sector, Sector), 1, lastLba, 2, lastLba, diskGuid, entriesCrc);
        entries.CopyTo(head, 2 * Sector);

        var tail = new byte[33 * Sector];
        entries.CopyTo(tail, 0);
        WriteHeader(tail.AsSpan(32 * Sector, Sector), lastLba, 1, lastLba - 32, lastLba, diskGuid, entriesCrc);
        return new GptImage(Sector, diskBytes, head, tail);
    }

    public static void WriteHeader(Span<byte> s, long my, long alt, long entriesLba, long lastLba, Guid disk, uint entriesCrc)
    {
        s.Clear();
        Encoding.ASCII.GetBytes(GptParser.Signature).CopyTo(s);
        BinaryPrimitives.WriteUInt32LittleEndian(s[8..], 0x00010000);
        BinaryPrimitives.WriteUInt32LittleEndian(s[12..], 92);
        BinaryPrimitives.WriteUInt64LittleEndian(s[24..], (ulong)my);
        BinaryPrimitives.WriteUInt64LittleEndian(s[32..], (ulong)alt);
        BinaryPrimitives.WriteUInt64LittleEndian(s[40..], 34);
        BinaryPrimitives.WriteUInt64LittleEndian(s[48..], (ulong)(lastLba - 33));
        disk.TryWriteBytes(s[56..]);
        BinaryPrimitives.WriteUInt64LittleEndian(s[72..], (ulong)entriesLba);
        BinaryPrimitives.WriteUInt32LittleEndian(s[80..], 128);
        BinaryPrimitives.WriteUInt32LittleEndian(s[84..], 128);
        BinaryPrimitives.WriteUInt32LittleEndian(s[88..], entriesCrc);
        BinaryPrimitives.WriteUInt32LittleEndian(s[16..], Crc32.Compute(s[..92]));
    }
}

public class GptVerifierTests
{
    private const long Disk = 4L * 1024 * 1024 * 1024;

    private static (GptImage Image, DiskLayout Layout) Sample()
    {
        var parts = new (Guid, long, long, string)[]
        {
            (PartitionTypes.EfiSystem, Units.MiB, 100 * Units.MiB, "EFI system partition"),
            (PartitionTypes.BasicData, 101 * Units.MiB, 2 * Units.GiB, "Basic data partition"),
            (PartitionTypes.Recovery, 101 * Units.MiB + 2 * Units.GiB, 512 * Units.MiB, "Recovery"),
        };
        var layout = new DiskLayout(Fixtures.Disk(Disk),
            [.. parts.Select((p, i) => Fixtures.Part(i + 1, p.Item2, p.Item3, p.Item1))]);
        return (GptImageBuilder.Build(Disk, parts), layout);
    }

    [Fact]
    public void Crc32_matches_the_standard_check_value() =>
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));

    [Fact]
    public void Valid_gpt_matching_windows_layout_is_healthy()
    {
        var (image, layout) = Sample();

        var report = GptVerifier.Verify(image, layout);

        Assert.True(report.Healthy, string.Join(" | ", report.Findings.Select(f => f.Message)));
        Assert.Equal(3, report.Entries.Count);
        Assert.Equal("Recovery", report.Entries[2].Name);
        Assert.Single(report.Findings, f => f.Level == GptCheckLevel.Ok);
    }

    [Fact]
    public void Corrupted_primary_header_crc_is_an_error()
    {
        var (image, _) = Sample();
        image.Head[512 + 40] ^= 0xFF;   // doi FirstUsableLBA ma khong tinh lai CRC

        Assert.Contains(GptVerifier.Verify(image).Findings, f => f.Message.Contains("header GPT chính sai", StringComparison.Ordinal));
    }

    [Fact]
    public void Corrupted_entry_table_is_an_error()
    {
        var (image, _) = Sample();
        image.Head[2 * 512 + 60] ^= 0x01;   // sua ten entry 1 trong bang chinh

        var findings = GptVerifier.Verify(image).Findings;

        Assert.Contains(findings, f => f.Message.Contains("bảng entry GPT chính sai", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Message.Contains("dự phòng khác", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_backup_header_is_an_error()
    {
        var (image, _) = Sample();
        Array.Clear(image.Tail, 32 * 512, 512);

        Assert.False(GptVerifier.Verify(image).Healthy);
    }

    [Fact]
    public void Missing_protective_mbr_is_an_error()
    {
        var (image, _) = Sample();
        image.Head[510] = 0;

        Assert.Contains(GptVerifier.Verify(image).Findings, f => f.Message.Contains("protective MBR", StringComparison.Ordinal));
    }

    [Fact]
    public void Windows_reporting_a_partition_absent_on_disk_is_an_error()
    {
        var (image, layout) = Sample();
        var extra = layout with { Partitions = [.. layout.Partitions, Fixtures.Part(9, 3 * Units.GiB, Units.GiB, PartitionTypes.BasicData)] };

        Assert.Contains(GptVerifier.Verify(image, extra).Findings, f => f.Level == GptCheckLevel.Error && f.Message.Contains("không có entry", StringComparison.Ordinal));
    }

    [Fact]
    public void Overlapping_entries_are_detected()
    {
        var parts = new (Guid, long, long, string)[]
        {
            (PartitionTypes.BasicData, Units.MiB, Units.GiB, "A"),
            (PartitionTypes.BasicData, Units.GiB, Units.GiB, "B"),
        };

        Assert.Contains(GptVerifier.Verify(GptImageBuilder.Build(Disk, parts)).Findings, f => f.Message.Contains("chồng lấn", StringComparison.Ordinal));
    }

    [Fact]
    public void Garbage_input_never_throws()
    {
        var random = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            var head = new byte[34 * 512];
            var tail = new byte[33 * 512];
            random.NextBytes(head);
            random.NextBytes(tail);
            Encoding.ASCII.GetBytes(GptParser.Signature).CopyTo(head, 512);   // qua cua chu ky de vao sau hon
            var report = GptVerifier.Verify(new GptImage(512, Disk, head, tail));
            Assert.False(report.Healthy);
        }
    }
}

public class HealthEvaluatorTests
{
    private static DiskHealth Ok() => new(0, "SSD", "SSD", "Healthy", ["OK"], 40, 70, 5, 0, 0, 1000);

    [Fact]
    public void Healthy_disk_is_good() => Assert.Equal(HealthLevel.Good, HealthEvaluator.Evaluate(Ok()).Level);

    [Fact]
    public void Missing_data_is_unknown_and_still_allows_writes()
    {
        var verdict = HealthEvaluator.Evaluate(null);
        Assert.Equal(HealthLevel.Unknown, verdict.Level);
        Assert.True(verdict.AllowsWrites);
    }

    [Theory]
    [InlineData("Unhealthy")]
    public void Unhealthy_status_blocks_writes(string status) =>
        Assert.False(HealthEvaluator.Evaluate(Ok() with { HealthStatus = status }).AllowsWrites);

    [Fact]
    public void Predictive_failure_blocks_writes() =>
        Assert.False(HealthEvaluator.Evaluate(Ok() with { OperationalStatus = ["Predictive Failure"] }).AllowsWrites);

    [Fact]
    public void Uncorrected_read_errors_block_writes() =>
        Assert.Equal(HealthLevel.Bad, HealthEvaluator.Evaluate(Ok() with { ReadErrorsUncorrected = 3 }).Level);

    [Fact]
    public void Worn_out_ssd_blocks_writes_and_high_wear_warns()
    {
        Assert.Equal(HealthLevel.Bad, HealthEvaluator.Evaluate(Ok() with { WearPercent = 100 }).Level);
        Assert.Equal(HealthLevel.Warning, HealthEvaluator.Evaluate(Ok() with { WearPercent = 85 }).Level);
    }

    [Fact]
    public void Hot_disk_warns_but_allows_writes()
    {
        var verdict = HealthEvaluator.Evaluate(Ok() with { TemperatureCelsius = 72 });
        Assert.Equal(HealthLevel.Warning, verdict.Level);
        Assert.True(verdict.AllowsWrites);
    }
}
