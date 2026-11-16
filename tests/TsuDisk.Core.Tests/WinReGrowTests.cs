using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

public class WinReGrowTests
{
    private static readonly RunOptions Real = new(false, "/tmp/tsudisk-test/winre", "test");

    /// <summary>Bo cuc pho bien gay loi 0x80070643: EFI | MSR | C: | Recovery 529 MB con 80 MB trong (khong vung trong sau).</summary>
    private static DiskLayout Kb5034441Layout(long? recoveryFree = 80 * Units.MiB, long cFree = 100 * Units.GiB)
    {
        var disk = Disk(256 * Units.GiB);
        var c = Part(3, 117 * Units.MiB, disk.LastUsableByteExclusive - 117 * Units.MiB - 529 * Units.MiB - Units.MiB, PartitionTypes.BasicData, 'C', "NTFS", cFree);
        c = c with { Size = Units.AlignDown(c.Size, Units.MiB) };
        return new DiskLayout(disk,
        [
            Part(1, Units.MiB, 100 * Units.MiB, PartitionTypes.EfiSystem, fs: "FAT32"),
            Part(2, 101 * Units.MiB, 16 * Units.MiB, PartitionTypes.MicrosoftReserved),
            c,
            Part(4, c.End, 529 * Units.MiB, PartitionTypes.Recovery, fs: "NTFS", free: recoveryFree),
        ]);
    }

    private static readonly WinReStatus WinReOn4 = new(true, 0, 4);

    [Fact]
    public void Full_recovery_without_free_space_after_shrinks_C_by_exactly_what_is_missing()
    {
        var layout = Kb5034441Layout();
        var plan = WinReGrowPlanner.Plan(layout, WinReOn4, new WinReGrowRequest()).Plan!;

        Assert.Equal(
            [StepKind.CheckHealth, StepKind.ScanVolume, StepKind.Backup, StepKind.DisableWinRe, StepKind.ShrinkPartition,
             StepKind.DeletePartition, StepKind.CreateRecovery, StepKind.EnableWinRe, StepKind.VerifyLayout],
            plan.Steps.Select(s => s.Kind));
        var shrink = plan.Steps.Single(s => s.Kind == StepKind.ShrinkPartition);
        var create = plan.Steps.Single(s => s.Kind == StepKind.CreateRecovery);
        // Can 250 MB trong, dang co 80 MB -> thieu 170 MB, cong 16 MB du phong = 186 MB.
        Assert.Equal(186 * Units.MiB, plan.GainBytes);
        Assert.Equal(layout.FindByLetter('C')!.Size - 186 * Units.MiB, shrink.Size);
        Assert.Equal(shrink.Offset + shrink.Size, create.Offset);
        Assert.Equal(715 * Units.MiB, create.Size);
        Assert.Equal(layout.Partitions.Single(p => p.PartitionNumber == 4).End, create.Offset + create.Size);
        Assert.Empty(SafetyGuard.CheckPlan(plan));
    }

    [Fact]
    public void Free_space_after_recovery_grows_it_in_place_without_shrinking_anything()
    {
        var layout = Kb5034441Layout();
        var c = layout.FindByLetter('C')!;
        var shrunkC = c with { Size = c.Size - Units.GiB };
        var rec = layout.Partitions.Single(p => p.PartitionNumber == 4) with { Offset = shrunkC.End };
        layout = layout with { Partitions = [.. layout.Partitions.Where(p => p.PartitionNumber < 3), shrunkC, rec] };

        var plan = WinReGrowPlanner.Plan(layout, WinReOn4, new WinReGrowRequest()).Plan!;

        Assert.DoesNotContain(plan.Steps, s => s.Kind == StepKind.ShrinkPartition);
        Assert.Equal(rec.Offset, plan.Steps.Single(s => s.Kind == StepKind.CreateRecovery).Offset);
    }

    [Fact]
    public void Recovery_with_enough_free_space_is_left_alone()
    {
        var result = WinReGrowPlanner.Plan(Kb5034441Layout(recoveryFree: 300 * Units.MiB), WinReOn4, new WinReGrowRequest());

        Assert.Equal(RefusalCode.NothingToGain, result.Refusal!.Code);
    }

    [Fact]
    public void Real_machine_after_16_11_already_has_enough_room()
    {
        var after = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('C')).Plan!.ExpectedAfter;
        var rec = after.Partitions.Single(p => p.Kind == PartitionKind.Recovery);
        after = after with { Partitions = [.. after.Partitions.Select(p => p == rec ? p with { FreeBytes = 259 * Units.MiB } : p)] };

