using System.Globalization;
using System.Text.Json.Nodes;

namespace TsuDisk.Core.Presentation;

/// <summary>Mau ARGB doc tu token (<c>#RRGGBB</c> hoac <c>rgba(r, g, b, a)</c>).</summary>
public readonly record struct TokenColor(byte A, byte R, byte G, byte B)
{
    public static TokenColor Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var v = value.Trim();
        if (v.StartsWith('#') && v.Length == 7)
        {
            return new TokenColor(255,
                byte.Parse(v.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(v.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(v.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        if (v.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && v.EndsWith(')'))
        {
            var parts = v[5..^1].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 4)
            {
                var alpha = double.Parse(parts[3], CultureInfo.InvariantCulture);
                return new TokenColor((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255),
                    byte.Parse(parts[0], CultureInfo.InvariantCulture),
                    byte.Parse(parts[1], CultureInfo.InvariantCulture),
                    byte.Parse(parts[2], CultureInfo.InvariantCulture));
            }
        }
        throw new FormatException($"Token màu không hợp lệ: {value}");
    }
}

/// <summary>
/// Token giao dien cho desktop (DESIGN_SYSTEM muc 1.2.1 + 7): lop nen <c>design-tokens.json</c> gop de bang nhan cua
/// <c>products/&lt;id&gt;.json</c>; bo bo goc theo <c>shape</c>. Khong chep ma mau nao vao ma nguon app.
/// </summary>
public sealed class DesignTokens
{
    private DesignTokens(ThemeMode mode, IReadOnlyDictionary<string, string> colors, IReadOnlyDictionary<string, double> radius,
        string fontFamily, string fontDisplay, string fontMono, string productId)
    {
        Mode = mode;
        Colors = colors;
        Radius = radius;
        FontFamily = fontFamily;
        FontDisplay = fontDisplay;
        FontMono = fontMono;
        ProductId = productId;
    }

    public ThemeMode Mode { get; }
    public IReadOnlyDictionary<string, string> Colors { get; }
    public IReadOnlyDictionary<string, double> Radius { get; }
    public string FontFamily { get; }
    public string FontDisplay { get; }
    public string FontMono { get; }
    public string ProductId { get; }

    public TokenColor Color(string role) => TokenColor.Parse(Colors[role]);

    /// <summary>Bo goc theo DESIGN_SYSTEM muc 1.2.3: sm / md / lg / control (px).</summary>
    internal static readonly IReadOnlyDictionary<string, (double Sm, double Md, double Lg, double Control)> Shapes =
        new Dictionary<string, (double, double, double, double)>
        {
            ["sac"] = (2, 3, 4, 3),
            ["chuan"] = (4, 6, 8, 6),
            ["mem"] = (6, 10, 14, 10),
            ["tron"] = (8, 14, 20, 999),
        };

    public static DesignTokens Build(string baseJson, string productJson, ThemeMode mode)
    {
        var root = JsonNode.Parse(baseJson)?.AsObject() ?? throw new FormatException("design-tokens.json rỗng");
        var product = JsonNode.Parse(productJson)?.AsObject() ?? throw new FormatException("products/<id>.json rỗng");
        var key = mode == ThemeMode.Dark ? "dark" : "light";

        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in root["color"]![key]!.AsObject())
            colors[name] = value!.GetValue<string>();
        foreach (var (name, value) in product["colors"]![key]!.AsObject())
        {
            if (!colors.ContainsKey(name))
                throw new FormatException($"Sản phẩm khai vai trò màu lạ: {name}");
            colors[name] = value!.GetValue<string>();
        }

        var shapeName = product["shape"]?.GetValue<string>() ?? "chuan";
        if (!Shapes.TryGetValue(shapeName, out var shape))
            throw new FormatException($"Bộ bo góc lạ: {shapeName}");
        var radius = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["none"] = 0,
            ["sm"] = shape.Sm,
            ["md"] = shape.Md,
            ["lg"] = shape.Lg,
            ["control"] = shape.Control,
        };

        var typography = root["typography"]!.AsObject();
        var body = FontList(typography["font-family"]!.GetValue<string>());
        var display = product["fontDisplay"]?.GetValue<string>() is { Length: > 0 } d ? $"{d}, {body}" : body;
        return new DesignTokens(mode, colors, radius, body, display, FontList(typography["font-mono"]!.GetValue<string>()),
            product["id"]?.GetValue<string>() ?? "");
    }

    /// <summary>Danh sach font CSS -> chuoi FontFamily cua WPF: bo nhay, bo ho chung (sans-serif, system-ui...).</summary>
    internal static string FontList(string css)
    {
        string[] generic = ["sans-serif", "serif", "monospace", "system-ui", "-apple-system", "ui-sans-serif", "ui-monospace"];
        return string.Join(", ", css.Split(',')
            .Select(f => f.Trim().Trim('\'', '"'))
            .Where(f => f.Length > 0 && !generic.Contains(f, StringComparer.OrdinalIgnoreCase)));
    }
}
