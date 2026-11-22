using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace TsuDisk.Gui;

/// <summary>
/// Chuoi giao dien theo ngon ngu dang chon. XAML gan qua <c>{g:T PlanTitle}</c>; doi ngon ngu => moi chuoi doi ngay (bao
/// "Item[]"), khong can mo lai cua so.
/// </summary>
internal sealed class Loc : INotifyPropertyChanged
{
    private static readonly ResourceManager Strings = new("TsuDisk.Gui.Resources.Strings", typeof(Loc).Assembly);

    public static Loc Instance { get; } = new();

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

    public string Language => Culture.TwoLetterISOLanguageName;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public string this[string key] => Strings.GetString(key, Culture) ?? key;

    public string Format(string key, params object[] args) => string.Format(Culture, this[key], args);

    public void SetLanguage(string language)
    {
        Culture = CultureInfo.GetCultureInfo(language == "vi" ? "vi-VN" : "en-US");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary><c>Text="{g:T PlanTitle}"</c> - rang buoc mot chieu toi <see cref="Loc"/>.</summary>
[MarkupExtensionReturnType(typeof(string))]
internal sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
