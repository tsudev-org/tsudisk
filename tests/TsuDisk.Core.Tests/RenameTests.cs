using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Safety;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

/// <summary>Doi chu cai + nhan o (giai doan 2): lap ke hoach, bat bien, chay, tiem loi, hoan tac.</summary>
public class RenameTests
{
    private static readonly RunOptions Real = new(false, "/tmp/tsudisk-test/rename", "test");
    private static readonly HashSet<char> NoOtherLetters = [];

    /// <summary>May that 16/11 voi C: la o khoi dong (dung nhu Storage API bao) va E: co nhan "Data".</summary>
    private static DiskLayout Machine()
    {
        var layout = RealMachine20261116();
        return layout with
        {
            Partitions = [.. layout.Partitions.Select(p => p.DriveLetter switch
            {
                'C' => p with { IsBoot = true },
                'E' => p with { Label = "Data" },
                _ => p,
            })],
        };
    }

    private static Plan PlanFor(RenameRequest request, DiskLayout? layout = null, IReadOnlySet<char>? used = null) =>
        RenamePlanner.Plan(layout ?? Machine(), used ?? NoOtherLetters, request).Plan
        ?? throw new InvalidOperationException("Kế hoạch bị từ chối ngoài dự kiến.");

    private static RefusalCode Refused(RenameRequest request, DiskLayout? layout = null, IReadOnlySet<char>? used = null) =>
        RenamePlanner.Plan(layout ?? Machine(), used ?? NoOtherLetters, request).Refusal!.Code;

    // ---------- Lap ke hoach ----------

    [Fact]
    public void Label_then_letter_change_keeps_every_partition_geometry()
    {
        var layout = Machine();
        var plan = PlanFor(new RenameRequest('e', 'f', "Dữ liệu"), layout);

        Assert.Equal([StepKind.CheckHealth, StepKind.SetVolumeLabel, StepKind.SetDriveLetter, StepKind.VerifyLayout],
            plan.Steps.Select(s => s.Kind));
        Assert.Equal(RenamePlanner.Operation, plan.Operation);
        Assert.Equal('E', plan.TargetLetter);
        Assert.Empty(plan.TouchedOffsets);
        Assert.Equal(LayoutFingerprint.Compute(layout), LayoutFingerprint.Compute(plan.ExpectedAfter));
        var after = plan.ExpectedAfter.FindByLetter('F')!;
        Assert.Equal(layout.FindByLetter('E')!.Offset, after.Offset);
        Assert.Equal("Dữ liệu", after.Label);
        Assert.Null(plan.ExpectedAfter.FindByLetter('E'));
        Assert.Empty(SafetyGuard.CheckPlan(plan));
    }

    [Fact]
    public void Label_only_and_letter_only_plans_have_a_single_write_step()
    {
        Assert.Single(PlanFor(new RenameRequest('E', NewLabel: "Kho")).Steps, s => s.ChangesDisk);
        var letterOnly = PlanFor(new RenameRequest('G', 'H'));
        Assert.Equal(StepKind.SetDriveLetter, Assert.Single(letterOnly.Steps, s => s.ChangesDisk).Kind);
        Assert.Contains(letterOnly.Notes, n => n.Contains("Lối tắt", StringComparison.Ordinal));
    }

