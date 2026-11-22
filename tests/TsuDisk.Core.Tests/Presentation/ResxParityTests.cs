using System.Xml.Linq;

namespace TsuDisk.Core.Tests.Presentation;

/// <summary>
/// PRODUCT_POLICY muc 1.1: giao dien co du tieng Viet + tieng Anh trong tep dich. Doc thang .resx cua TsuDisk.Gui (du
/// project WPF khong chay tren Linux): hai tep cung bo khoa, khoa nao XAML/ma dung cung co.
/// </summary>
public class ResxParityTests
{
    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Gui", "Resources", file))
            .Root!.Elements("data").ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")!.Value);

    [Fact]
    public void ViAndEn_HaveSameKeys()
    {
        var en = Load("Strings.resx");
        var vi = Load("Strings.vi.resx");
        Assert.Equal(en.Keys.Order(), vi.Keys.Order());
    }

    [Fact]
    public void EveryPartitionKind_HasLabel()
    {
        var en = Load("Strings.resx");
        foreach (var kind in Enum.GetNames<TsuDisk.Core.Model.PartitionKind>())
            Assert.True(en.ContainsKey($"Kind_{kind}"), $"Thiếu Kind_{kind}");
    }

    [Fact]
    public void KeysUsedInGui_Exist()
    {
        var keys = Load("Strings.resx").Keys.ToHashSet();
        var dir = DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Gui");
        var pattern = new System.Text.RegularExpressions.Regex(@"\{g:T (\w+)\}|(?:Loc\.Instance|\bl|\bL)(?:\.Format\(|\[)""(\w+)""");
        var used = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".xaml", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(f => pattern.Matches(File.ReadAllText(f)))
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            .ToHashSet();
        Assert.NotEmpty(used);
        Assert.Empty(used.Except(keys));
    }

    [Fact]
    public void NoEmDash_InStrings()
    {
        foreach (var file in new[] { "Strings.resx", "Strings.vi.resx" })
            Assert.DoesNotContain(Load(file).Values, v => v.Contains((char)0x2014, StringComparison.Ordinal));
    }
}
