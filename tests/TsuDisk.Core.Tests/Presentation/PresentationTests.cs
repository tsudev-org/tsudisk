using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Core.Tests.Presentation;

/// <summary>PRODUCT_POLICY muc 1.4: khoi dong voi ngon ngu he thong vi, en, ja; che do he thong sang/toi.</summary>
public class UiPreferenceTests
{
    [Theory]
    [InlineData("vi-VN", "vi")]
    [InlineData("en-US", "en")]
    [InlineData("ja-JP", "en")]
    public void FirstRun_FollowsSystemLanguage_FallbackEnglish(string system, string expected) =>
        Assert.Equal(expected, UiLanguage.Resolve(null, [system]));

    [Fact]
    public void FirstRun_UsesFirstSupportedSystemLanguage() =>
        Assert.Equal("vi", UiLanguage.Resolve(UiLanguage.System, ["ja-JP", "vi-VN", "en-US"]));

    [Fact]
    public void SavedLanguage_BeatsSystem() => Assert.Equal("vi", UiLanguage.Resolve("vi", ["en-US"]));

    [Fact]
    public void SavedUnsupportedLanguage_FallsBackToSystem() => Assert.Equal("en", UiLanguage.Resolve("fr", ["en-GB"]));

    [Theory]
    [InlineData(true, ThemeMode.Light)]
    [InlineData(false, ThemeMode.Dark)]
    public void SystemTheme_FollowsOs(bool systemLight, ThemeMode expected) =>
        Assert.Equal(expected, ThemeResolver.Resolve(ThemeChoice.System, systemLight));

    [Fact]
    public void ExplicitTheme_BeatsOs()
    {
        Assert.Equal(ThemeMode.Dark, ThemeResolver.Resolve(ThemeChoice.Dark, systemUsesLight: true));
        Assert.Equal(ThemeMode.Light, ThemeResolver.Resolve(ThemeChoice.Light, systemUsesLight: false));
    }

    [Fact]
    public void Settings_FirstRun_IsSystemAndTen()
    {
        var s = GuiSettings.Parse(null);
        Assert.Equal(UiLanguage.System, s.Language);
        Assert.Equal(ThemeChoice.System, s.Theme);
        Assert.Equal(10, s.PageSize("phan-vung"));
    }

