using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

/// <summary>Thu nho o NTFS tong quat (ADR-0004).</summary>
public class ShrinkTests
{
    private static readonly RunOptions Real = new(false, "/tmp/tsudisk-test/shrink", "test");

    private static PlanResult Plan(ShrinkRequest request, DiskLayout? layout = null) =>
        ShrinkPlanner.Plan(layout ?? RealMachine20261116(), request);

    private static RefusalCode Refused(ShrinkRequest request, DiskLayout? layout = null) => Plan(request, layout).Refusal!.Code;

    private static DiskLayout With(char letter, Func<PartitionInfo, PartitionInfo> change)
    {
        var layout = RealMachine20261116();
        return layout with { Partitions = [.. layout.Partitions.Select(p => p.DriveLetter == letter ? change(p) : p)] };
    }

    [Fact]
    public void Shrinking_G_frees_exactly_the_requested_space_right_after_it()
    {
        var layout = RealMachine20261116();
        var g = layout.FindByLetter('G')!;

        var plan = Plan(new ShrinkRequest('g', 20 * Units.GiB), layout).Plan!;

        Assert.Equal([StepKind.CheckHealth, StepKind.ScanVolume, StepKind.Backup, StepKind.ShrinkPartition, StepKind.VerifyLayout],
            plan.Steps.Select(s => s.Kind));
        Assert.Equal(20 * Units.GiB, plan.GainBytes);
        var shrunk = plan.ExpectedAfter.FindByLetter('G')!;
        Assert.Equal((g.Offset, g.Size - 20 * Units.GiB), (shrunk.Offset, shrunk.Size));
        Assert.Empty(SafetyGuard.CheckPlan(plan));

        // Vung trong sinh ra dung ngay duoc cho tao-phan-vung.
        var extent = CreatePartitionPlanner.UsableExtents(plan.ExpectedAfter).Single(e => e.Offset == shrunk.End);
        Assert.Equal(20 * Units.GiB, extent.Size);
    }

    [Fact]
    public void New_end_is_rounded_down_to_a_MiB_boundary()
    {
        var plan = Plan(new ShrinkRequest('G', 100 * Units.MiB + 1)).Plan!;

        Assert.Equal(101 * Units.MiB, plan.GainBytes);
        var g = plan.ExpectedAfter.FindByLetter('G')!;
        Assert.True(Units.IsAligned(g.End, Units.MiB));
    }

    [Fact]
    public void Data_drive_must_keep_enough_free_space()
    {
        var refusal = Plan(new ShrinkRequest('G', 72 * Units.GiB)).Refusal!;

        Assert.Equal(RefusalCode.InvalidOption, refusal.Code);
        Assert.Contains(refusal.Hints, h => h.StartsWith("Tối đa nhường", StringComparison.Ordinal));
    }

