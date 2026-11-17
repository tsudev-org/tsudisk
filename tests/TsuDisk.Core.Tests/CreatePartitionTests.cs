using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

/// <summary>Tao + dinh dang phan vung du lieu trong vung trong (giai doan 2, rui ro vua).</summary>
public class CreatePartitionTests
{
    private static readonly RunOptions Real = new(false, "/tmp/tsudisk-test/create", "test");
    private static readonly HashSet<char> NoOtherLetters = [];

    private static PlanResult Plan(CreatePartitionRequest request, DiskLayout? layout = null, IReadOnlySet<char>? used = null) =>
        CreatePartitionPlanner.Plan(layout ?? RealMachine20261116(), used ?? NoOtherLetters, request);

    private static Plan Ok(CreatePartitionRequest request, DiskLayout? layout = null) =>
        Plan(request, layout).Plan ?? throw new InvalidOperationException("Kế hoạch bị từ chối ngoài dự kiến.");

    private static RefusalCode Refused(CreatePartitionRequest request, DiskLayout? layout = null, IReadOnlySet<char>? used = null) =>
        Plan(request, layout, used).Refusal!.Code;

    /// <summary>Dia MBR 100 GB: C: 40 GB, con lai trong.</summary>
    private static DiskLayout MbrWithGap(int partitions = 1)
    {
        var parts = Enumerable.Range(0, partitions).Select(i =>
            new PartitionInfo(0, i + 1, Units.MiB + i * 10 * Units.GiB, 10 * Units.GiB, null, 0x07, (char)('C' + i), i == 0, i == 0, "NTFS", null, null));
        return new DiskLayout(Disk(100 * Units.GiB, PartitionStyle.Mbr), [.. parts]);
    }

    // ---------- Lap ke hoach ----------

    [Fact]
    public void Default_plan_fills_the_chosen_gap_with_NTFS_and_the_first_free_letter()
    {
        var layout = RealMachine20261116();
        var extent = CreatePartitionPlanner.UsableExtents(layout)[0];

        var plan = Ok(new CreatePartitionRequest { ExtentIndex = 1, Label = "Kho ảnh" }, layout);

        Assert.Equal([StepKind.CheckHealth, StepKind.Backup, StepKind.CreatePartition, StepKind.FormatVolume, StepKind.VerifyLayout],
            plan.Steps.Select(s => s.Kind));
        var recovery = layout.Partitions.Single(p => p.Kind == PartitionKind.Recovery);
        Assert.Equal(Units.AlignUp(recovery.End, Units.MiB), extent.Offset);
        var created = plan.ExpectedAfter.FindByOffset(extent.Offset)!;
        Assert.Equal(extent.Size, created.Size);
        Assert.Equal(PartitionKind.BasicData, created.Kind);
        Assert.Equal(('D', "NTFS", "Kho ảnh"), (created.DriveLetter!.Value, created.FileSystem!, created.Label!));
        Assert.Equal('D', plan.TargetLetter);
        Assert.Equal(extent.Size, plan.GainBytes);
        foreach (var old in layout.Partitions)
            Assert.Equal(old, plan.ExpectedAfter.FindByOffset(old.Offset));
        Assert.Empty(SafetyGuard.CheckPlan(plan));
    }

    [Fact]
    public void Usable_extents_are_aligned_and_numbered_by_offset()
    {
        var extents = CreatePartitionPlanner.UsableExtents(RealMachine20261116());

        Assert.Equal(2, extents.Count);
        Assert.Equal([1, 2], extents.Select(e => e.Index));
        Assert.All(extents, e => Assert.True(Units.IsAligned(e.Offset, Units.MiB) && e.Size % Units.MiB == 0));
        Assert.True(extents[0].Offset < extents[1].Offset);
    }

    [Fact]
    public void Several_gaps_require_an_explicit_choice()
    {
        var refusal = Plan(new CreatePartitionRequest()).Refusal!;

        Assert.Equal(RefusalCode.InvalidOption, refusal.Code);
        Assert.Equal(2, refusal.Hints.Count);
        Assert.StartsWith("--vung 1:", refusal.Hints[0], StringComparison.Ordinal);
        Assert.Equal(RefusalCode.InvalidOption, Refused(new CreatePartitionRequest { ExtentIndex = 3 }));
    }

