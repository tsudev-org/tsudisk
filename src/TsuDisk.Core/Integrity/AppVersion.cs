using System.Globalization;
using System.Text.RegularExpressions;

namespace TsuDisk.Core.Integrity;

/// <summary>
/// Phien ban app theo VERSIONING.md muc 2 (<c>YY.M.DDNN</c>, ban thu <c>-beta.N</c>/<c>-rc.N</c>) - so theo luat SemVer
/// (ban thu NHO hon ban chinh cung so: <c>26.11.2601-beta.1</c> &lt; <c>26.11.2601</c>, khop cong cua tsudev.com).
/// Khuon giong <see cref="Pro.ProContract.AppHeaderValue"/>: ban tu dung (vd. "0") khong parse duoc ⇒ cong khong ap dung.
/// </summary>
public sealed partial class AppVersion : IComparable<AppVersion>, IEquatable<AppVersion>
{
    private AppVersion(int major, int minor, int patch, string? prerelease, string text)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        Text = text;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    /// <summary>Phan sau dau '-' (vd. <c>beta.1</c>), null neu la ban chinh thuc.</summary>
    public string? Prerelease { get; }

    /// <summary>Chuoi goc dung de hien thi (nguyen van modal khoa).</summary>
    public string Text { get; }

    [GeneratedRegex(@"^(\d{1,4})\.(\d{1,4})\.(\d{1,6})(?:-([0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    public static AppVersion? TryParse(string? text)
    {
        if (text is null || text.Length > 48) return null;
        var m = Shape().Match(text);
        if (!m.Success) return null;
        var pre = m.Groups[4].Success ? m.Groups[4].Value : null;
        if (pre is { Length: > 32 }) return null;
        return new AppVersion(
            int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
            pre, text);
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null) return 1;
        var c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        return ComparePrerelease(Prerelease, other.Prerelease);
    }

    /// <summary>SemVer 2.0.0 muc 11: khong co ban thu &gt; co ban thu; tung dinh danh: so so theo gia tri, chu so theo ASCII, so &lt; chu.</summary>
    private static int ComparePrerelease(string? a, string? b)
    {
        if (a is null) return b is null ? 0 : 1;
        if (b is null) return -1;
        var x = a.Split('.');
        var y = b.Split('.');
        for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
        {
            var xn = IsNumeric(x[i]);
            var yn = IsNumeric(y[i]);
            int c;
            if (xn && yn)
            {
                // So dai hon (bo so 0 dau) lon hon; cung do dai thi so ky tu - khong tran so voi dinh danh rat dai.
                var xs = x[i].TrimStart('0');
                var ys = y[i].TrimStart('0');
                c = xs.Length != ys.Length ? xs.Length.CompareTo(ys.Length) : string.CompareOrdinal(xs, ys);
            }
            else if (xn) c = -1;
            else if (yn) c = 1;
            else c = string.CompareOrdinal(x[i], y[i]);
            if (c != 0) return c;
        }
        return x.Length.CompareTo(y.Length);
    }

    private static bool IsNumeric(string s)
    {
        foreach (var ch in s)
            if (ch is < '0' or > '9') return false;
        return s.Length > 0;
    }

    public bool Equals(AppVersion? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is AppVersion v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Prerelease);
    public override string ToString() => Text;

    public static bool operator ==(AppVersion? a, AppVersion? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(AppVersion? a, AppVersion? b) => !(a == b);
    public static bool operator <(AppVersion? a, AppVersion? b) => a is null ? b is not null : a.CompareTo(b) < 0;
    public static bool operator >(AppVersion? a, AppVersion? b) => a is not null && a.CompareTo(b) > 0;
    public static bool operator <=(AppVersion? a, AppVersion? b) => !(a > b);
    public static bool operator >=(AppVersion? a, AppVersion? b) => !(a < b);
}
