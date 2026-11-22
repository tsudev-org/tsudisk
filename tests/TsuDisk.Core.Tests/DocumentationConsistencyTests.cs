using System.Text.RegularExpressions;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using static TsuDisk.Core.Tests.Fixtures;

namespace TsuDisk.Core.Tests;

/// <summary>
/// Noi dung cong khai (website) phai khop hanh vi that cua chuong trinh. Lan dau da lech mot lan ("59,89 GB" trong khi
/// tsudisk in "59,9 GB") - test nay chan loai loi do: sua ExtendPlanner/Units ma quen sua trang chu thi do.
/// </summary>
public partial class DocumentationConsistencyTests
{
    internal static string RepoPath(params string[] parts) => RepoFile(parts);

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TsuDisk.slnx")))
            dir = dir.Parent;
        return Path.Combine([dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy gốc repo"), .. parts]);
    }

    [GeneratedRegex(@"^\s+(\d+)\.\s+(\[GHI\])?\s*(.+)$")]
    private static partial Regex StepLine();

    [Fact]
    public void Homepage_example_matches_real_planner_output_line_by_line()
    {
        var page = File.ReadAllText(RepoFile("website", "src", "pages", "index.astro"));
        var block = page[page.IndexOf("tsudisk ke-hoach C:", StringComparison.Ordinal)..page.IndexOf("</code></pre>", StringComparison.Ordinal)];
        var plan = ExtendPlanner.Plan(RealMachine20261116(), WinReOnPartition4, new ExtendRequest('C')).Plan!;

        Assert.Contains($"tăng {Units.Format(plan.GainBytes)}", block, StringComparison.Ordinal);
        var lines = block.Split('\n').Select(l => StepLine().Match(l)).Where(m => m.Success).ToList();
        Assert.Equal(plan.Steps.Count, lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            Assert.Equal(plan.Steps[i].Index.ToString(System.Globalization.CultureInfo.InvariantCulture), lines[i].Groups[1].Value);
            Assert.Equal(plan.Steps[i].ChangesDisk, lines[i].Groups[2].Success);
            Assert.Equal(plan.Steps[i].Title, lines[i].Groups[3].Value.Trim());
        }
    }

    [Fact]
    public void Readme_exit_codes_cover_every_documented_code()
    {
        var readme = File.ReadAllText(RepoFile("README.md"));
        foreach (var code in new[] { "`0`", "`1`", "`2`", "`3`", "`4`", "`5`" })
            Assert.Contains($"| {code} |", readme, StringComparison.Ordinal);
    }
}
