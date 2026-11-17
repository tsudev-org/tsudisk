using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

/// <summary>Xoa phan vung du lieu (ADR-0005).</summary>
public class DeletePartitionTests
{
    private static readonly RunOptions Real = new(false, "/tmp/tsudisk-test/delete", "test");

    /// <summary>May that 16/11/2026, C: la o khoi dong, E: mang nhan tieng Viet.</summary>
    private static DiskLayout Layout()
    {
        var real = RealMachine20261116();
        return real with
        {
            Partitions = [.. real.Partitions.Select(p => p.DriveLetter switch
            {
                'C' => p with { IsBoot = true },
                'E' => p with { Label = "Dữ liệu" },
                _ => p,
            })],
        };
    }

    private static Plan Ok(char letter, DiskLayout? layout = null) =>
        DeletePartitionPlanner.Plan(layout ?? Layout(), new DeletePartitionRequest(letter)).Plan
        ?? throw new InvalidOperationException("Kế hoạch bị từ chối ngoài dự kiến.");

    private static RefusalCode Refused(char letter, DiskLayout? layout = null, bool virtualOnly = false) =>
        DeletePartitionPlanner.Plan(layout ?? Layout(), new DeletePartitionRequest(letter) { VirtualDiskOnly = virtualOnly }).Refusal!.Code;

