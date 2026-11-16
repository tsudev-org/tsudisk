using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

public class PlanRunnerTests
{
    private static readonly RunOptions Real = new(false, "/tmp/tsudisk-test/run-1", "test");
    private static readonly RunOptions Dry = Real with { DryRun = true };

    private static (FakeBackend Backend, MemoryJournalStore Store, Plan Plan) Arrange()
    {
        var layout = RealMachine20261116();
        var plan = ExtendPlanner.Plan(layout, WinReOnPartition4, new ExtendRequest('C')).Plan!;
        return (new FakeBackend(layout, WinReOnPartition4), new MemoryJournalStore(), plan);
    }

    [Fact]
    public void Success_path_ends_with_layout_matching_the_plan_and_WinRE_on_new_recovery()
    {
        var (backend, store, plan) = Arrange();

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.Succeeded, journal.State);
        Assert.Empty(SafetyGuard.CheckActual(plan, backend.Layout));
        Assert.Equal(348_975_529_984, backend.Layout.FindByLetter('C')!.Size);
        var recovery = backend.Layout.Partitions.Single(p => p.Kind == PartitionKind.Recovery);
        Assert.True(backend.WinRe.LivesOn(recovery));
        Assert.All(journal.Steps, s => Assert.Equal(StepStatus.Done, s.Status));
    }

    [Fact]
    public void Journal_is_persisted_after_every_transition()
    {
        var (backend, store, plan) = Arrange();

        new PlanRunner(backend, store).Run(plan, Real);

        // Running + Done cho moi buoc, cong ban ket thuc.
        Assert.Equal(plan.Steps.Count * 2 + 1, store.Saved.Count);
        Assert.Equal(RunState.Running, store.Saved[0].State);
    }

    [Fact]
    public void Dry_run_changes_nothing()
    {
        var (backend, store, plan) = Arrange();
        var before = backend.Layout;

        var journal = new PlanRunner(backend, store).Run(plan, Dry);

        Assert.Equal(RunState.DryRunCompleted, journal.State);
        Assert.Same(before, backend.Layout);
        Assert.Equal(["health", "scan", "backup"], backend.Calls);
        Assert.True(backend.WinRe.Enabled);
    }

    [Fact]
    public void Layout_changed_since_planning_aborts_before_any_call()
    {
        var (_, store, plan) = Arrange();
        var changed = RealMachine20261116() with
        {
            Partitions = [.. RealMachine20261116().Partitions.Where(p => p.DriveLetter != 'E')],
        };
        var backend = new FakeBackend(changed, WinReOnPartition4);

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.Aborted, journal.State);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public void Failing_disk_stops_before_any_other_call()
    {
        var (backend, store, plan) = Arrange();
        backend.Health = backend.Health! with { HealthStatus = "Unhealthy", OperationalStatus = ["Predictive Failure"] };

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal(["health"], backend.Calls);
        Assert.Contains("dấu hiệu hỏng", journal.Steps[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_health_on_raid_driver_does_not_block()
    {
        var (backend, store, plan) = Arrange();
        backend.Health = null;

        Assert.Equal(RunState.Succeeded, new PlanRunner(backend, store).Run(plan, Real).State);
    }

    [Fact]
    public void Unhealthy_volume_stops_before_touching_the_disk()
    {
        var (backend, store, plan) = Arrange();
        backend.Scan = new VolumeScanResult(false, "NeedsOfflineRepair");

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal(["health", "scan"], backend.Calls);
    }

    [Fact]
    public void Missing_WinRE_image_after_disable_re_enables_WinRE_and_keeps_partitions()
    {
        var (backend, store, plan) = Arrange();
        backend.ImageMissingAfterDisable = true;
        var before = backend.Layout;

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Same(before, backend.Layout);
        Assert.True(backend.WinRe.Enabled);
        Assert.DoesNotContain("delete", backend.Calls);
    }

    [Fact]
    public void Resize_failure_recreates_old_recovery_in_place_and_re_enables_WinRE()
    {
        var (backend, store, plan) = Arrange();
        backend.FailOn.Add("resize");
        var original = RealMachine20261116();

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        var recovery = backend.Layout.Partitions.Single(p => p.Kind == PartitionKind.Recovery);
        Assert.Equal(284_890_759_168, recovery.Offset);
        Assert.Equal(original.FindByLetter('C')!.Size, backend.Layout.FindByLetter('C')!.Size);
        Assert.True(backend.WinRe.LivesOn(recovery));
    }

    [Fact]
    public void Create_recovery_failure_keeps_extended_C_and_re_enables_WinRE_from_system_drive()
    {
        var (backend, store, plan) = Arrange();
        backend.FailOn.Add("create-recovery");

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal(348_975_529_984, backend.Layout.FindByLetter('C')!.Size);
        Assert.True(backend.WinRe.Enabled);
        Assert.Equal(1, backend.Calls.Count(c => c == "create-recovery"));
    }

    [Fact]
    public void Failing_compensation_marks_run_as_needing_attention_with_advice()
    {
        var (backend, store, plan) = Arrange();
        backend.FailOn.Add("resize");
        backend.FailOn.Add("create-recovery");

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.NeedsAttention, journal.State);
        Assert.Contains(journal.Messages, m => m.StartsWith("KHÔNG hoàn tác được", StringComparison.Ordinal));
        var advice = RecoveryAdvisor.Advise(journal);
        Assert.Contains(advice, a => a.Contains("reagentc /enable", StringComparison.Ordinal) || a.Contains("tsudisk mo-rong C:", StringComparison.Ordinal));
    }

    [Fact]
    public void Recovery_not_matching_plan_at_delete_time_is_not_deleted()
    {
        var (_, store, plan) = Arrange();
        // Ai do da thay phan vung 4 bang mot phan vung du lieu cung offset/kich thuoc NHUNG dau van tay khong doi kieu
        // khac: mo phong bang cach tao backend co Recovery doi kich thuoc sau khi lap ke hoach -> runner phai Aborted.
        var tampered = RealMachine20261116() with
        {
            Partitions = [.. RealMachine20261116().Partitions.Select(p => p.PartitionNumber == 4 ? p with { GptType = PartitionTypes.BasicData } : p)],
        };
        var backend = new FakeBackend(tampered, WinReOnPartition4);

        var journal = new PlanRunner(backend, store).Run(plan, Real);

        Assert.Equal(RunState.Aborted, journal.State);
        Assert.DoesNotContain("delete", backend.Calls);
    }
}