    [Fact]
    public void Windows_drive_keeps_at_least_10_GiB()
    {
        var request = new ShrinkRequest('C', 60 * Units.GiB);

        Assert.True(Plan(request).Success);
        var refusal = Plan(request with { SystemDrive = 'C' }).Refusal!;
        Assert.Equal(RefusalCode.InvalidOption, refusal.Code);
        Assert.Contains("Windows Update", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(RefusalCode.InvalidOption, Refused(request, With('C', p => p with { IsBoot = true })));
    }

    [Fact]
    public void Windows_minimum_size_refuses_early_with_concrete_hints()
    {
        var g = RealMachine20261116().FindByLetter('G')!;

        var refusal = Plan(new ShrinkRequest('G', 20 * Units.GiB) { WindowsMinSize = g.Size - 10 * Units.GiB }).Refusal!;

        Assert.Equal(RefusalCode.InvalidOption, refusal.Code);
        Assert.Contains(refusal.Hints, h => h.Contains(Units.Format(10 * Units.GiB), StringComparison.Ordinal));
        Assert.Contains(refusal.Hints, h => h.Contains("powercfg", StringComparison.Ordinal));
        Assert.True(Plan(new ShrinkRequest('G', 5 * Units.GiB) { WindowsMinSize = g.Size - 10 * Units.GiB }).Success);
    }

    [Fact]
    public void Unsupported_targets_are_refused()
    {
        Assert.Equal(RefusalCode.UnsupportedFileSystem, Refused(new ShrinkRequest('G', Units.GiB), With('G', p => p with { FileSystem = "exFAT" })));
        Assert.Equal(RefusalCode.UnsupportedFileSystem, Refused(new ShrinkRequest('G', Units.GiB), With('G', p => p with { FileSystem = "ReFS" })));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new ShrinkRequest('G', Units.GiB), With('G', p => p with { FreeBytes = null })));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new ShrinkRequest('G', 32 * Units.MiB)));
        // Lan dau E2E bat duoc: muc giam lon hon ca o lam AlignDown nem loi so am thay vi tu choi.
        Assert.Equal(RefusalCode.InvalidOption, Refused(new ShrinkRequest('G', 100_000 * Units.GiB)));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new ShrinkRequest('G', Units.GiB), With('G', p => p with { FreeBytes = Units.MiB })));
        Assert.Equal(RefusalCode.TargetNotFound, Refused(new ShrinkRequest('Q', Units.GiB)));
        Assert.Equal(RefusalCode.DiskNotWritable, Refused(new ShrinkRequest('G', Units.GiB) { VirtualDiskOnly = true }));
        var recoveryWithLetter = RealMachine20261116() with
        {
            Partitions = [.. RealMachine20261116().Partitions.Select(p => p.PartitionNumber == 4 ? p with { DriveLetter = 'R' } : p)],
        };
        Assert.Equal(RefusalCode.ProtectedTarget, Refused(new ShrinkRequest('R', Units.GiB), recoveryWithLetter));
    }

    [Fact]
    public void Guard_enforces_ADR_0004_on_tampered_plans()
    {
        var layout = RealMachine20261116();
        var plan = Plan(new ShrinkRequest('G', 20 * Units.GiB), layout).Plan!;
        var shrink = plan.Steps.Single(s => s.Kind == StepKind.ShrinkPartition);
        var g = layout.FindByLetter('G')!;
        Plan Tamper(PlanStep step) => plan with { Steps = [step], ExpectedAfter = LayoutSimulator.Apply(layout, [step]) };

        Assert.Contains(SafetyGuard.CheckPlan(plan with { Operation = "extend" }), v => v.Contains("thu nhỏ chỉ được phép", StringComparison.Ordinal));
        Assert.Contains(SafetyGuard.CheckPlan(Tamper(shrink with { Size = 2 * Units.GiB })), v => v.Contains("trống sau khi thu nhỏ", StringComparison.Ordinal));
        Assert.Contains(SafetyGuard.CheckPlan(Tamper(shrink with { Size = g.Size - 20 * Units.GiB + 4096 })), v => v.Contains("căn lề", StringComparison.Ordinal));
        Assert.Contains(SafetyGuard.CheckPlan(Tamper(shrink with { Size = g.Size })), v => v.Contains("nhỏ hơn hiện tại", StringComparison.Ordinal));
        var exfat = With('G', p => p with { FileSystem = "exFAT" });
        var onExfat = plan with { Before = exfat, ExpectedAfter = LayoutSimulator.Apply(exfat, [shrink]) };
        Assert.Contains(SafetyGuard.CheckPlan(onExfat), v => v.Contains("NTFS", StringComparison.Ordinal));
    }

    [Fact]
    public void Runner_shrinks_and_layout_matches_plan()
    {
        var plan = Plan(new ShrinkRequest('G', 20 * Units.GiB)).Plan!;
        var backend = new FakeBackend(plan.Before, WinReOnPartition4);

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.Succeeded, journal.State);
        Assert.Equal(["health", "scan", "backup", "shrink"], backend.Calls);
        Assert.Empty(SafetyGuard.CheckActual(plan, backend.Layout));
    }

    [Fact]
    public void Windows_minimum_rechecked_at_run_time_stops_before_shrinking()
    {
        var plan = Plan(new ShrinkRequest('G', 20 * Units.GiB)).Plan!;
        var backend = new FakeBackend(plan.Before, WinReOnPartition4) { MinSize = plan.Before.FindByLetter('G')!.Size - Units.GiB };

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.DoesNotContain("shrink", backend.Calls);
        Assert.Same(plan.Before, backend.Layout);
    }

    [Fact]
    public void Failed_shrink_leaves_layout_unchanged()
    {
        var plan = Plan(new ShrinkRequest('G', 20 * Units.GiB)).Plan!;
        var backend = new FakeBackend(plan.Before, WinReOnPartition4);
        backend.FailOn.Add("shrink");

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal(plan.Before.FindByLetter('G')!.Size, backend.Layout.FindByLetter('G')!.Size);
    }
}