    [Fact]
    public void Empty_label_clears_the_label()
    {
        var plan = PlanFor(new RenameRequest('E', NewLabel: ""));

        Assert.Null(plan.ExpectedAfter.FindByLetter('E')!.Label);
        Assert.StartsWith("Xóa nhãn E:", plan.Steps.Single(s => s.Kind == StepKind.SetVolumeLabel).Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_unaligned_MBR_data_drive_can_still_be_renamed()
    {
        // Bo cuc thoi Windows XP: phan vung bat dau o sector 63 (khong can le 1 MiB), loai MBR 0x07.
        var disk = Disk(80 * Units.GiB, PartitionStyle.Mbr);
        var data = new PartitionInfo(0, 1, 63 * 512, 40 * Units.GiB, null, 0x07, 'D', false, false, "NTFS", "Old", null);
        var plan = PlanFor(new RenameRequest('D', 'K', "Cu"), new DiskLayout(disk, [data]));

        Assert.Empty(SafetyGuard.CheckPlan(plan));
        Assert.Equal('K', plan.ExpectedAfter.Partitions.Single().DriveLetter);
    }

    [Fact]
    public void System_drive_letter_never_changes_but_its_label_may()
    {
        Assert.Equal(RefusalCode.ProtectedTarget, Refused(new RenameRequest('C', 'K')));
        // Ke ca khi Storage API khong bao IsBoot, %SystemDrive% van chan.
        Assert.Equal(RefusalCode.ProtectedTarget, Refused(new RenameRequest('G', 'K') { SystemDrive = 'G' }));
        Assert.NotNull(RenamePlanner.Plan(Machine(), NoOtherLetters, new RenameRequest('C', NewLabel: "Windows")).Plan);
    }

    [Fact]
    public void Letter_in_use_anywhere_is_refused_with_free_suggestions()
    {
        var onDisk = RenamePlanner.Plan(Machine(), NoOtherLetters, new RenameRequest('E', 'G')).Refusal!;
        Assert.Equal(RefusalCode.LetterInUse, onDisk.Code);

        var network = RenamePlanner.Plan(Machine(), new HashSet<char> { 'Z', 'D' }, new RenameRequest('E', 'Z')).Refusal!;
        Assert.Equal(RefusalCode.LetterInUse, network.Code);
        var hint = Assert.Single(network.Hints);
        Assert.DoesNotContain("D:", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("G:", hint, StringComparison.Ordinal);
        Assert.Contains("F:", hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData('A')]
    [InlineData('B')]
    public void Floppy_letters_are_not_assignable(char letter) =>
        Assert.Equal(RefusalCode.InvalidOption, Refused(new RenameRequest('E', letter)));

    [Fact]
    public void Invalid_requests_are_refused_with_specific_codes()
    {
        Assert.Equal(RefusalCode.InvalidOption, Refused(new RenameRequest('E')));
        Assert.Equal(RefusalCode.TargetNotFound, Refused(new RenameRequest('Q', 'R')));
        Assert.Equal(RefusalCode.NothingToGain, Refused(new RenameRequest('E', 'E', "Data")));
        Assert.Equal(RefusalCode.DiskNotWritable, Refused(new RenameRequest('E', 'F') { VirtualDiskOnly = true }));
        var readOnly = Machine() with { Disk = Disk(readOnly: true) };
        Assert.Equal(RefusalCode.DiskNotWritable, Refused(new RenameRequest('E', 'F'), readOnly));
    }

    [Fact]
    public void Non_data_or_unformatted_partitions_are_refused()
    {
        var layout = Machine();
        var withLetters = layout with
        {
            Partitions = [.. layout.Partitions.Select(p => p.PartitionNumber switch
            {
                4 => p with { DriveLetter = 'R' },
                5 => p with { FileSystem = null },
                _ => p,
            })],
        };

        Assert.Equal(RefusalCode.ProtectedTarget, Refused(new RenameRequest('R', 'S'), withLetters));
        Assert.Equal(RefusalCode.UnsupportedFileSystem, Refused(new RenameRequest('G', 'H'), withLetters));
    }

    [Fact]
    public void Labels_follow_file_system_rules()
    {
        Assert.Equal(RefusalCode.InvalidOption, Refused(new RenameRequest('E', NewLabel: new string('x', 33))));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new RenameRequest('E', NewLabel: "a\tb")));
        Assert.Equal(RefusalCode.InvalidOption, Refused(new RenameRequest('E', NewLabel: " Data")));

        Assert.Null(VolumeNaming.LabelProblem(new string('x', 32), "NTFS"));
        Assert.Null(VolumeNaming.LabelProblem("Dữ liệu: ảnh, phim", "NTFS"));
        Assert.Null(VolumeNaming.LabelProblem("USB_2026", "FAT32"));
        Assert.NotNull(VolumeNaming.LabelProblem("123456789012", "FAT32"));
        Assert.NotNull(VolumeNaming.LabelProblem("A.B", "FAT32"));
        Assert.NotNull(VolumeNaming.LabelProblem("Dữ liệu", "FAT32"));
        Assert.Null(VolumeNaming.LabelProblem("", "FAT32"));
    }

    // ---------- Bat bien chan ke hoach bi sua tay ----------

    [Fact]
    public void Guard_rejects_tampered_rename_plans()
    {
        var layout = Machine();
        var plan = PlanFor(new RenameRequest('E', 'F', "Kho"), layout);
        var c = layout.FindByLetter('C')!;
        var efi = layout.Partitions.Single(p => p.Kind == PartitionKind.EfiSystem);

        Plan WithStep(PlanStep step) => plan with { Steps = [step], ExpectedAfter = LayoutSimulator.Apply(layout, [step]) };

        var onSystem = WithStep(new PlanStep(1, StepKind.SetDriveLetter, "x", true, c.PartitionNumber, c.Offset, c.Size, 'C', 'K'));
        Assert.Contains(SafetyGuard.CheckPlan(onSystem), v => v.Contains("hệ thống", StringComparison.Ordinal));

        var onEfi = WithStep(new PlanStep(1, StepKind.SetVolumeLabel, "x", true, efi.PartitionNumber, efi.Offset, efi.Size, null, null, "X"));
        Assert.NotEmpty(SafetyGuard.CheckPlan(onEfi));

        var e = layout.FindByLetter('E')!;
        var duplicate = WithStep(new PlanStep(1, StepKind.SetDriveLetter, "x", true, e.PartitionNumber, e.Offset, e.Size, 'E', 'G'));
        Assert.Contains(SafetyGuard.CheckPlan(duplicate), v => v.Contains("nhiều phân vùng", StringComparison.Ordinal));

        var badLabel = WithStep(new PlanStep(1, StepKind.SetVolumeLabel, "x", true, e.PartitionNumber, e.Offset, e.Size, 'E', null, new string('x', 40)));
        Assert.NotEmpty(SafetyGuard.CheckPlan(badLabel));

        // Buoc doi ten khong duoc lam doi kich thuoc: phan vung van chiu bat bien "giu nguyen".
        var resized = plan with
        {
            ExpectedAfter = plan.ExpectedAfter with
            {
                Partitions = [.. plan.ExpectedAfter.Partitions.Select(p => p.Offset == e.Offset ? p with { Size = p.Size - Units.MiB } : p)],
            },
        };
        Assert.Contains(SafetyGuard.CheckPlan(resized), v => v.Contains("kích thước đổi", StringComparison.Ordinal));
    }

    [Fact]
    public void CheckActual_compares_letter_exactly_and_label_case_insensitively()
    {
        var plan = PlanFor(new RenameRequest('E', 'F', "Kho"));
        var e = plan.Before.FindByLetter('E')!;
        DiskLayout With(char? letter, string? label) => plan.Before with
        {
            Partitions = [.. plan.Before.Partitions.Select(p => p.Offset == e.Offset ? p with { DriveLetter = letter, Label = label } : p)],
        };

        Assert.Empty(SafetyGuard.CheckActual(plan, With('F', "KHO")));
        Assert.NotEmpty(SafetyGuard.CheckActual(plan, With('E', "Kho")));
        Assert.NotEmpty(SafetyGuard.CheckActual(plan, With('F', "Data")));
    }

    // ---------- Chay that tren o gia + tiem loi ----------

    private static (FakeBackend Backend, RunJournal Journal) Run(Plan plan, Action<FakeBackend>? arrange = null, RunOptions? options = null)
    {
        var backend = new FakeBackend(plan.Before, WinReOnPartition4);
        arrange?.Invoke(backend);
        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, options ?? Real);
        return (backend, journal);
    }

    [Fact]
    public void Success_renames_and_relabels_without_touching_geometry()
    {
        var plan = PlanFor(new RenameRequest('E', 'F', "Kho"));

        var (backend, journal) = Run(plan);

        Assert.Equal(RunState.Succeeded, journal.State);
        Assert.Equal(["health", "set-label", "set-letter"], backend.Calls);
        Assert.Equal("Kho", backend.Layout.FindByLetter('F')!.Label);
        Assert.Equal(plan.LayoutFingerprint, LayoutFingerprint.Compute(backend.Layout));
    }

    [Fact]
    public void Dry_run_changes_no_name()
    {
        var plan = PlanFor(new RenameRequest('E', 'F', "Kho"));

        var (backend, journal) = Run(plan, options: Real with { DryRun = true });

        Assert.Equal(RunState.DryRunCompleted, journal.State);
        Assert.Equal(["health"], backend.Calls);
        Assert.Same(plan.Before, backend.Layout);
    }

    [Fact]
    public void Letter_failure_restores_the_label()
    {
        var plan = PlanFor(new RenameRequest('E', 'F', "Kho"));

        var (backend, journal) = Run(plan, b => b.FailOn.Add("set-letter"));

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal("Data", backend.Layout.FindByLetter('E')!.Label);
        Assert.Null(backend.Layout.FindByLetter('F'));
    }

    [Fact]
    public void Letter_taken_after_planning_stops_before_assigning()
    {
        var plan = PlanFor(new RenameRequest('E', 'F', "Kho"));

        var (backend, journal) = Run(plan, b => b.OtherLetters.Add('F'));

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.DoesNotContain("set-letter", backend.Calls);
        Assert.Equal("Data", backend.Layout.FindByLetter('E')!.Label);
    }

    [Fact]
    public void Wrong_letter_from_Windows_is_detected_and_undone_from_the_actual_letter()
    {
        var plan = PlanFor(new RenameRequest('E', 'F'));

        var (backend, journal) = Run(plan, b => b.LetterActuallyAssigned = 'H');

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.NotNull(backend.Layout.FindByLetter('E'));
        Assert.Null(backend.Layout.FindByLetter('H'));
        Assert.Equal(["health", "set-letter", "set-letter"], backend.Calls);
    }

    [Fact]
    public void Letter_changed_by_someone_else_after_planning_stops_without_writing()
    {
        // Dau van tay bo qua chu cai/nhan, nen buoc kiem lai truoc khi ghi phai tu bat duoc truong hop nay.
        var plan = PlanFor(new RenameRequest('E', 'F', "Kho"));
        var moved = plan.Before with
        {
            Partitions = [.. plan.Before.Partitions.Select(p => p.DriveLetter == 'E' ? p with { DriveLetter = 'X' } : p)],
        };
        var backend = new FakeBackend(moved, WinReOnPartition4);

        var journal = new PlanRunner(backend, new MemoryJournalStore()).Run(plan, Real);

        Assert.Equal(RunState.RolledBack, journal.State);
        Assert.Equal(["health"], backend.Calls);
    }

    [Fact]
    public void Advisor_explains_how_to_restore_a_missing_letter()
    {
        var plan = PlanFor(new RenameRequest('E', 'F'));
        var steps = plan.Steps.Select(s => new StepRecord(s.Index, s.Kind, s.Title,
            s.Kind == StepKind.SetDriveLetter ? StepStatus.Running : s.Index == 1 ? StepStatus.Done : StepStatus.Pending,
            null, null, null)).ToList();

        var advice = RecoveryAdvisor.Advise(new RunJournal("r", plan, RunState.Running, steps, [], "test"));

        Assert.Contains(advice, a => a.Contains("Disk Management", StringComparison.Ordinal) && a.Contains("E:", StringComparison.Ordinal));
    }
}
