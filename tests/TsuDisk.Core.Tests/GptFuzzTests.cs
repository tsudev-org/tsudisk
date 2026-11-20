using System.Buffers.Binary;
using TsuDisk.Core.Gpt;
using TsuDisk.Core.Model;

namespace TsuDisk.Core.Tests;

/// <summary>
/// Fuzz co hieu cau truc cho bo kiem GPT - dau vao KHONG tin cay doc tho tu dia. Khac Garbage_input_never_throws (rac
/// ngau nhien thuong dung o CRC), o day dot bien truong header/entry cua anh HOP LE roi TINH LAI CRC de di sau vao
/// phan tich bang entry, chong lan, header du phong va doi chieu bo cuc Windows. Tinh chat: Verify() khong bao gio
/// nem, va anh da bi doi khac goc khong bao gio duoc coi la khoe ma lai sai lech voi bo cuc Windows.
/// Seed co dinh => lap lai duoc; doi seed: bien moi truong TSUDISK_FUZZ_SEED.
/// </summary>
public class GptFuzzTests
{
    private const long Disk = 4L * 1024 * 1024 * 1024;
    private const int Ss = GptImageBuilder.Sector;
    private const int Rounds = 5000;

    // Offset cac truong header GPT (UEFI 2.10 bang 5-5) va entry (bang 5-6).
    private static readonly int[] HeaderFields = [8, 12, 24, 32, 40, 48, 72, 80, 84];
    private static readonly int[] EntryFields = [0, 32, 40, 48];

    private static int Seed =>
        int.TryParse(Environment.GetEnvironmentVariable("TSUDISK_FUZZ_SEED"), out var s) ? s : 20261120;

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

    private static ulong Interesting(Random r) => r.Next(8) switch
    {
        0 => 0,
        1 => ulong.MaxValue,
        2 => long.MaxValue,
        3 => (ulong)r.Next(0, 64),
        4 => (Disk / Ss) - 1 + (ulong)r.Next(-40, 40),
        5 => uint.MaxValue,
        6 => 1UL << r.Next(0, 63),
        _ => (ulong)r.NextInt64(),
    };

    /// <summary>Ghi 4 hoac 8 byte gia tri "thu vi" vao mot truong.</summary>
    private static void Poke(Span<byte> s, int offset, Random r)
    {
        var v = Interesting(r);
        if (offset is 8 or 12 or 80 or 84)
            BinaryPrimitives.WriteUInt32LittleEndian(s[offset..], (uint)v);
        else
            BinaryPrimitives.WriteUInt64LittleEndian(s[offset..], v);
    }

    /// <summary>Tinh lai CRC bang entry (theo so entry khai trong header neu hop ly) va CRC header.</summary>
    private static void Reseal(Span<byte> header, ReadOnlySpan<byte> entries)
    {
        var count = BinaryPrimitives.ReadUInt32LittleEndian(header[80..]);
        var size = BinaryPrimitives.ReadUInt32LittleEndian(header[84..]);
        var bytes = (long)count * size;
        if (bytes > 0 && bytes <= entries.Length)
            BinaryPrimitives.WriteUInt32LittleEndian(header[88..], Crc32.Compute(entries[..(int)bytes]));
        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        var crcLen = headerSize is >= 92 and <= Ss ? (int)headerSize : 92;
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], Crc32.Compute(header[..crcLen]));
    }

    [Fact]
    public void Mutated_and_resealed_gpt_never_throws()
    {
        var random = new Random(Seed);
        var (pristine, layout) = Sample();
        for (var i = 0; i < Rounds; i++)
        {
            var head = (byte[])pristine.Head.Clone();
            var tail = (byte[])pristine.Tail.Clone();
            var primary = head.AsSpan(Ss, Ss);
            var backup = tail.AsSpan(32 * Ss, Ss);
            var primaryEntries = head.AsSpan(2 * Ss, 32 * Ss);
            var backupEntries = tail.AsSpan(0, 32 * Ss);

            for (var m = 0; m < 1 + random.Next(4); m++)
            {
                switch (random.Next(5))
                {
                    case 0:
                        Poke(primary, HeaderFields[random.Next(HeaderFields.Length)], random);
                        break;
                    case 1:
                        Poke(backup, HeaderFields[random.Next(HeaderFields.Length)], random);
                        break;
                    case 2:
                        Poke(primaryEntries[(random.Next(8) * 128)..], EntryFields[random.Next(EntryFields.Length)], random);
                        break;
                    case 3:
                        Poke(backupEntries[(random.Next(8) * 128)..], EntryFields[random.Next(EntryFields.Length)], random);
                        break;
                    default:
                        head[random.Next(head.Length)] ^= (byte)(1 << random.Next(8));
                        break;
                }
            }
            if (random.Next(4) != 0)
            {
                Reseal(primary, primaryEntries);
                Reseal(backup, backupEntries);
            }

            var image = new GptImage(Ss, Disk, head, tail);
            var mutated = !head.AsSpan().SequenceEqual(pristine.Head) || !tail.AsSpan().SequenceEqual(pristine.Tail);
            var withLayout = Record.Exception(() => GptVerifier.Verify(image, layout));
            var withoutLayout = Record.Exception(() => GptVerifier.Verify(image));
            Assert.True(withLayout is null, $"seed {Seed} vong {i}: {withLayout}");
            Assert.True(withoutLayout is null, $"seed {Seed} vong {i}: {withoutLayout}");

            var report = GptVerifier.Verify(image, layout);
            if (mutated && report.Healthy)
            {
                // Chi chap nhan "khoe" khi entry doc duoc van khop dung bo cuc Windows (vd. doi truong khong anh huong).
                Assert.Equal(layout.Partitions.Count, report.Entries.Count(e => e.TypeGuid != Guid.Empty));
            }
        }
    }

    [Theory]
    [InlineData(uint.MaxValue, 128u)]      // so entry tran int
    [InlineData(128u, uint.MaxValue)]      // kich thuoc entry tran int
    [InlineData(0x01000000u, 0x100u)]      // bang 4 GiB
    [InlineData(128u, 200u)]               // khong phai 128 x 2^n
    public void Absurd_entry_table_size_is_an_error_not_a_crash(uint count, uint size)
    {
        // Hoi quy: truoc ban sua, Verify() nem ArgumentOutOfRangeException (tran so trong EntriesBytes/GptImage.Lba).
        var (image, layout) = Sample();
        var primary = image.Head.AsSpan(Ss, Ss);
        BinaryPrimitives.WriteUInt32LittleEndian(primary[80..], count);
        BinaryPrimitives.WriteUInt32LittleEndian(primary[84..], size);
        Reseal(primary, image.Head.AsSpan(2 * Ss, 32 * Ss));

        var report = GptVerifier.Verify(image, layout);

        Assert.False(report.Healthy);
        Assert.Contains(report.Findings, f => f.Message.Contains("bảng entry vô lý", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(long.MaxValue, 1)]
    [InlineData(-1L, 1)]
    [InlineData(0L, -5)]
    [InlineData(0L, int.MaxValue)]
    public void Out_of_range_lba_reads_are_empty(long lba, int count) =>
        Assert.True(Sample().Image.Lba(lba, count).IsEmpty);
}
