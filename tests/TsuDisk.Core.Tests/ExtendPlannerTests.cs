using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

public class ExtendPlannerTests
{
    [Fact]
    public void Real_machine_C_reproduces_the_proven_operation_byte_for_byte()
    {
        var result = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('C'));

        Assert.True(result.Success, result.Refusal?.Message);
        var plan = result.Plan!;
        Assert.Equal(
            [StepKind.ScanVolume, StepKind.Backup, StepKind.DisableWinRe, StepKind.DeletePartition, StepKind.ExtendPartition,
             StepKind.CreateRecovery, StepKind.EnableWinRe, StepKind.VerifyLayout],
            plan.Steps.Select(s => s.Kind));

        // So do sau khi chay that ngay 16/11/2026 (partitions-after-20261116-192422.txt).
        var extend = plan.Steps.Single(s => s.Kind == StepKind.ExtendPartition);
        Assert.Equal(348_975_529_984, extend.Size);
        var create = plan.Steps.Single(s => s.Kind == StepKind.CreateRecovery);
        Assert.Equal(349_203_070_976, create.Offset);
        Assert.Equal(1_073_741_824, create.Size);
        Assert.Equal(348_975_529_984 - 284_663_218_176, plan.GainBytes);
    }

    [Fact]
    public void Real_machine_keeps_G_and_E_untouched_in_expected_layout()
    {
        var plan = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('C')).Plan!;

        foreach (var letter in new[] { 'G', 'E' })
        {
            var before = plan.Before.FindByLetter(letter)!;
            var after = plan.ExpectedAfter.FindByLetter(letter)!;
            Assert.Equal(before.Offset, after.Offset);
            Assert.Equal(before.Size, after.Size);
        }
        Assert.Contains(plan.ExpectedAfter.Partitions, p => p.Kind == PartitionKind.Recovery && p.Offset == 349_203_070_976);
    }

    [Fact]
    public void Trailing_737MB_after_E_extends_E_directly()
    {
        var result = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('E'));

        Assert.True(result.Success, result.Refusal?.Message);
        Assert.DoesNotContain(result.Plan!.Steps, s => s.Kind is StepKind.DeletePartition or StepKind.DisableWinRe);
        Assert.InRange(result.Plan.GainBytes, 736 * Units.MiB, 738 * Units.MiB);
        var extend = result.Plan.Steps.Single(s => s.Kind == StepKind.ExtendPartition);
        Assert.True(Units.IsAligned(extend.Offset!.Value + extend.Size!.Value, Units.MiB));
    }

    [Fact]
    public void G_is_refused_because_E_blocks_it_and_hints_explain_alternatives()
    {
        var result = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('G'));

        Assert.False(result.Success);
        Assert.Equal(RefusalCode.NotAdjacent, result.Refusal!.Code);
        Assert.Contains(result.Refusal.Hints, h => h.Contains("tsudisk mo-rong C:", StringComparison.Ordinal));
        Assert.Contains(result.Refusal.Hints, h => h.Contains("tsudisk mo-rong E:", StringComparison.Ordinal));
    }

    [Fact]
    public void Direct_gap_plan_has_no_recovery_steps()
    {
        var result = ExtendPlanner.Plan(DirectGap(), WinReStatus.Disabled, new ExtendRequest('c'));

        Assert.True(result.Success);
        Assert.Equal([StepKind.ScanVolume, StepKind.Backup, StepKind.ExtendPartition, StepKind.VerifyLayout],
            result.Plan!.Steps.Select(s => s.Kind));
        Assert.Equal(10 * Units.GiB, result.Plan.GainBytes);
    }

    [Fact]
    public void Recovery_without_active_WinRE_is_refused_by_default()
    {
        var result = ExtendPlanner.Plan(RealMachine20261116(), WinReStatus.Disabled, new ExtendRequest('C'));

        Assert.Equal(RefusalCode.RecoveryNotWinRe, result.Refusal!.Code);
    }

    [Fact]
    public void Recovery_without_WinRE_can_be_recreated_empty_when_user_opts_in()
    {
        var request = new ExtendRequest('C') { RecoveryPolicy = RecoveryPolicy.RecreateEmpty };
        var result = ExtendPlanner.Plan(RealMachine20261116(), WinReStatus.Disabled, request);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Plan!.Steps, s => s.Kind is StepKind.DisableWinRe or StepKind.EnableWinRe);
        Assert.Contains(result.Plan.Steps, s => s.Kind == StepKind.CreateRecovery);
    }

    [Fact]
    public void WinRE_on_another_disk_does_not_count_as_this_recovery()
    {
        var result = ExtendPlanner.Plan(RealMachine20261116(), new WinReStatus(true, 1, 4), new ExtendRequest('C'));

        Assert.Equal(RefusalCode.RecoveryNotWinRe, result.Refusal!.Code);
    }

    [Theory]
    [InlineData("FAT32", RefusalCode.UnsupportedFileSystem)]
    [InlineData("exFAT", RefusalCode.UnsupportedFileSystem)]
    [InlineData(null, RefusalCode.UnsupportedFileSystem)]
    public void Non_extendable_file_systems_are_refused(string? fileSystem, RefusalCode expected)
    {
        var layout = DirectGap();
        var c = layout.FindByLetter('C')!;
        layout = layout with { Partitions = [.. layout.Partitions.Select(p => p == c ? p with { FileSystem = fileSystem } : p)] };

        Assert.Equal(expected, ExtendPlanner.Plan(layout, WinReStatus.Disabled, new ExtendRequest('C')).Refusal!.Code);
    }

    [Fact]
    public void Dynamic_disk_is_refused()
    {
        var layout = DirectGap();
        layout = layout with { Partitions = [.. layout.Partitions, Part(9, 95 * Units.GiB, Units.MiB, PartitionTypes.LdmMetadata)] };

        Assert.Equal(RefusalCode.DynamicDisk, ExtendPlanner.Plan(layout, WinReStatus.Disabled, new ExtendRequest('C')).Refusal!.Code);
    }

    [Fact]
    public void Read_only_disk_is_refused()
    {
        var layout = DirectGap() with { Disk = Disk(100 * Units.GiB, readOnly: true) };

        Assert.Equal(RefusalCode.DiskNotWritable, ExtendPlanner.Plan(layout, WinReStatus.Disabled, new ExtendRequest('C')).Refusal!.Code);
    }

    [Fact]
    public void Virtual_only_mode_refuses_physical_disks_and_accepts_virtual_ones()
    {
        var request = new ExtendRequest('C') { VirtualDiskOnly = true };

        Assert.Equal(RefusalCode.DiskNotWritable, ExtendPlanner.Plan(DirectGap(), WinReStatus.Disabled, request).Refusal!.Code);
        var virtualLayout = DirectGap() with { Disk = Disk(100 * Units.GiB, isVirtual: true) };
        Assert.True(ExtendPlanner.Plan(virtualLayout, WinReStatus.Disabled, request).Success);
    }

    [Fact]
    public void Missing_drive_letter_is_reported()
    {
        Assert.Equal(RefusalCode.TargetNotFound, ExtendPlanner.Plan(DirectGap(), WinReStatus.Disabled, new ExtendRequest('Z')).Refusal!.Code);
    }

    [Theory]
    [InlineData(100L * 1024 * 1024)]
    [InlineData(8L * 1024 * 1024 * 1024)]
    public void Recovery_size_outside_bounds_is_refused(long size)
    {
        var request = new ExtendRequest('C') { RecoverySize = size };

        Assert.Equal(RefusalCode.InvalidOption, ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, request).Refusal!.Code);
    }

    [Fact]
    public void Gain_below_threshold_is_refused()
    {
        var layout = new DiskLayout(Disk(100 * Units.GiB),
        [
            Part(1, Units.MiB, 10 * Units.GiB, PartitionTypes.BasicData, 'C', "NTFS"),
            Part(2, Units.MiB + 10 * Units.GiB + 8 * Units.MiB, 10 * Units.GiB, PartitionTypes.BasicData, 'D', "NTFS"),
        ]);

        Assert.Equal(RefusalCode.NothingToGain, ExtendPlanner.Plan(layout, WinReStatus.Disabled, new ExtendRequest('C')).Refusal!.Code);
    }

    [Fact]
    public void Recovery_never_shrinks_below_its_current_size()
    {
        var request = new ExtendRequest('C') { RecoverySize = 600 * Units.MiB };
        var plan = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, request).Plan!;

        Assert.True(plan.Steps.Single(s => s.Kind == StepKind.CreateRecovery).Size >= 961_544_192);
    }

    [Fact]
    public void Mbr_disk_with_direct_gap_is_supported()
    {
        var layout = new DiskLayout(Disk(100 * Units.GiB, PartitionStyle.Mbr),
        [
            new PartitionInfo(0, 1, Units.MiB, 50 * Units.GiB, null, 0x07, 'C', true, true, "NTFS", null, null),
        ]);

        var result = ExtendPlanner.Plan(layout, WinReStatus.Disabled, new ExtendRequest('C'));

        Assert.True(result.Success);
        Assert.Equal(100 * Units.GiB - Units.MiB, result.Plan!.Steps.Single(s => s.Kind == StepKind.ExtendPartition).Size);
    }
}
