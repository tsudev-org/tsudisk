using System.Text.RegularExpressions;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Core.Tests.Presentation;

/// <summary>
/// UI1 (phieu 20261124-UI1, DESIGN_SYSTEM muc 1 "Bieu tuong tren nen trang thai"): moi do hoa mang nghia cua GUI (mui
/// ten ComboBox, vong focus, o chu giai + khoi ban do phan vung) &gt;= 3:1 so voi NEN THAT phia sau o MOI trang thai, ca
/// Sang lan Toi (WCAG 1.4.11). Test quet XAML chan tai phat: them Path/Rectangle/Ellipse to bang vai tro moi ma chua
/// khai nen o day ⇒ do.
/// </summary>
public class IconContrastTests
{
    /// <summary>Vai tro ve do hoa ⇒ moi nen no nam tren (thuong, di chuot, dang chon).</summary>
    private static readonly Dictionary<string, string[]> Surfaces = new(StringComparer.Ordinal)
    {
        ["text-secondary"] = ["bg-surface", "bg-hover"],          // mui ten ComboBox: thuong / di chuot
        ["focus-ring"] = ["bg-base", "bg-surface", "bg-subtle"],  // vong focus nam ngoai dieu khien
        ["primary"] = ["bg-surface"],                             // khoi + o chu giai "Du lieu" trong the
        ["info"] = ["bg-surface"],                                // "He thong"
        ["warning"] = ["bg-surface"],                             // "Phuc hoi"
        ["text-muted"] = ["bg-surface"],                          // "Khac"
        // Vien o + khoi "Trong" (nen bg-subtle co chu dich nhat). Truoc UI1 la border-strong = 1,56:1 (Sang) / 1,47:1 (Toi).
        ["border-control"] = ["bg-surface"],
    };

    /// <summary>Nen nhat co chu dich (vung trong) - chi hop le khi cung phan tu co vien thuoc <see cref="Surfaces"/>.</summary>
    private static readonly HashSet<string> OutlinedFills = new(StringComparer.Ordinal) { "bg-subtle" };

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Do_hoa_GUI_dat_3_1_tren_moi_nen(ThemeMode mode)
    {
        var t = DesignTokens.Build(Token("design-tokens.json"), Token("products", "disk.json"), mode);
        var failures = (from pair in Surfaces
                        from bg in pair.Value
                        let ratio = Contrast(t.Color(pair.Key), t.Color(bg))
                        where ratio < 3.0
                        select $"{mode} {pair.Key}/{bg} = {ratio:0.00}").ToList();
        Assert.Empty(failures);
    }

    [Fact]
    public void Moi_do_hoa_trong_XAML_dung_vai_tro_da_khai_nen()
    {
        var dir = DocumentationConsistencyTests.RepoPath("src", "TsuDisk.Gui");
        var element = new Regex(@"<(Path|Rectangle|Ellipse|Polyline|Polygon|Line)\b[^>]*>", RegexOptions.Singleline);
        var brush = new Regex(@"\b(Stroke|Fill)=""\{DynamicResource ([\w-]+)\}""");
        var problems = new List<string>();
        var seen = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.xaml", SearchOption.TopDirectoryOnly))
        {
            foreach (Match m in element.Matches(File.ReadAllText(file)))
            {
                var brushes = brush.Matches(m.Value).Select(b => (Kind: b.Groups[1].Value, Role: b.Groups[2].Value)).ToList();
                var hasOutline = brushes.Any(b => b.Kind == "Stroke" && Surfaces.ContainsKey(b.Role));
                foreach (var (kind, role) in brushes)
                {
                    seen++;
                    if (Surfaces.ContainsKey(role) || (kind == "Fill" && OutlinedFills.Contains(role) && hasOutline))
                        continue;
                    problems.Add($"{Path.GetFileName(file)}: {kind}={role} chua khai nen trong IconContrastTests");
                }
                if (brushes.Count == 0 && Regex.IsMatch(m.Value, @"\b(Stroke|Fill)=""(#|[A-Z][a-z]+"")"))
                    problems.Add($"{Path.GetFileName(file)}: mau go cung tren do hoa, phai dung token: {m.Value}");
            }
        }
        Assert.True(seen >= 7, $"chi thay {seen} co ve - mau quet XAML co the da hong");
        Assert.Empty(problems);
    }

    private static string Token(params string[] parts) =>
        File.ReadAllText(DocumentationConsistencyTests.RepoPath([".standards", "tokens", .. parts]));

    private static double Contrast(TokenColor a, TokenColor b)
    {
        static double L(TokenColor c)
        {
            static double Ch(byte v)
            {
                var s = v / 255.0;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }
        var (x, y) = (L(a), L(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
}
