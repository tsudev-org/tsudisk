using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Presentation;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests.Presentation;

/// <summary>Nut "Mo rong o" cua giao dien (ADR-0011) + duong chay chung CLI/GUI.</summary>
public class ExtendFormTests
{
    [Fact]
    public void Request_is_the_cli_default_request()
    {
        var request = ExtendForm.Request('c');

        Assert.Equal(new ExtendRequest('C'), request);
        Assert.Equal(RecoveryPolicy.RelocateActiveWinReOnly, request.RecoveryPolicy);
        Assert.Equal(Units.GiB, request.RecoverySize);
        Assert.False(request.VirtualDiskOnly);
    }

    [Fact]
    public void Operation_constant_is_what_the_planner_writes()
    {
        var plan = ExtendPlanner.Plan(DirectGap(), WinReOnPartition4, ExtendForm.Request('C')).Plan!;
        Assert.Equal(ExtendPlanner.Operation, plan.Operation);
        Assert.Equal("extend", ExtendPlanner.Operation);
    }

    [Fact]
    public void Virtual_only_flag_reaches_the_planner_and_refuses_a_physical_disk()
    {
        var layout = DirectGap();
        var request = ExtendForm.Request('C', virtualDiskOnly: true);

        var refused = ExtendPlanner.Plan(layout, WinReOnPartition4, request);
        Assert.False(refused.Success);
        Assert.Equal(RefusalCode.DiskNotWritable, refused.Refusal!.Code);
        var onVhd = layout with { Disk = layout.Disk with { IsVirtual = true } };
        Assert.True(ExtendPlanner.Plan(onVhd, WinReOnPartition4, request).Success);
    }

    [Fact]
    public void Recovery_without_active_winre_is_refused_because_the_gui_never_recreates_it_empty()
    {
        // May that: C: | Recovery (WinRE o phan vung 4) | trong. WinRE o noi khac => GUI khong co --recovery tao-lai-rong.
        var result = ExtendPlanner.Plan(RealMachine20261116(), new WinReStatus(true, 0, 9), ExtendForm.Request('C'));

        Assert.False(result.Success);
        Assert.Equal(RefusalCode.RecoveryNotWinRe, result.Refusal!.Code);
    }

    [Fact]
    public void Relocation_is_detected_direct_extend_is_not()
    {
        var relocate = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, ExtendForm.Request('C')).Plan!;
        var direct = ExtendPlanner.Plan(DirectGap(), WinReOnPartition4, ExtendForm.Request('C')).Plan!;

        Assert.True(ExtendForm.RelocatesRecovery(relocate));
        Assert.False(ExtendForm.RelocatesRecovery(direct));
    }

    [Fact]
    public void Apply_needs_an_extend_plan_and_a_known_winre_status()
    {
        var plan = ExtendPlanner.Plan(DirectGap(), WinReOnPartition4, ExtendForm.Request('C')).Plan!;

        Assert.True(ExtendForm.CanApply(plan, winReKnown: true));
        Assert.False(ExtendForm.CanApply(plan, winReKnown: false));
        Assert.False(ExtendForm.CanApply(plan with { Operation = RenamePlanner.Operation }, winReKnown: true));
    }

    [Theory]
    [InlineData('c', "tsudisk mo-rong C:")]
    [InlineData('E', "tsudisk mo-rong E:")]
    public void Command_matches_cli_syntax(char letter, string expected) => Assert.Equal(expected, ExtendForm.Command(letter));

    [Theory]
    [InlineData("C", true)]
    [InlineData(" c: ", true)]
    [InlineData("D", false)]
    [InlineData("MO RONG", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Second_confirmation_requires_retyping_the_target_letter(string? typed, bool ok) =>
        Assert.Equal(ok, ExtendForm.ConfirmationMatches(typed, 'C'));

    [Fact]
    public void Shared_run_extends_and_rolls_back_to_the_original_layout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tsudisk-test-{Guid.NewGuid():N}");
        try
        {
            var plan = ExtendPlanner.Plan(DirectGap(), WinReOnPartition4, ExtendForm.Request('C')).Plan!;

            var ok = new FakeBackend(plan.Before, WinReOnPartition4);
            var (journal, _) = PlanExecution.Run(ok, _ => new MemoryJournalStore(), plan, root, dryRun: false, "test", DateTime.Now);
            Assert.Equal(RunState.Succeeded, journal.State);
            Assert.Equal(plan.ExpectedAfter.FindByLetter('C')!.Size, ok.Layout.FindByLetter('C')!.Size);
            Assert.Equal(plan.Before.FindByLetter('D'), ok.Layout.FindByLetter('D'));

            var relocate = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, ExtendForm.Request('C')).Plan!;
            var failing = new FakeBackend(relocate.Before, WinReOnPartition4);
            failing.FailOn.Add("resize");
            var (rolled, _) = PlanExecution.Run(failing, _ => new MemoryJournalStore(), relocate, root, dryRun: false, "test", DateTime.Now);
            Assert.Equal(RunState.RolledBack, rolled.State);
            Assert.Equal(relocate.Before.FindByLetter('C')!.Size, failing.Layout.FindByLetter('C')!.Size);
            Assert.Contains(failing.Layout.Partitions, p => p.Kind == PartitionKind.Recovery);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