    [Fact]
    public void Size_must_fit_be_whole_MiB_and_not_tiny()
    {
        Assert.Equal(RefusalCode.InvalidOption, Refused(new CreatePartitionRequest { ExtentIndex = 2, Size = 2 * Units.GiB }));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new CreatePartitionRequest { ExtentIndex = 1, Size = 100 * Units.MiB + 1 }));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new CreatePartitionRequest { ExtentIndex = 1, Size = 32 * Units.MiB }));

        var plan = Ok(new CreatePartitionRequest { ExtentIndex = 1, Size = 10 * Units.GiB });
        Assert.Equal(10 * Units.GiB, plan.Steps.Single(s => s.Kind == StepKind.CreatePartition).Size);
        Assert.Contains(plan.Notes, n => n.Contains("Còn trống sau đó", StringComparison.Ordinal));
    }

    [Fact]
    public void File_system_rules_are_enforced()
    {
        Assert.Equal(RefusalCode.InvalidOption, Refused(new CreatePartitionRequest { ExtentIndex = 1, FileSystem = "FAT32" }));
        Assert.Equal(RefusalCode.UnsupportedFileSystem, Refused(new CreatePartitionRequest { ExtentIndex = 1, FileSystem = "ReFS" }));
        Assert.Equal(RefusalCode.InvalidOption,
            Refused(new CreatePartitionRequest { ExtentIndex = 2, FileSystem = "FAT32", Label = "Ảnh" }));

        var fat = Ok(new CreatePartitionRequest { ExtentIndex = 2, FileSystem = "fat32", Label = "USB" });
        Assert.Equal("FAT32", fat.Steps.Single(s => s.Kind == StepKind.FormatVolume).FileSystem);
        Assert.Contains(fat.Notes, n => n.Contains("4 GB", StringComparison.Ordinal));
        Assert.Equal("exFAT", Ok(new CreatePartitionRequest { ExtentIndex = 1, FileSystem = "EXFAT" }).ExpectedAfter.FindByLetter('D')!.FileSystem);
    }

    [Fact]
    public void Letters_are_checked_against_every_drive()
    {
        Assert.Equal(RefusalCode.LetterInUse, Refused(new CreatePartitionRequest { ExtentIndex = 1, Letter = 'G' }));
        Assert.Equal(RefusalCode.LetterInUse, Refused(new CreatePartitionRequest { ExtentIndex = 1, Letter = 'Z' }, used: new HashSet<char> { 'Z' }));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new CreatePartitionRequest { ExtentIndex = 1, Letter = 'B' }));

        var auto = Plan(new CreatePartitionRequest { ExtentIndex = 1 }, used: new HashSet<char> { 'D', 'F' }).Plan!;
        Assert.Equal('H', auto.TargetLetter);
        Assert.Equal('K', Ok(new CreatePartitionRequest { ExtentIndex = 1, Letter = 'k' }).TargetLetter);
    }

    [Fact]
    public void Disks_that_cannot_take_a_new_partition_are_refused()
    {
        var full = new DiskLayout(Disk(10 * Units.GiB),
            [Part(1, Units.MiB, Units.AlignDown(Disk(10 * Units.GiB).LastUsableByteExclusive, Units.MiB) - Units.MiB, PartitionTypes.BasicData, 'D', "NTFS")]);
        Assert.Equal(RefusalCode.NothingToGain, Refused(new CreatePartitionRequest(), full));
        Assert.Equal(RefusalCode.DiskNotWritable, Refused(new CreatePartitionRequest { ExtentIndex = 1, VirtualDiskOnly = true }));
        Assert.Equal(RefusalCode.DiskNotWritable, Refused(new CreatePartitionRequest { ExtentIndex = 1 }, RealMachine20261116(Disk(readOnly: true))));
        Assert.Equal(RefusalCode.UnsupportedPartitionStyle, Refused(new CreatePartitionRequest(), MbrWithGap(4)));
        var raw = new DiskLayout(Disk(10 * Units.GiB, PartitionStyle.Unknown), []);
        Assert.Equal(RefusalCode.UnsupportedPartitionStyle, Refused(new CreatePartitionRequest(), raw));
    }

    [Fact]
    public void MBR_disk_gets_an_IFS_partition()
    {
        var plan = Ok(new CreatePartitionRequest(), MbrWithGap());

        var created = plan.ExpectedAfter.FindByLetter('D')!;
        Assert.Equal(PartitionTypes.MbrIfs, created.MbrType);
        Assert.Equal(PartitionKind.BasicData, created.Kind);
        Assert.Empty(SafetyGuard.CheckPlan(plan));
    }

    // ---------- Bat bien chan ke hoach sua tay ----------

    [Fact]
    public void Guard_rejects_creating_over_or_formatting_existing_partitions()
    {
        var layout = RealMachine20261116();
        var plan = Ok(new CreatePartitionRequest { ExtentIndex = 1 }, layout);
        var create = plan.Steps.Single(s => s.Kind == StepKind.CreatePartition);
        var format = plan.Steps.Single(s => s.Kind == StepKind.FormatVolume);
        var e = layout.FindByLetter('E')!;
        var g = layout.FindByLetter('G')!;

        // Tao de len G: (gia lap ExpectedAfter "dep" de chi bat bien buoc moi bat).
        var overG = plan with { Steps = [create with { Offset = g.Offset }], ExpectedAfter = layout };
        Assert.Contains(SafetyGuard.CheckPlan(overG), v => v.Contains("đè lên", StringComparison.Ordinal));

        // Vuot qua bien vung trong, an vao G:.
        var straddle = plan with { Steps = [create with { Size = g.Offset - create.Offset!.Value + Units.GiB }], ExpectedAfter = layout };
        Assert.Contains(SafetyGuard.CheckPlan(straddle), v => v.Contains("không nằm trọn", StringComparison.Ordinal));

        // Dinh dang E: co san - dieu cam quan trong nhat.
        var formatE = plan with { Steps = [create, format with { Offset = e.Offset }], ExpectedAfter = plan.ExpectedAfter };
        Assert.Contains(SafetyGuard.CheckPlan(formatE), v => v.Contains("cấm định dạng phân vùng có sẵn", StringComparison.Ordinal));

        var formatOnly = plan with { Steps = [format with { Index = 1 }] };
        Assert.Contains(SafetyGuard.CheckPlan(formatOnly), v => v.Contains("đi sau bước tạo", StringComparison.Ordinal));
    }

    [Fact]
    public void Guard_caps_MBR_at_four_primaries()
    {
        var layout = MbrWithGap(4);
        var step = new PlanStep(1, StepKind.CreatePartition, "x", true, Offset: 41 * Units.GiB, Size: 10 * Units.GiB);
        var plan = Ok(new CreatePartitionRequest(), MbrWithGap(3)) with
        {
            Before = layout,
            Steps = [step],
            ExpectedAfter = LayoutSimulator.Apply(layout, [step]),
        };

        Assert.Contains(SafetyGuard.CheckPlan(plan), v => v.Contains("tối đa 4", StringComparison.Ordinal));
    }

    // ---------- Chay + tiem loi ----------

    private static (FakeBackend Backend, RunJournal Journal) Run(Plan plan, Action<FakeBackend>? arrange = null, RunOptions? options = null)
    {
        var backend = new FakeBackend(plan.Before, WinReOnPartition4);
        arrange?.Invoke(backend);
        return (backend, new PlanRunner(backend, new MemoryJournalStore()).Run(plan, options ?? Real));
    }

    private static Plan StandardPlan() => Ok(new CreatePartitionRequest { ExtentIndex = 1, Label = "Kho" });

    [Fact]
    public void Success_creates_formats_and_leaves_others_untouched()
    {
        var plan = StandardPlan();

        var (backend, journal) = Run(plan);

        Assert.Equal(RunState.Succeeded, journal.State);
        Assert.Equal(["health", "backup", "create-partition", "format"], backend.Calls);
        var d = backend.Layout.FindByLetter('D')!;
        Assert.Equal(("NTFS", "Kho"), (d.FileSystem!, d.Label!));
        Assert.Empty(SafetyGuard.CheckActual(plan, backend.Layout));
    }

    [Fact]
    public void Dry_run_only_reads_and_backs_up()
    {
        var plan = StandardPlan();

        var (backend, journal) = Run(plan, options: Real with { DryRun = true });

        Assert.Equal(RunState.DryRunCompleted, journal.State);
        Assert.Equal(["health", "backup"], backend.Calls);
        Assert.Same(plan.Before, backend.Layout);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("after-create")]
    public void Failure_removes_only_the_new_partition(string failure)
    {
        var plan = StandardPlan();

        var (backend, journal) = Run(plan, b =>
        {
            if (failure == "after-create") b.FailAfterCreate = true;
            else b.FailOn.Add(failure);
        });

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal("delete", backend.Calls[^1]);
        Assert.Equal(plan.Before.Ordered, backend.Layout.Ordered);
    }

    [Fact]
    public void Old_file_system_revealed_in_the_gap_is_never_formatted()
    {
        var plan = StandardPlan();

        var (backend, journal) = Run(plan, b => b.RevealedFileSystem = "NTFS");

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.DoesNotContain("format", backend.Calls);
        Assert.Contains(journal.Steps, s => s.Status == StepStatus.Failed && s.Message!.Contains("TestDisk", StringComparison.Ordinal));
        Assert.Equal(plan.Before.Ordered, backend.Layout.Ordered);
    }

    [Fact]
    public void Letter_taken_after_planning_rolls_back_before_formatting()
    {
        var plan = StandardPlan();

        var (backend, journal) = Run(plan, b => b.OtherLetters.Add('D'));

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.DoesNotContain("format", backend.Calls);
        Assert.Equal(plan.Before.Ordered, backend.Layout.Ordered);
    }

    [Fact]
    public void Gap_used_by_another_tool_aborts_before_any_write()
    {
        var plan = StandardPlan();
        var extent = CreatePartitionPlanner.UsableExtents(plan.Before)[0];
        var intruder = Part(9, extent.Offset, Units.GiB, PartitionTypes.BasicData, 'X', "NTFS");

        var backend = new FakeBackend(plan.Before with { Partitions = [.. plan.Before.Partitions, intruder] }, WinReOnPartition4);

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.Aborted, journal.State);
        Assert.Empty(backend.Calls);
    }

    [Fact]
    public void Advisor_points_at_a_leftover_raw_partition()
    {
        var plan = StandardPlan();
        var steps = plan.Steps.Select(s => new StepRecord(s.Index, s.Kind, s.Title,
            s.Kind == StepKind.FormatVolume ? StepStatus.Running : s.Kind == StepKind.VerifyLayout ? StepStatus.Pending : StepStatus.Done,
            null, null, null)).ToList();

        var advice = RecoveryAdvisor.Advise(new RunJournal("r", plan, RunState.Running, steps, [], "test"));

        Assert.Contains(advice, a => a.Contains("RAW", StringComparison.Ordinal));
    }
}
