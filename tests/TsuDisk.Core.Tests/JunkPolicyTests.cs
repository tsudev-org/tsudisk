using TsuDisk.Core.Cleanup;

namespace TsuDisk.Core.Tests;

/// <summary>
/// ADR-0013: quet-rac chi bao cao va PHAI khong de xuat xoa nham. Cac ca duoi day la bai hoc thuc tu lan ra quet
/// 27/11/2026 (tep token tho quyen 644, bang chung Infatica, thu muc cai dat Windows, o ao WSL 190 GB).
/// </summary>
public class JunkPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Old = Now.AddDays(-10);
    private const string Temp = @"C:\Users\devng\AppData\Local\Temp\";

    private static JunkVerdict Verdict(string path, bool dir = false, DateTime? written = null) =>
        JunkPolicy.Assess(new JunkCandidate(path, dir, 1000, written ?? Old), Now).Verdict;

    [Fact]
    public void Old_plain_temp_file_is_junk() => Assert.Equal(JunkVerdict.Junk, Verdict(Temp + "ab12.tmp"));

    [Theory]
    [InlineData(@"C:\Windows\Temp\old.log")]
    [InlineData(@"C:\Windows\SoftwareDistribution\Download\abc.cab")]
    [InlineData("/mnt/c/Users/devng/.cache/pip/x.whl")]
    public void Known_windows_and_cache_roots_are_junk(string path) => Assert.Equal(JunkVerdict.Junk, Verdict(path));

    [Theory]
    [InlineData("api.env")]
    [InlineData(".env.production")]
    [InlineData("20261111-token1.txt")]
    [InlineData("id_ed25519")]
    [InlineData("server.pem")]
    [InlineData("release.keystore")]
    [InlineData("wallet.kdbx")]
    [InlineData("my-secret-notes.txt")]
    [InlineData("swap.vhdx")]
    [InlineData("ext4.vhdx")]
    public void Secret_and_disk_image_names_are_protected_even_inside_temp(string name) =>
        Assert.Equal(JunkVerdict.Protected, Verdict(Temp + name));

    [Theory]
    [InlineData(@"C:\ProgramData\lrpjava\SilverCluster.exe")]
    [InlineData(@"C:\Users\devng\AppData\Roaming\Precipitator\x.dat")]
    [InlineData(@"C:\Users\devng\AppData\Local\Temp\infatica\agent.log")]
    public void Security_incident_evidence_is_never_junk(string path) => Assert.Equal(JunkVerdict.Protected, Verdict(path));

    [Theory]
    [InlineData(@"C:\Windows\Installer\1234.msi")]
    [InlineData(@"C:\$Recycle.Bin\S-1-5\$R1.docx")]
    [InlineData(@"C:\Program Files\App\app.dll")]
    [InlineData(@"C:\Users\devng\Documents\bao-cao.docx")]
    [InlineData(@"C:\Users\devng\Downloads\setup.exe")]
    [InlineData(@"C:\Users\devng\OneDrive\x.txt")]
    [InlineData(@"C:\Users\devng\projects\tsudisk\.git\config")]
    [InlineData(@"C:\Users\devng\.ssh\known_hosts")]
    [InlineData(@"C:\Users\devng\AppData\Local\wsl\{guid}\data.bin")]
    public void User_data_system_and_key_stores_are_protected(string path) => Assert.Equal(JunkVerdict.Protected, Verdict(path));

    [Fact]
    public void Protection_wins_over_known_junk_root()
    {
        // Duong dan nam trong goc rac nhung di qua thu muc nguoi dung => van bao ve.
        Assert.Equal(JunkVerdict.Protected, Verdict(Temp + @"Documents\x.tmp"));
        Assert.Equal(JunkVerdict.Protected, Verdict(Temp + @".git\objects", dir: true));
    }

    [Fact]
    public void Recent_items_are_refused_because_they_may_be_in_use()
    {
        Assert.Equal(JunkVerdict.TooRecent, Verdict(Temp + "build.tmp", written: Now.AddHours(-5)));
        Assert.Equal(JunkVerdict.TooRecent, Verdict(Temp + "build.tmp", written: Now.AddDays(-2.9)));
        Assert.Equal(JunkVerdict.Junk, Verdict(Temp + "build.tmp", written: Now.AddDays(-3.1)));
    }

    [Fact]
    public void Clock_in_the_future_is_treated_as_recent() =>
        Assert.Equal(JunkVerdict.TooRecent, Verdict(Temp + "x.tmp", written: Now.AddDays(5)));

    [Theory]
    [InlineData(@"D:\du-an\bao-cao.txt")]
    [InlineData(@"C:\Users\devng\AppData\Local\Temp")]    // chinh goc rac, khong phai muc ben trong
    [InlineData(@"C:\Users\devng\AppData\Roaming\Zalo\cache.bin")]
    public void Outside_known_roots_the_policy_refuses_to_guess(string path) => Assert.Equal(JunkVerdict.Unknown, Verdict(path));

    [Fact]
    public void Directories_are_judged_by_the_same_rules()
    {
        Assert.Equal(JunkVerdict.Junk, Verdict(Temp + "tsft", dir: true));
        Assert.Equal(JunkVerdict.Protected, Verdict(Temp + "secrets-backup", dir: true));
    }

    [Fact]
    public void Summary_counts_only_junk_as_reclaimable()
    {
        var items = new[]
        {
            JunkPolicy.Assess(new JunkCandidate(Temp + "a.tmp", false, 100, Old), Now),
            JunkPolicy.Assess(new JunkCandidate(Temp + "b.pem", false, 40, Old), Now),
            JunkPolicy.Assess(new JunkCandidate(Temp + "c.tmp", false, 7, Now), Now),
            JunkPolicy.Assess(new JunkCandidate(@"D:\x.txt", false, 3, Old), Now),
        };
        var s = JunkPolicy.Summarize(items);
        Assert.Equal((100, 40, 7, 3), (s.ReclaimableBytes, s.ProtectedBytes, s.TooRecentBytes, s.UnknownBytes));
    }

    [Fact]
    public void Cleanup_code_has_no_delete_path()
    {
        // Bat bien an toan: quet-rac chi doc. Them duong xoa ⇒ phai co ADR rieng va sua test nay co chu y.
        var dirs = new[] { DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Core", "Cleanup"), DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Cli") };
        var files = Directory.GetFiles(dirs[0], "*.cs").Append(Path.Combine(dirs[1], "CommandsCleanup.cs"));
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var banned in new[] { "File.Delete", "Directory.Delete", ".Delete(", "Remove-Item", "rm -", "FileSystem.DeleteFile", "MoveToTrash" })
                Assert.DoesNotContain(banned, text, StringComparison.Ordinal);
        }
    }
}
