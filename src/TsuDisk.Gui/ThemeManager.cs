using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Gui;

/// <summary>
/// Nap token (lop nen + ban sac <c>disk</c>, nhung luc build) vao tai nguyen ung dung. Che do "Theo he thong" doc
/// <c>AppsUseLightTheme</c> va doi NGAY khi Windows doi (PRODUCT_POLICY muc 1.2).
/// </summary>
internal static class ThemeManager
{
    private static readonly string BaseJson = ReadEmbedded("tokens/design-tokens.json");
    private static readonly string ProductJson = ReadEmbedded("tokens/disk.json");

    public static ThemeChoice Choice { get; private set; } = ThemeChoice.System;

    public static ThemeMode Mode { get; private set; } = ThemeMode.Light;

    public static event EventHandler? ThemeChanged;

    public static void Start(ThemeChoice choice)
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color && Choice == ThemeChoice.System)
                Application.Current?.Dispatcher.BeginInvoke(() => Apply(Choice));
        };
        Apply(choice);
    }

    public static void Apply(ThemeChoice choice, ThemeMode? forced = null)
    {
        Choice = choice;
        Mode = forced ?? ThemeResolver.Resolve(choice, SystemUsesLightTheme());
        var tokens = DesignTokens.Build(BaseJson, ProductJson, Mode);
        var resources = Application.Current.Resources;
        foreach (var (role, _) in tokens.Colors)
        {
            var c = tokens.Color(role);
            var brush = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
            brush.Freeze();
            resources[role] = brush;
        }
        foreach (var (name, px) in tokens.Radius)
            resources[$"radius-{name}"] = new CornerRadius(px);
        resources["font-body"] = new FontFamily(tokens.FontFamily);
        resources["font-display"] = new FontFamily(tokens.FontDisplay);
        resources["font-mono"] = new FontFamily(tokens.FontMono);
        foreach (Window w in Application.Current.Windows)
            ApplyTitleBar(w);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Khoa khong co (Windows cu, chinh sach chan) => coi la Sang - gia tri mac dinh cua Windows.</summary>
    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch (System.Security.SecurityException)
        {
            return true;
        }
    }

    /// <summary>Thanh tieu de toi/sang theo giao dien (DWMWA_USE_IMMERSIVE_DARK_MODE = 20, Windows 10 20H1+).</summary>
    public static void ApplyTitleBar(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;
        var dark = Mode == ThemeMode.Dark ? 1 : 0;
        _ = NativeMethods.DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }

    private static string ReadEmbedded(string name)
    {
        using var stream = typeof(ThemeManager).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Thiếu tài nguyên nhúng {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static class NativeMethods
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