    private static (FakeBackend Backend, RunJournal Journal) Run(Plan plan, Action<FakeBackend>? setup = null, RunOptions? options = null)
    {
        var backend = new FakeBackend(plan.Before, WinReOnPartition4);
        setup?.Invoke(backend);
        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, options ?? Real);
        return (backend, journal);
    }

    // ---------- Lap ke hoach ----------

    [Fact]
    public void Plan_deletes_exactly_one_data_volume_and_frees_its_space()
    {
        var plan = Ok('e');
        var e = plan.Before.FindByLetter('E')!;

        Assert.Equal(DeletePartitionPlanner.Operation, plan.Operation);
        Assert.Equal([StepKind.CheckHealth, StepKind.Backup, StepKind.DeleteDataPartition, StepKind.VerifyLayout], plan.Steps.Select(s => s.Kind));
        var step = plan.Steps.Single(s => s.ChangesDisk);
        Assert.Equal((e.Offset, e.Size, 'E', "Dữ liệu", e.PartitionNumber), (step.Offset!.Value, step.Size!.Value, step.DriveLetter!.Value, step.NewLabel!, step.PartitionNumber!.Value));
        Assert.Null(plan.ExpectedAfter.FindByLetter('E'));
        Assert.Equal(plan.Before.Partitions.Count - 1, plan.ExpectedAfter.Partitions.Count);
        Assert.Equal(e.Size, plan.GainBytes);
        Assert.Empty(SafetyGuard.CheckPlan(plan));
    }

    [Fact]
    public void Confirmation_is_letter_and_exact_label()
    {
        var e = Layout().FindByLetter('E')!;
        Assert.Equal("E: Dữ liệu", DeletePartitionPlanner.ConfirmationText(e));
        Assert.True(DeletePartitionPlanner.ConfirmationMatches(e, "  e: Dữ liệu "));
        Assert.False(DeletePartitionPlanner.ConfirmationMatches(e, "E: Du lieu"));
        Assert.False(DeletePartitionPlanner.ConfirmationMatches(e, "E:"));
        Assert.False(DeletePartitionPlanner.ConfirmationMatches(e, "co"));
        Assert.False(DeletePartitionPlanner.ConfirmationMatches(e, null));
        // Chuoi to hop (NFD) cua cung nhan van khop.
        Assert.True(DeletePartitionPlanner.ConfirmationMatches(e, "E: " + "Dữ liệu".Normalize(System.Text.NormalizationForm.FormD)));
        var g = Layout().FindByLetter('G')!;
        Assert.Equal("G:", DeletePartitionPlanner.ConfirmationText(g));
    }

    [Fact]
    public void Manual_restore_commands_recreate_same_geometry()
    {
        var layout = Layout();
        var e = layout.FindByLetter('E')!;
        var lines = DeletePartitionPlanner.ManualRestoreCommands(layout, e);
        Assert.Equal(["select disk 0", $"create partition primary offset={e.Offset / 1024} size={e.Size / Units.MiB}", "assign letter=E"], lines);
        Assert.Contains(Ok('E').Notes, n => n.Contains("create partition primary", StringComparison.Ordinal));
    }

    [Fact]
    public void Protected_and_missing_targets_are_refused()
    {
        Assert.Equal(RefusalCode.ProtectedTarget, Refused('C'));
        Assert.Equal(RefusalCode.TargetNotFound, Refused('Q'));
        Assert.Equal(RefusalCode.DiskNotWritable, Refused('E', virtualOnly: true));
        Assert.Equal(RefusalCode.DiskNotWritable, Refused('E', Layout() with { Disk = Disk(readOnly: true) }));
        var recoveryWithLetter = Layout() with
        {
            Partitions = [.. Layout().Partitions.Select(p => p.Kind == PartitionKind.Recovery ? p with { DriveLetter = 'R' } : p)],
        };
        Assert.Equal(RefusalCode.ProtectedTarget, Refused('R', recoveryWithLetter));
    }

    [Fact]
    public void Guard_rejects_hand_edited_delete_plans()
    {
        var plan = Ok('E');
        var step = plan.Steps.Single(s => s.ChangesDisk);
        Plan With(PlanStep changed, string? operation = null) =>
            plan with { Operation = operation ?? plan.Operation, Steps = [.. plan.Steps.Select(s => s.Index == step.Index ? changed : s)] };

        var c = plan.Before.FindByLetter('C')!;
        Assert.NotEmpty(SafetyGuard.CheckPlan(With(step with { Offset = c.Offset, Size = c.Size, DriveLetter = 'C', PartitionNumber = c.PartitionNumber })));
        Assert.NotEmpty(SafetyGuard.CheckPlan(With(step, operation: "extend")));
        Assert.NotEmpty(SafetyGuard.CheckPlan(With(step with { DriveLetter = 'G' })));
        Assert.NotEmpty(SafetyGuard.CheckPlan(With(step with { NewLabel = "" })));
        Assert.NotEmpty(SafetyGuard.CheckPlan(With(step with { Size = step.Size - Units.MiB })));
        var g = plan.Before.FindByLetter('G')!;
        var two = plan with
        {
            Steps = [.. plan.Steps, new PlanStep(9, StepKind.DeleteDataPartition, "x", true, g.PartitionNumber, g.Offset, g.Size, 'G', NewLabel: "")],
        };
        Assert.NotEmpty(SafetyGuard.CheckPlan(two));
        // Buoc DeletePartition cu van chi cho Recovery, ke ca trong thao tac xoa.
        Assert.NotEmpty(SafetyGuard.CheckPlan(With(step with { Kind = StepKind.DeletePartition })));
    }

    // ---------- Chay ----------

    [Fact]
    public void Success_deletes_only_the_target()
    {
        var plan = Ok('E');
        var (backend, journal) = Run(plan);

        Assert.Equal(RunState.Succeeded, journal.State);
        Assert.Equal(["health", "backup", "delete-data"], backend.Calls);
        Assert.Null(backend.Layout.FindByLetter('E'));
        Assert.Empty(SafetyGuard.CheckActual(plan, backend.Layout));
    }

    [Fact]
    public void Dry_run_never_deletes()
    {
        var plan = Ok('E');
        var (backend, journal) = Run(plan, options: Real with { DryRun = true });
        Assert.Equal(RunState.DryRunCompleted, journal.State);
        Assert.DoesNotContain("delete-data", backend.Calls);
        Assert.Same(plan.Before, backend.Layout);
    }

    [Fact]
    public void Volume_in_use_stops_without_deleting()
    {
        var plan = Ok('E');
        var (backend, journal) = Run(plan, b => b.VolumeInUse = true);
        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal(plan.Before.Ordered, backend.Layout.Ordered);
        Assert.Contains(journal.Messages, m => m.Contains("đang được dùng", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("letter")]
    [InlineData("label")]
    public void Letter_or_label_changed_since_planning_stops_before_delete(string change)
    {
        var plan = Ok('E');
        var (backend, journal) = Run(plan, b => b.ReplaceLayout(b.Layout with
        {
            Partitions = [.. b.Layout.Partitions.Select(p => p.DriveLetter == 'E'
                ? change == "letter" ? p with { DriveLetter = 'F' } : p with { Label = "Khác" }
                : p)],
        }));
        Assert.NotEqual(RunState.Succeeded, journal.State);
        Assert.DoesNotContain("delete-data", backend.Calls);
    }

    [Fact]
    public void Failed_verification_recreates_the_volume_at_its_old_place()
    {
        var plan = Ok('E');
        var e = plan.Before.FindByLetter('E')!;
        var g = plan.Before.FindByLetter('G')!;
        // Mot cong cu khac go chu cai G: dung luc xoa -> doi chieu lech -> buoc bu tao lai E:.
        var (backend, journal) = Run(plan, b => b.AfterDeleteData = f => f.ReplaceLayout(f.Layout with
        {
            Partitions = [.. f.Layout.Partitions.Select(p => p.Offset == g.Offset ? p with { Size = p.Size - Units.MiB } : p)],
        }));

        Assert.Equal(RunState.RolledBack, journal.State);
        var back = backend.Layout.FindByOffset(e.Offset)!;
        Assert.Equal((e.Size, (char?)'E', PartitionKind.BasicData), (back.Size, back.DriveLetter, back.Kind));
        Assert.Equal(["create-partition", "set-letter"], backend.Calls[^2..]);
    }

    [Fact]
    public void Advisor_explains_an_interrupted_delete()
    {
        var plan = Ok('E');
        var steps = plan.Steps.Select(s => new StepRecord(s.Index, s.Kind, s.Title,
            s.Kind == StepKind.DeleteDataPartition ? StepStatus.Running : s.Kind == StepKind.VerifyLayout ? StepStatus.Pending : StepStatus.Done,
            null, null, null)).ToList();

        var advice = RecoveryAdvisor.Advise(new RunJournal("r", plan, RunState.Running, steps, [], "test"));

        Assert.Contains(advice, a => a.Contains("create partition primary", StringComparison.Ordinal));
    }
}