        Assert.Equal(RefusalCode.NothingToGain, WinReGrowPlanner.Plan(after, new WinReStatus(true, 0, rec.PartitionNumber), new WinReGrowRequest()).Refusal!.Code);
    }

    [Fact]
    public void Nearly_full_C_refuses_to_give_space()
    {
        var result = WinReGrowPlanner.Plan(Kb5034441Layout(cFree: Units.GiB), WinReOn4, new WinReGrowRequest());

        Assert.Equal(RefusalCode.InvalidOption, result.Refusal!.Code);
    }

    [Fact]
    public void Disabled_WinRE_is_refused() =>
        Assert.Equal(RefusalCode.RecoveryNotWinRe, WinReGrowPlanner.Plan(Kb5034441Layout(), WinReStatus.Disabled, new WinReGrowRequest()).Refusal!.Code);

    [Fact]
    public void Shrink_outside_grow_winre_operation_is_a_safety_violation()
    {
        var plan = WinReGrowPlanner.Plan(Kb5034441Layout(), WinReOn4, new WinReGrowRequest()).Plan!;

        Assert.Contains(SafetyGuard.CheckPlan(plan with { Operation = "extend" }), v => v.Contains("thu nhỏ chỉ được phép", StringComparison.Ordinal));
    }

    [Fact]
    public void Shrink_larger_than_cap_is_a_safety_violation()
    {
        var plan = WinReGrowPlanner.Plan(Kb5034441Layout(), WinReOn4, new WinReGrowRequest()).Plan!;
        var steps = plan.Steps.Select(s => s.Kind == StepKind.ShrinkPartition ? s with { Size = s.Size - 5 * Units.GiB } : s).ToList();

        Assert.Contains(SafetyGuard.CheckPlan(plan with { Steps = steps }), v => v.Contains("không quá 4 GB", StringComparison.Ordinal));
    }

    [Fact]
    public void Runner_success_matches_expected_layout_and_WinRE_moves_to_the_bigger_partition()
    {
        var layout = Kb5034441Layout();
        var plan = WinReGrowPlanner.Plan(layout, WinReOn4, new WinReGrowRequest()).Plan!;
        var backend = new FakeBackend(layout, WinReOn4);

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.Succeeded, journal.State);
        Assert.Empty(SafetyGuard.CheckActual(plan, backend.Layout));
        var rec = backend.Layout.Partitions.Single(p => p.Kind == PartitionKind.Recovery);
        Assert.Equal(715 * Units.MiB, rec.Size);
        Assert.True(backend.WinRe.LivesOn(rec));
    }

    [Fact]
    public void Delete_failure_after_shrink_regrows_C_and_re_enables_WinRE()
    {
        var layout = Kb5034441Layout();
        var plan = WinReGrowPlanner.Plan(layout, WinReOn4, new WinReGrowRequest()).Plan!;
        var backend = new FakeBackend(layout, WinReOn4);
        backend.FailOn.Add("delete");

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal(layout.FindByLetter('C')!.Size, backend.Layout.FindByLetter('C')!.Size);
        Assert.True(backend.WinRe.Enabled);
    }

    [Fact]
    public void Create_failure_restores_old_recovery_and_C_size()
    {
        var layout = Kb5034441Layout();
        var plan = WinReGrowPlanner.Plan(layout, WinReOn4, new WinReGrowRequest()).Plan!;
        var backend = new FakeBackend(layout, WinReOn4);
        backend.FailOn.Add("create-recovery");
        backend.FailOn.Remove("create-recovery");
        var failOnce = true;
        var wrapped = new FailFirstCreate(backend, () => failOnce, () => failOnce = false);

        var journal = new PlanRunner(wrapped, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        var oldRec = layout.Partitions.Single(p => p.PartitionNumber == 4);
        var rec = backend.Layout.Partitions.Single(p => p.Kind == PartitionKind.Recovery);
        Assert.Equal((oldRec.Offset, oldRec.Size), (rec.Offset, rec.Size));
        Assert.Equal(layout.FindByLetter('C')!.Size, backend.Layout.FindByLetter('C')!.Size);
        Assert.True(backend.WinRe.Enabled);
    }

    [Fact]
    public void Shrink_blocked_by_unmovable_data_stops_before_deleting()
    {
        var layout = Kb5034441Layout();
        var plan = WinReGrowPlanner.Plan(layout, WinReOn4, new WinReGrowRequest()).Plan!;
        var backend = new FakeBackend(layout, WinReOn4) { MinSize = layout.FindByLetter('C')!.Size - Units.MiB };

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.DoesNotContain("delete", backend.Calls);
        Assert.True(backend.WinRe.Enabled);
    }

    /// <summary>Bo boc lam CreateRecovery that bai DUNG LAN DAU (buoc chinh), cac lan sau (buoc bu) chay binh thuong.</summary>
    private sealed class FailFirstCreate(FakeBackend inner, Func<bool> shouldFail, Action consumed) : IDiskBackend
    {
        public IReadOnlyList<DiskInfo> ListDisks() => inner.ListDisks();
        public DiskLayout ReadLayout(int diskNumber) => inner.ReadLayout(diskNumber);
        public WinReStatus ReadWinRe() => inner.ReadWinRe();
        public VolumeScanResult ScanVolume(char driveLetter) => inner.ScanVolume(driveLetter);
        public Health.DiskHealth? ReadHealth(int diskNumber) => inner.ReadHealth(diskNumber);
        public void SaveBackups(string runDirectory, DiskLayout layout) => inner.SaveBackups(runDirectory, layout);
        public void DisableWinRe() => inner.DisableWinRe();
        public void EnableWinRe() => inner.EnableWinRe();
        public bool WinReImageOnSystemDrive() => inner.WinReImageOnSystemDrive();
        public long GetMaxPartitionSize(int diskNumber, int partitionNumber) => inner.GetMaxPartitionSize(diskNumber, partitionNumber);
        public long GetMinPartitionSize(int diskNumber, int partitionNumber) => inner.GetMinPartitionSize(diskNumber, partitionNumber);
        public void DeletePartition(int diskNumber, int partitionNumber) => inner.DeletePartition(diskNumber, partitionNumber);
        public void ResizePartition(int diskNumber, int partitionNumber, long newSize) => inner.ResizePartition(diskNumber, partitionNumber, newSize);

        public void CreateRecoveryPartition(int diskNumber, long offset, long? size, PartitionStyle style)
        {
            if (shouldFail())
            {
                consumed();
                throw new DiskOperationException("Lỗi tiêm vào: diskpart");
            }
            inner.CreateRecoveryPartition(diskNumber, offset, size, style);
        }
    }
}