    [Fact]
    public void Settings_RoundTrip_PerTableKey()
    {
        var s = GuiSettings.Default.WithPageSize("phan-vung", 50) with { Language = "en", Theme = ThemeChoice.Dark };
        var json = s.ToJson();
        Assert.Contains("tsudev.pagesize.phan-vung", json, StringComparison.Ordinal);
        var back = GuiSettings.Parse(json);
        Assert.Equal("en", back.Language);
        Assert.Equal(ThemeChoice.Dark, back.Theme);
        Assert.Equal(50, back.PageSize("phan-vung"));
        Assert.Equal(10, back.PageSize("vung-trong"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("""{"language":42,"theme":"tim","pageSizes":{"tsudev.pagesize.phan-vung":25}}""")]
    public void Settings_Garbage_FallsBackToDefaults(string json)
    {
        var s = GuiSettings.Parse(json);
        Assert.Equal(UiLanguage.System, s.Language);
        Assert.Equal(ThemeChoice.System, s.Theme);
        Assert.Equal(10, s.PageSize("phan-vung"));
    }
}

/// <summary>DATA_TABLE muc 2, 4, 6.</summary>
public class PageWindowTests
{
    [Fact]
    public void Default_IsTen_AndSizesAreStandard()
    {
        Assert.Equal([10, 20, 50, 100, 200], PageWindow.Sizes);
        Assert.Equal(10, new PageWindow(128).PageSize);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public void NonStandardSize_IsRejected(int size) => Assert.Equal(10, new PageWindow(5, size).PageSize);

    [Fact]
    public void ChangeSize_KeepsFirstVisibleRecord()
    {
        // Dang xem dong 341-350 (trang 35 moc 10) -> moc 100: trang floor(340/100)+1 = 4.
        var w = new PageWindow(1000, 10, 35).WithPageSize(100);
        Assert.Equal(4, w.Page);
        Assert.Equal(300, w.FirstIndex);
    }

    [Fact]
    public void LastPage_Partial()
    {
        var w = new PageWindow(128, 10, 13);
        Assert.Equal(120, w.FirstIndex);
        Assert.Equal(8, w.CountOnPage);
        Assert.False(w.CanGoNext);
        Assert.True(w.CanGoPrevious);
    }

    [Fact]
    public void Empty_HasOnePage_NothingToShow()
    {
        var w = new PageWindow(0);
        Assert.Equal(1, w.PageCount);
        Assert.Equal(0, w.CountOnPage);
        Assert.False(w.CanGoNext);
    }

    [Fact]
    public void PageCells_AtMostSeven()
    {
        Assert.Equal([1, 2, 3, 4, 5, 6, 7], new PageWindow(70).PageCells());
        Assert.Equal([1, 2, 3, 4, 5, null, 13], new PageWindow(128, 10, 2).PageCells());
        Assert.Equal([1, null, 5, 6, 7, null, 13], new PageWindow(128, 10, 6).PageCells());
        Assert.Equal([1, null, 9, 10, 11, 12, 13], new PageWindow(128, 10, 12).PageCells());
        for (var p = 1; p <= 13; p++)
            Assert.True(new PageWindow(128, 10, p).PageCells().Count <= 7);
    }

    [Fact]
    public void Slice_TakesCurrentPage() =>
        Assert.Equal([21, 22, 23], new PageWindow(23, 10, 3).Slice(Enumerable.Range(1, 23)));
}

public class DiskMapTests
{
    [Fact]
    public void RealMachine_EverySegmentVisible_SumIsOne()
    {
        var blocks = DiskMap.Build(Fixtures.RealMachine20261116());
        Assert.Equal(Fixtures.RealMachine20261116().Segments().Count, blocks.Count);
        Assert.Equal(1.0, blocks.Sum(b => b.Fraction), 9);
        Assert.All(blocks, b => Assert.True(b.Fraction >= DiskMap.DefaultMinFraction - 1e-9));
        // C: (265 GB) van rong nhat - nong khoi nho khong lam dao thu tu kich thuoc.
        Assert.Equal('C', blocks.MaxBy(b => b.Fraction)!.Segment.Partition!.DriveLetter);
    }

    [Fact]
    public void Roles_FollowPartitionKind()
    {
        var roles = DiskMap.Build(Fixtures.RealMachine20261116()).Select(b => b.Role).ToList();
        Assert.Equal([MapRole.System, MapRole.System, MapRole.Data, MapRole.Recovery, MapRole.Free, MapRole.Data, MapRole.Data, MapRole.Free], roles);
    }

    [Fact]
    public void TooManyBlocks_EqualWidths()
    {
        var w = DiskMap.Widths([1, 1000, 5], 0.5);
        Assert.All(w, f => Assert.Equal(1.0 / 3, f, 9));
    }

    [Fact]
    public void Widths_ProportionalAboveMinimum()
    {
        var w = DiskMap.Widths([100, 300, 1], 0.1);
        Assert.Equal(1.0, w.Sum(), 9);
        Assert.Equal(0.1, w[2], 9);
        Assert.Equal(3.0, w[1] / w[0], 9);
    }

    [Fact]
    public void EmptyDisk_IsOneFreeBlock()
    {
        var blocks = DiskMap.Build(new DiskLayout(Fixtures.Disk(10 * Units.GiB), []));
        var only = Assert.Single(blocks);
        Assert.Equal(MapRole.Free, only.Role);
        Assert.Equal(1.0, only.Fraction, 9);
    }
}

public class DesignTokenTests
{
    private static string Token(params string[] parts) =>
        File.ReadAllText(DocumentationConsistencyTests.RepoPath([".standards", "tokens", .. parts]));

    [Theory]
    [InlineData(ThemeMode.Light, "#04776B")]   // tokens v8.0.0
    [InlineData(ThemeMode.Dark, "#24B9A8")]
    public void ProductPrimary_OverridesBase(ThemeMode mode, string primary)
    {
        var t = DesignTokens.Build(Token("design-tokens.json"), Token("products", "disk.json"), mode);
        Assert.Equal(primary, t.Colors["primary"]);
        Assert.Equal("disk", t.ProductId);
    }

    [Fact]
    public void LightAndDark_HaveSameRoles_AndAllParse()
    {
        var light = DesignTokens.Build(Token("design-tokens.json"), Token("products", "disk.json"), ThemeMode.Light);
        var dark = DesignTokens.Build(Token("design-tokens.json"), Token("products", "disk.json"), ThemeMode.Dark);
        Assert.Equal(light.Colors.Keys.Order(), dark.Colors.Keys.Order());
        foreach (var role in light.Colors.Keys)
        {
            _ = light.Color(role);
            _ = dark.Color(role);
        }
        Assert.NotEqual(light.Colors["bg-base"], dark.Colors["bg-base"]);
    }

    [Fact]
    public void Shape_Chuan_AndDisplayFont()
    {
        var t = DesignTokens.Build(Token("design-tokens.json"), Token("products", "disk.json"), ThemeMode.Light);
        Assert.Equal(6, t.Radius["control"]);
        Assert.Equal(8, t.Radius["lg"]);
        Assert.StartsWith("Montserrat, ", t.FontDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("sans-serif", t.FontFamily, StringComparison.Ordinal);
        Assert.DoesNotContain("'", t.FontFamily, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownRoleInProduct_IsRejected()
    {
        var product = """{"id":"x","shape":"chuan","colors":{"light":{"tim-la":"#000000"},"dark":{}}}""";
        Assert.Throws<FormatException>(() => DesignTokens.Build(Token("design-tokens.json"), product, ThemeMode.Light));
    }

    [Theory]
    [InlineData("#0A0B0C", 255, 10, 11, 12)]
    [InlineData("rgba(8, 120, 108, 0.1)", 26, 8, 120, 108)]
    public void TokenColor_Parses(string value, int a, int r, int g, int b) =>
        Assert.Equal(new TokenColor((byte)a, (byte)r, (byte)g, (byte)b), TokenColor.Parse(value));

    [Fact]
    public void TokenColor_RejectsGarbage() => Assert.Throws<FormatException>(() => TokenColor.Parse("blue"));
}

public class PlanTextTests
{
    [Fact]
    public void ExtendPlan_HeadlineAndInvariants()
    {
        var result = ExtendPlanner.Plan(Fixtures.DirectGap(), WinReStatus.Disabled, new ExtendRequest('C'));
        Assert.True(result.Success);
        Assert.StartsWith($"KẾ HOẠCH {result.Plan!.Id}: mở rộng C: trên đĩa 0", PlanText.Headline(result.Plan), StringComparison.Ordinal);
        Assert.Equal(2, PlanText.Invariants(result.Plan).Count);
    }
}

public class UnitsCultureTests
{
    [Fact]
    public void FormatIn_FollowsDisplayLanguage()
    {
        const long size = 68_610_000_000;
        Assert.Equal(Units.Format(size), Units.FormatIn(size, System.Globalization.CultureInfo.GetCultureInfo("vi-VN")));
        Assert.Equal("63.9 GB", Units.FormatIn(size, System.Globalization.CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("63,9 GB", Units.Format(size));
    }
}
