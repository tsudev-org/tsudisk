using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Presentation;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests.Presentation;

/// <summary>O "Tao phan vung trong vung trong" cua giao dien (ADR-0010) + duong chay chung CLI/GUI.</summary>
public class CreateFormTests
{
    private static DiskLayout Machine() => RealMachine20261116();

    private static Segment Gap(DiskLayout layout, int n = 0) => layout.FreeExtents()[n];

    [Fact]
    public void Free_segment_maps_to_the_same_extent_number_as_the_cli()
    {
        var layout = Machine();
        var cli = CreatePartitionPlanner.UsableExtents(layout);

        for (var i = 0; i < layout.FreeExtents().Count; i++)
        {
            var extent = CreateForm.ExtentFor(layout, Gap(layout, i));
            Assert.NotNull(extent);
            Assert.Equal(cli[i], extent);
            Assert.InRange(extent!.Offset, Gap(layout, i).Offset, Gap(layout, i).End);
            Assert.True(extent.End <= Gap(layout, i).End);
        }
    }

    [Fact]
    public void Partitions_and_missing_selection_have_no_extent()
    {
        var layout = Machine();
        Assert.All(layout.Segments().Where(s => !s.IsFree), s => Assert.Null(CreateForm.ExtentFor(layout, s)));
        Assert.Null(CreateForm.ExtentFor(layout, null));
    }

    [Fact]
    public void Free_segment_too_small_after_alignment_has_no_extent()
    {
        var layout = Machine();
        var tiny = new Segment(Gap(layout).Offset + 512, 40 * Units.MiB, null);
        Assert.Null(CreateForm.ExtentFor(layout, tiny));
    }

    [Fact]
    public void Letter_choices_are_free_letters_from_d_default_first()
    {
        var choices = CreateForm.LetterChoices(Machine(), new HashSet<char> { 'C', 'D', 'F', 'G', 'E' });

        Assert.Equal('H', choices[0]);
        Assert.DoesNotContain('C', choices);
        Assert.DoesNotContain('E', choices);   // co tren bo cuc
        Assert.DoesNotContain('F', choices);   // dang dung o noi khac
        Assert.All(choices, c => Assert.InRange(c, 'D', 'Z'));
        Assert.Equal(choices.Count, choices.Distinct().Count());
    }

    [Theory]
    [InlineData("", true, null)]
    [InlineData("  ", true, null)]
    [InlineData(null, true, null)]
    [InlineData("512", true, 512L)]
    [InlineData(" 1024 ", true, 1024L)]
    [InlineData("0", false, null)]
    [InlineData("-5", false, null)]
    [InlineData("1,5", false, null)]
    [InlineData("1.5", false, null)]
    [InlineData("10 GB", false, null)]
    [InlineData("99999999999999999999", false, null)]
    public void Size_text_is_whole_megabytes_or_empty(string? text, bool ok, long? mb)
    {
        Assert.Equal(ok, CreateForm.TryParseSizeMb(text, out var bytes));
        Assert.Equal(mb * Units.MiB, bytes);
    }

    [Fact]
    public void Request_targets_the_selected_extent_and_the_planner_accepts_it()
    {
        var layout = Machine();
        var extent = CreateForm.ExtentFor(layout, Gap(layout))!;

        var request = CreateForm.Request(extent, 512 * Units.MiB, "exFAT", "Kho", 'h');
        var result = CreatePartitionPlanner.Plan(layout, new HashSet<char>(), request);

        Assert.True(result.Success, result.Refusal?.Message);
        Assert.Equal(extent.Index, request.ExtentIndex);
        Assert.Equal('H', result.Plan!.TargetLetter);
        var created = result.Plan.Steps.Single(s => s.Kind == StepKind.CreatePartition);
        Assert.Equal(extent.Offset, created.Offset);
        Assert.Equal(512 * Units.MiB, created.Size);
    }

    [Fact]
    public void Virtual_only_flag_reaches_the_planner_and_refuses_a_physical_disk()
    {
        var layout = Machine();
        var request = CreateForm.Request(CreateForm.ExtentFor(layout, Gap(layout))!, null, "NTFS", "", null, virtualDiskOnly: true);

        Assert.False(CreatePartitionPlanner.Plan(layout, new HashSet<char>(), request).Success);
        var onVhd = layout with { Disk = layout.Disk with { IsVirtual = true } };
        Assert.True(CreatePartitionPlanner.Plan(onVhd, new HashSet<char>(), request).Success);
    }

    [Theory]
    [InlineData(null, "NTFS", "", null, "tsudisk tao-phan-vung --dia 0 --vung 1 --he-tep NTFS")]
    [InlineData(2048L, "exFAT", "Kho", 'h', "tsudisk tao-phan-vung --dia 0 --vung 1 --kich-thuoc-mb 2048 --he-tep exFAT --nhan Kho --chu-cai H")]
    [InlineData(null, "FAT32", "Dữ liệu", null, "tsudisk tao-phan-vung --dia 0 --vung 1 --he-tep FAT32 --nhan \"Dữ liệu\"")]
    public void Command_matches_cli_syntax(long? mb, string fs, string label, char? letter, string expected)
    {
        var layout = Machine();
        var request = CreateForm.Request(CreateForm.ExtentFor(layout, Gap(layout))!, mb * Units.MiB, fs, label, letter);
        Assert.Equal(expected, CreateForm.Command(0, request));
    }

    [Theory]
    [InlineData("3", true)]
    [InlineData(" 3 ", true)]
    [InlineData("03", false)]
    [InlineData("TAO", false)]
    [InlineData("H", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Second_confirmation_requires_retyping_the_disk_number(string? typed, bool ok) =>
        Assert.Equal(ok, CreateForm.ConfirmationMatches(typed, 3));

    [Fact]
    public void Shared_run_creates_and_rolls_back_only_the_new_partition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tsudisk-test-{Guid.NewGuid():N}");
        try
        {
            var layout = Machine();
            var request = CreateForm.Request(CreateForm.ExtentFor(layout, Gap(layout))!, 1024 * Units.MiB, "NTFS", "Moi", 'H');
            var plan = CreatePartitionPlanner.Plan(layout, new HashSet<char>(), request).Plan!;

            var ok = new FakeBackend(plan.Before, WinReOnPartition4);
            var (journal, _) = PlanExecution.Run(ok, _ => new MemoryJournalStore(), plan, root, dryRun: false, "test", DateTime.Now);
            Assert.Equal(RunState.Succeeded, journal.State);
            Assert.Equal(layout.Partitions.Count + 1, ok.Layout.Partitions.Count);
            Assert.Equal('H', ok.Layout.Partitions.Single(p => p.Offset == plan.Steps[2].Offset).DriveLetter);

            var failing = new FakeBackend(plan.Before, WinReOnPartition4);
            failing.FailOn.Add("format");
            var (rolled, _) = PlanExecution.Run(failing, _ => new MemoryJournalStore(), plan, root, dryRun: false, "test", DateTime.Now);
            Assert.Equal(RunState.RolledBack, rolled.State);
            Assert.Equal(plan.Before.Ordered, failing.Layout.Ordered);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
