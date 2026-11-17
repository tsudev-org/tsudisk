using TsuDisk.Core.Conversion;
using TsuDisk.Core.Model;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

public class Mbr2GptTests
{
    private static PartitionInfo Mbr(int number, long offset, long size, byte type, bool system = false, bool boot = false, char? letter = null) =>
        new(0, number, offset, size, null, type, letter, system, boot, "NTFS", null, null);

    /// <summary>Bo cuc MBR cai Windows mac dinh: System Reserved (active) | C: | Recovery | 1 MB trong cuoi.</summary>
    private static DiskLayout MbrWindows(params PartitionInfo[] extra) => new(Disk(100 * Units.GiB, PartitionStyle.Mbr),
    [
        Mbr(1, Units.MiB, 50 * Units.MiB, PartitionTypes.MbrIfs, system: true),
        Mbr(2, 51 * Units.MiB, 90 * Units.GiB, PartitionTypes.MbrIfs, boot: true, letter: 'C'),
        Mbr(3, 51 * Units.MiB + 90 * Units.GiB, 600 * Units.MiB, PartitionTypes.MbrRecovery),
        .. extra,
    ]);

    [Fact]
    public void Arguments_are_always_validate_never_convert()
    {
        var args = Mbr2GptCheck.ValidateArguments(3);
        Assert.Equal(["/validate", "/disk:3", "/allowFullOS"], args);
        Assert.DoesNotContain(args, a => a.Contains("convert", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mbr2GptCheck.ValidateArguments(-1));
    }

    [Fact]
    public void Gpt_and_raw_disks_are_not_applicable()
    {
        Assert.Contains("đã là GPT", Mbr2GptCheck.NotApplicableReason(Disk()), StringComparison.Ordinal);
        Assert.Contains("RAW", Mbr2GptCheck.NotApplicableReason(Disk(style: PartitionStyle.Unknown)), StringComparison.Ordinal);
        Assert.Null(Mbr2GptCheck.NotApplicableReason(Disk(style: PartitionStyle.Mbr)));
        Assert.Empty(Mbr2GptCheck.PreCheck(RealMachine20261116()));
    }

    [Fact]
    public void Default_windows_mbr_layout_passes_precheck()
    {
        Assert.Empty(Mbr2GptCheck.PreCheck(MbrWindows()));
    }

    [Fact]
    public void Fourth_primary_partition_blocks()
    {
        var layout = MbrWindows(Mbr(4, 95 * Units.GiB, 2 * Units.GiB, PartitionTypes.MbrIfs, letter: 'D'));
        var f = Assert.Single(Mbr2GptCheck.PreCheck(layout));
        Assert.True(f.Blocking);
        Assert.Contains("4 phân vùng chính", f.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Extended_partition_blocks_without_double_counting()
    {
        var layout = MbrWindows(Mbr(4, 95 * Units.GiB, 2 * Units.GiB, 0x0F));
        var f = Assert.Single(Mbr2GptCheck.PreCheck(layout));
        Assert.Contains("Extended", f.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Data_disk_without_windows_blocks()
    {
        var layout = new DiskLayout(Disk(10 * Units.GiB, PartitionStyle.Mbr), [Mbr(1, Units.MiB, 5 * Units.GiB, PartitionTypes.MbrIfs, letter: 'E')]);
        var findings = Mbr2GptCheck.PreCheck(layout);
        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.True(f.Blocking));
    }

    [Fact]
    public void Gpt_structures_need_room_at_both_ends()
    {
        var size = 10 * Units.GiB;
        var layout = new DiskLayout(Disk(size, PartitionStyle.Mbr),
        [
            Mbr(1, 512, 50 * Units.MiB, PartitionTypes.MbrIfs, system: true),
            Mbr(2, 50 * Units.MiB + 512, size - 50 * Units.MiB - 1024, PartitionTypes.MbrIfs, boot: true, letter: 'C'),
        ]);
        var messages = Mbr2GptCheck.PreCheck(layout).Select(f => f.Message).ToList();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Contains("đầu đĩa", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Contains("cuối đĩa", StringComparison.Ordinal));
        Assert.Equal(16 * 1024 + 1024, Mbr2GptCheck.RequiredLeadingBytes(512));
        Assert.Equal(16 * 1024 + 4096, Mbr2GptCheck.RequiredTrailingBytes(4096));
    }

    [Fact]
    public void Exit_zero_passes_and_demotes_precheck_to_notes()
    {
        var layout = MbrWindows(Mbr(4, 95 * Units.GiB, 2 * Units.GiB, PartitionTypes.MbrIfs));
        var report = Mbr2GptCheck.Interpret(layout, 0, "MBR2GPT: Validation completed successfully", []);
        Assert.Equal(Mbr2GptVerdict.Pass, report.Verdict);
        Assert.All(report.Findings, f => Assert.False(f.Blocking));
    }

    [Fact]
    public void Failure_maps_exit_code_and_log_keywords_to_vietnamese_hints()
    {
        var log = new[] { "2026-11-17 10:00:00, Error MBR2GPT: Cannot find OS partition(s) for disk 2" };
        var report = Mbr2GptCheck.Interpret(MbrWindows(), 5, "MBR2GPT: Disk layout validation failed for disk 2", log);
        Assert.Equal(Mbr2GptVerdict.Fail, report.Verdict);
        Assert.Equal(5, report.ToolExitCode);
        Assert.Contains(report.Findings, f => f.Blocking && f.Message.Contains("hình học", StringComparison.Ordinal));
        Assert.Contains(report.Findings, f => f.Message.Contains("không tìm thấy Windows", StringComparison.Ordinal));
        Assert.Equal(log, report.ToolLog);
    }

    [Fact]
    public void Normal_log_words_do_not_produce_false_hints()
    {
        var report = Mbr2GptCheck.Interpret(MbrWindows(), 7, "MBR2GPT: Validating layout, disk logical sector size is: 512 bytes", []);
        Assert.Single(report.Findings);
        Assert.Contains("không đáp ứng", report.Findings[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bitlocker_exit_code_is_explained()
    {
        Assert.Contains("BitLocker", Mbr2GptCheck.DescribeExitCode(6), StringComparison.Ordinal);
        Assert.Contains("999", Mbr2GptCheck.DescribeExitCode(999), StringComparison.Ordinal);
    }

    [Fact]
    public void New_log_lines_keep_only_appended_mbr2gpt_lines()
    {
        const string before = "old MBR2GPT: stale\r\n";
        var after = before + "Info Other: noise\r\nInfo MBR2GPT: Attempting to validate disk 1\r\nError MBR2GPT: Cannot find OS partition(s) for disk 1\r\n";
        Assert.Equal(["Info MBR2GPT: Attempting to validate disk 1", "Error MBR2GPT: Cannot find OS partition(s) for disk 1"],
            Mbr2GptCheck.NewLogLines(before, after));
        // Tep bi xoay vong (noi dung moi khong bat dau bang noi dung cu): lay toan bo, cat con maxLines dong cuoi.
        var rotated = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"MBR2GPT: {i}"));
        var lines = Mbr2GptCheck.NewLogLines(before, rotated, maxLines: 10);
        Assert.Equal(10, lines.Count);
        Assert.Equal("MBR2GPT: 49", lines[^1]);
    }

    [Fact]
    public void Report_serializes_with_string_verdict()
    {
        var json = Serialization.TsuDiskJson.Serialize([Mbr2GptCheck.Interpret(MbrWindows(), 7, "", [])]);
        Assert.Contains("\"verdict\": \"Fail\"", json, StringComparison.Ordinal);
        Assert.Contains("\"toolExitCode\": 7", json, StringComparison.Ordinal);
    }
}
