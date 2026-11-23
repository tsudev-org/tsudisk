using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Presentation;
using TsuDisk.Core.Serialization;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests.Presentation;

/// <summary>O "Doi chu cai/nhan" cua giao dien + duong chay chung CLI/GUI (ADR-0009).</summary>
public class RenameFormTests
{
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

    private static PartitionInfo Drive(char letter) => Machine().FindByLetter(letter)!;

    [Fact]
    public void Only_data_partitions_with_letter_and_file_system_can_be_renamed()
    {
        var layout = Machine();
        Assert.True(RenameForm.CanRename(Drive('E')));
        Assert.All(layout.Partitions.Where(p => p.Kind != PartitionKind.BasicData), p => Assert.False(RenameForm.CanRename(p)));
        Assert.False(RenameForm.CanRename(Drive('E') with { DriveLetter = null }));
        Assert.False(RenameForm.CanRename(Drive('E') with { FileSystem = null }));
        Assert.False(RenameForm.CanRename(null));
    }

    [Fact]
    public void Letter_choices_start_with_current_and_skip_letters_in_use()
    {
        var choices = RenameForm.LetterChoices('E', new HashSet<char> { 'C', 'D', 'E', 'G', 'Z' });

        Assert.Equal('E', choices[0]);
        Assert.Equal(['E', 'F', 'H'], choices.Take(3));
        Assert.DoesNotContain('A', choices);
        Assert.DoesNotContain('B', choices);
        Assert.DoesNotContain('G', choices);
        Assert.DoesNotContain('Z', choices);
        Assert.Equal(choices.Count, choices.Distinct().Count());
    }

    [Fact]
    public void Unchanged_fields_become_null_so_the_planner_keeps_them()
    {
        var e = Drive('E');

        var same = RenameForm.Request(e, 'e', "Data", 'c');
        Assert.Null(same.NewLetter);
        Assert.Null(same.NewLabel);
        Assert.Equal('C', same.SystemDrive);

        var both = RenameForm.Request(e, 'F', "", null);
        Assert.Equal('F', both.NewLetter);
        Assert.Equal("", both.NewLabel);   // chuoi rong = xoa nhan
    }

    [Fact]
    public void No_change_is_refused_by_the_planner_not_by_the_form()
    {
        var request = RenameForm.Request(Drive('E'), 'E', "Data", 'C');
        var result = RenamePlanner.Plan(Machine(), new HashSet<char>(), request);
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData('F', "Kho", "tsudisk doi-ten E: --chu-cai F --nhan Kho")]
    [InlineData('E', "Dữ liệu", "tsudisk doi-ten E: --nhan \"Dữ liệu\"")]
    [InlineData('E', "", "tsudisk doi-ten E: --nhan \"\"")]
    [InlineData('F', "Data", "tsudisk doi-ten E: --chu-cai F")]
    [InlineData('E', "a$b", "tsudisk doi-ten E: --nhan \"a$b\"")]
    public void Command_matches_cli_syntax(char letter, string label, string expected) =>
        Assert.Equal(expected, RenameForm.Command(RenameForm.Request(Drive('E'), letter, label, 'C')));

    [Theory]
    [InlineData("E", true)]
    [InlineData(" e: ", true)]
    [InlineData("E:", true)]
    [InlineData("F", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("EE", false)]
    public void Second_confirmation_requires_retyping_the_drive_letter(string? typed, bool ok) =>
        Assert.Equal(ok, RenameForm.ConfirmationMatches(typed, 'E'));

    // ---------- Duong chay chung (PlanExecution) ----------

    [Fact]
    public void Shared_run_writes_plan_before_running_and_reports_progress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tsudisk-test-{Guid.NewGuid():N}");
        try
        {
            var plan = RenamePlanner.Plan(Machine(), new HashSet<char>(), RenameForm.Request(Drive('E'), 'F', "Kho", 'C')).Plan!;
            var backend = new FakeBackend(plan.Before, WinReOnPartition4);
            var store = new MemoryJournalStore();
            string? storeDir = null;
            var progress = new List<RunState>();

            var (journal, dir) = PlanExecution.Run(backend, d => { storeDir = d; return store; }, plan, root, dryRun: false, "test",
                new DateTime(2026, 11, 23, 9, 5, 7, DateTimeKind.Local), j => progress.Add(j.State));

            Assert.Equal(RunState.Succeeded, journal.State);
            Assert.Equal(Path.Combine(root, $"20261123-090507-{plan.Id}"), dir);
            Assert.Equal(dir, storeDir);
            Assert.Equal(plan.Id, TsuDiskJson.DeserializePlan(File.ReadAllText(Path.Combine(dir, "plan.json")))!.Id);
            Assert.Equal(store.Saved.Count, progress.Count);
            Assert.True(progress.Count >= plan.Steps.Count);
            Assert.Equal('F', backend.Layout.Partitions.Single(p => p.Label == "Kho").DriveLetter);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Shared_run_rolls_back_on_injected_failure_like_the_cli()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tsudisk-test-{Guid.NewGuid():N}");
        try
        {
            var plan = RenamePlanner.Plan(Machine(), new HashSet<char>(), RenameForm.Request(Drive('E'), 'F', "Kho", 'C')).Plan!;
            var backend = new FakeBackend(plan.Before, WinReOnPartition4);
            backend.FailOn.Add("set-letter");

            var (journal, dir) = PlanExecution.Run(backend, _ => new MemoryJournalStore(), plan, root, dryRun: false, "test", DateTime.Now);

            Assert.Equal(RunState.RolledBack, journal.State);
            Assert.EndsWith(plan.Id, dir, StringComparison.Ordinal);
            Assert.Equal('E', backend.Layout.Partitions.Single(p => p.Offset == Drive('E').Offset).DriveLetter);
            Assert.Equal("Data", backend.Layout.Partitions.Single(p => p.Offset == Drive('E').Offset).Label);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Dry_run_directory_is_marked() =>
        Assert.EndsWith("-chay-thu", PlanExecution.RunDirectoryName(
            RenamePlanner.Plan(Machine(), new HashSet<char>(), RenameForm.Request(Drive('E'), 'F', "Data", 'C')).Plan!, DateTime.Now, dryRun: true),
            StringComparison.Ordinal);
}
