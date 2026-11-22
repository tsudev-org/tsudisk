using System.Text.Json;
using System.Text.Json.Nodes;

namespace TsuDisk.Core.Presentation;

/// <summary>Lua chon giao dien cua nguoi dung. Lan chay dau = System (PRODUCT_POLICY muc 1.2).</summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>Che do mau thuc dang ve.</summary>
public enum ThemeMode
{
    Light,
    Dark,
}

/// <summary>
/// Chon ngon ngu giao dien theo PRODUCT_POLICY muc 1.1: lua chon da luu thang; chua luu (hoac "system") thi theo ngon
/// ngu he dieu hanh; he dieu hanh dung ngon ngu chua ho tro thi tieng Anh.
/// </summary>
public static class UiLanguage
{
    public const string System = "system";
    public const string Fallback = "en";
    public static readonly IReadOnlyList<string> Supported = ["vi", "en"];

    public static string Resolve(string? saved, IEnumerable<string> systemLanguages)
    {
        ArgumentNullException.ThrowIfNull(systemLanguages);
        if (Normalize(saved) is { } chosen)
            return chosen;
        foreach (var language in systemLanguages)
        {
            if (Normalize(language) is { } match)
                return match;
        }
        return Fallback;
    }

    /// <summary>"vi-VN" -> "vi"; ngon ngu chua ho tro, rong hoac "system" -> null.</summary>
    public static string? Normalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var primary = tag.Split('-', '_')[0].Trim().ToLowerInvariant();
        return Supported.Contains(primary) ? primary : null;
    }
}

public static class ThemeResolver
{
    public static ThemeMode Resolve(ThemeChoice choice, bool systemUsesLight) => choice switch
    {
        ThemeChoice.Light => ThemeMode.Light,
        ThemeChoice.Dark => ThemeMode.Dark,
        _ => systemUsesLight ? ThemeMode.Light : ThemeMode.Dark,
    };
}

/// <summary>
/// Cai dat giao dien luu o tep cau hinh nguoi dung (DATA_TABLE muc 5: desktop luu vao tep, khong vao registry). Tep hong
/// hoac gia tri la deu quay ve mac dinh - day la tien nghi hien thi, mat cung khong sao.
/// </summary>
public sealed record GuiSettings(string Language, ThemeChoice Theme, IReadOnlyDictionary<string, int> PageSizes)
{
    public static GuiSettings Default { get; } = new(UiLanguage.System, ThemeChoice.System, new Dictionary<string, int>());

    /// <summary>Khoa ghi nho so ban ghi theo tung bang: <c>tsudev.pagesize.&lt;ma-bang&gt;</c>.</summary>
    public static string PageSizeKey(string tableId) => $"tsudev.pagesize.{tableId}";

    public int PageSize(string tableId) =>
        PageWindow.Sanitize(PageSizes.TryGetValue(PageSizeKey(tableId), out var size) ? size : null);

    public GuiSettings WithPageSize(string tableId, int size) =>
        this with { PageSizes = new Dictionary<string, int>(PageSizes) { [PageSizeKey(tableId)] = PageWindow.Sanitize(size) } };

    public static GuiSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return Default;
            var language = root["language"]?.GetValueKind() == JsonValueKind.String ? root["language"]!.GetValue<string>() : null;
            language = UiLanguage.Normalize(language) ?? UiLanguage.System;
            var theme = root["theme"]?.GetValueKind() == JsonValueKind.String
                && Enum.TryParse<ThemeChoice>(root["theme"]!.GetValue<string>(), ignoreCase: true, out var t)
                && Enum.IsDefined(t) ? t : ThemeChoice.System;
            var sizes = new Dictionary<string, int>();
            if (root["pageSizes"] is JsonObject map)
            {
                foreach (var (key, value) in map)
                {
                    if (key.StartsWith("tsudev.pagesize.", StringComparison.Ordinal) && value?.GetValueKind() == JsonValueKind.Number
                        && value.AsValue().TryGetValue<int>(out var size))
                        sizes[key] = PageWindow.Sanitize(size);
                }
            }
            return new GuiSettings(language, theme, sizes);
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson()
    {
        var sizes = new JsonObject();
        foreach (var (key, value) in PageSizes.OrderBy(p => p.Key, StringComparer.Ordinal))
            sizes[key] = value;
        var root = new JsonObject
        {
            ["language"] = Language,
            ["theme"] = Theme.ToString().ToLowerInvariant(),
            ["pageSizes"] = sizes,
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
