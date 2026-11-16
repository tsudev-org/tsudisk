using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;
using TsuDisk.Core.Scripts;
using TsuDisk.Core.Serialization;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

public class SafetyGuardTests
{
    private static Plan RealPlan() => ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('C')).Plan!;

    [Fact]
    public void Valid_plan_has_no_violations() => Assert.Empty(SafetyGuard.CheckPlan(RealPlan()));

    [Fact]
    public void Tampered_plan_deleting_a_data_partition_is_caught()
    {
        var plan = RealPlan();
        var g = plan.Before.FindByLetter('G')!;
        var steps = plan.Steps.Select(s => s.Kind == StepKind.DeletePartition ? s with { Offset = g.Offset, Size = g.Size } : s).ToList();

        var violations = SafetyGuard.CheckPlan(plan with { Steps = steps });

        Assert.Contains(violations, v => v.Contains("chỉ được xóa phân vùng Recovery", StringComparison.Ordinal));
    }

    [Fact]
    public void Tampered_plan_shrinking_target_is_caught()
    {
        var plan = RealPlan();
        var steps = plan.Steps.Select(s => s.Kind == StepKind.ExtendPartition ? s with { Size = 100 * Units.GiB } : s).ToList();

        Assert.Contains(SafetyGuard.CheckPlan(plan with { Steps = steps }), v => v.Contains("thu nhỏ", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_touching_EFI_is_caught()
    {
        var plan = RealPlan();
        var efi = plan.Before.Partitions.First(p => p.Kind == PartitionKind.EfiSystem);
        var steps = plan.Steps.Append(new PlanStep(99, StepKind.ExtendPartition, "x", true, efi.PartitionNumber, efi.Offset, efi.Size * 2)).ToList();

        Assert.Contains(SafetyGuard.CheckPlan(plan with { Steps = steps }), v => v.Contains("bảo vệ", StringComparison.Ordinal));
    }

    [Fact]
    public void Actual_layout_with_moved_data_partition_is_reported()
    {
        var plan = RealPlan();
        var moved = plan.ExpectedAfter with
        {
            Partitions = [.. plan.ExpectedAfter.Partitions.Select(p => p.DriveLetter == 'E' ? p with { Size = p.Size - Units.MiB } : p)],
        };

        Assert.Contains(SafetyGuard.CheckActual(plan, moved), v => v.Contains("E:", StringComparison.Ordinal));
    }

    [Fact]
    public void Expected_layout_satisfies_actual_check() => Assert.Empty(SafetyGuard.CheckActual(RealPlan(), RealPlan().ExpectedAfter));
}

public class ModelTests
{
    [Fact]
    public void Segments_find_both_unallocated_extents_on_the_real_machine()
    {
        var free = RealMachine20261116().FreeExtents();

        Assert.Equal(2, free.Count);
        Assert.Equal(60 * Units.GiB, free[0].Size);
        Assert.Equal(773_135_872, free[1].Size);
    }

    [Fact]
    public void Analyzer_suggests_C_via_recovery_and_E_directly()
    {
        var advice = LayoutAnalyzer.Analyze(RealMachine20261116());

        Assert.Equal('C', advice[0].Absorber!.DriveLetter);
        Assert.True(advice[0].ViaRecoveryRelocation);
        Assert.Equal('E', advice[1].Absorber!.DriveLetter);
        Assert.False(advice[1].ViaRecoveryRelocation);
    }

    [Fact]
    public void Fingerprint_ignores_free_space_but_not_geometry()
    {
        var layout = RealMachine20261116();
        var freeChanged = layout with { Partitions = [.. layout.Partitions.Select(p => p with { FreeBytes = 1 })] };
        var resized = layout with { Partitions = [.. layout.Partitions.Select(p => p.DriveLetter == 'E' ? p with { Size = p.Size + Units.MiB } : p)] };

        Assert.Equal(LayoutFingerprint.Compute(layout), LayoutFingerprint.Compute(freeChanged));
        Assert.NotEqual(LayoutFingerprint.Compute(layout), LayoutFingerprint.Compute(resized));
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(961_544_192L, "917 MB")]
    [InlineData(284_663_218_176L, "265,11 GB")]
    [InlineData(348_975_529_984L, "325,01 GB")]
    public void Sizes_are_formatted_like_Windows_in_Vietnamese(long bytes, string expected) => Assert.Equal(expected, Units.Format(bytes));

    [Theory]
    [InlineData(0L, 0L, 0L)]
    [InlineData(1L, 0L, 1_048_576L)]
    [InlineData(1_048_576L, 1_048_576L, 1_048_576L)]
    [InlineData(1_048_577L, 1_048_576L, 2_097_152L)]
    public void Alignment_math(long value, long down, long up)
    {
        Assert.Equal(down, Units.AlignDown(value, Units.MiB));
        Assert.Equal(up, Units.AlignUp(value, Units.MiB));
    }

    [Fact]
    public void Partition_kinds_map_from_gpt_and_mbr()
    {
        Assert.Equal(PartitionKind.Recovery, PartitionTypes.FromGpt(PartitionTypes.Recovery));
        Assert.Equal(PartitionKind.Recovery, PartitionTypes.FromMbr(0x27));
        Assert.Equal(PartitionKind.BasicData, PartitionTypes.FromMbr(0x07));
        Assert.True(PartitionTypes.IsProtected(PartitionKind.EfiSystem));
        Assert.False(PartitionTypes.IsProtected(PartitionKind.Recovery));
    }
}

public class ToolingTests
{
    private const string EnabledEn = """
        Windows Recovery Environment (Windows RE) and system reset configuration
        Information:

            Windows RE status:         Enabled
            Windows RE location:       \\?\GLOBALROOT\device\harddisk0\partition4\Recovery\WindowsRE
            Boot Configuration Data (BCD) identifier: e9862386-70eb-11f1-a8c2-887f652cd151
        REAGENTC.EXE: Operation Successful.
        """;

    private const string DisabledEn = """
            Windows RE status:         Disabled
            Windows RE location:
        REAGENTC.EXE: Operation Successful.
        """;

    private const string EnabledLocalized = """
            Trạng thái Windows RE:      Đã bật
            Vị trí Windows RE:          \\?\GLOBALROOT\device\HarddisK1\Partition5\Recovery\WindowsRE
        """;

    [Fact]
    public void ReAgentC_enabled_output_is_parsed() => Assert.Equal(new WinReStatus(true, 0, 4), ReAgentInfoParser.Parse(EnabledEn));

    [Fact]
    public void ReAgentC_disabled_output_is_parsed() => Assert.Equal(WinReStatus.Disabled, ReAgentInfoParser.Parse(DisabledEn));

    [Fact]
    public void ReAgentC_localized_output_is_parsed_by_location_not_words() =>
        Assert.Equal(new WinReStatus(true, 1, 5), ReAgentInfoParser.Parse(EnabledLocalized));

    [Fact]
    public void Diskpart_recovery_script_for_gpt_matches_the_proven_commands()
    {
        var lines = DiskpartScript.CreateRecovery(0, 349_203_070_976, PartitionStyle.Gpt);

        Assert.Equal(
        [
            "select disk 0",
            "create partition primary offset=341018624",
            "format quick fs=ntfs label=\"Recovery\"",
            "set id=de94bba4-06d1-4d40-a16a-bfd50179d6ac override",
            "gpt attributes=0x8000000000000001",
        ], lines);
    }

    [Fact]
    public void Diskpart_recovery_script_for_mbr_uses_type_27() =>
        Assert.Contains("set id=27 override", DiskpartScript.CreateRecovery(1, Units.GiB, PartitionStyle.Mbr));

    [Fact]
    public void Diskpart_rejects_unaligned_offsets() =>
        Assert.Throws<ArgumentException>(() => DiskpartScript.CreateRecovery(0, Units.MiB + 512, PartitionStyle.Gpt));

    [Fact]
    public void Plan_and_journal_round_trip_through_json()
    {
        var plan = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('C')).Plan!;
        var json = TsuDiskJson.Serialize(plan);
        var back = TsuDiskJson.DeserializePlan(json)!;

        Assert.Equal(plan.LayoutFingerprint, back.LayoutFingerprint);
        Assert.Equal(plan.Steps, back.Steps);
        Assert.Equal(LayoutFingerprint.Compute(plan.Before), LayoutFingerprint.Compute(back.Before));
        Assert.Contains("Mở rộng C:", json, StringComparison.Ordinal);

        var journal = new RunJournal("r1", plan, RunState.Succeeded, [], ["ok"], "26.11.1601");
        Assert.Equal(RunState.Succeeded, TsuDiskJson.DeserializeJournal(TsuDiskJson.Serialize(journal))!.State);
    }

    [Fact]
    public void Advisor_for_interrupted_run_after_delete_points_to_rerun()
    {
        var plan = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('C')).Plan!;
        var steps = plan.Steps.Select(s => new StepRecord(s.Index, s.Kind, s.Title,
            s.Kind is StepKind.ScanVolume or StepKind.Backup or StepKind.DisableWinRe or StepKind.DeletePartition ? StepStatus.Done : StepStatus.Pending,
            null, null, null)).ToList();
        var journal = new RunJournal("r2", plan, RunState.Running, steps, [], "test");

        var advice = RecoveryAdvisor.Advise(journal);

        Assert.Contains(advice, a => a.Contains("tsudisk mo-rong C:", StringComparison.Ordinal));
        Assert.Contains(advice, a => a.Contains("reagentc /enable", StringComparison.Ordinal));
    }
}
