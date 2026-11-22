namespace TsuDisk.Core.Pro;

/// <summary>Ngon ngu cua noi dung Pro. Chi hai ngon ngu (PRODUCT_POLICY muc 1).</summary>
public enum AppLanguage { Vi, En }

/// <summary>
/// Chon chuoi vi/en cho noi dung Pro (lay tu tsuswico - cung hop dong PRODUCT_POLICY 7.3/7.6). Cap chuoi nam canh
/// nhau thay vi .resx: trinh bien dich bat thieu ban dich, khong can satellite assembly trong ban mot tep.
/// CLI TsuDisk hien chi co tieng Viet ⇒ mac dinh Vi; GUI (giai doan 2) dat theo he thong qua
/// <see cref="SetProcessLanguage"/>. Test dung <see cref="Use"/> (theo luong bat dong bo).
/// </summary>
public static class Loc
{
    private static AppLanguage _process = AppLanguage.Vi;
    private static readonly AsyncLocal<AppLanguage?> Scoped = new();

    public static AppLanguage Current => Scoped.Value ?? _process;

    public static bool IsEnglish => Current == AppLanguage.En;

    public static void SetProcessLanguage(AppLanguage language) => _process = language;

    /// <summary>Doi ngon ngu trong pham vi <c>using</c> (luong bat dong bo hien tai).</summary>
    public static IDisposable Use(AppLanguage language)
    {
        var previous = Scoped.Value;
        Scoped.Value = language;
        return new Restore(previous);
    }

    /// <summary>Chuoi theo ngon ngu dang dung.</summary>
    public static string T(string vi, string en) => IsEnglish ? en : vi;

    /// <summary>Gia tri thuoc tinh <c>lang</c> cua trang HTML (trang ket qua dang nhap).</summary>
    public static string HtmlLang => IsEnglish ? "en" : "vi";

    private sealed class Restore(AppLanguage? previous) : IDisposable
    {
        public void Dispose() => Scoped.Value = previous;
    }
}
