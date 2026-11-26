using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Microsoft.Win32;
using TsuDisk.Core.Integrity;

namespace TsuDisk.Gui;

/// <summary>
/// Modal khoa (ADR-0012 muc 10). Chi hai nut: <b>Cap nhat ngay</b> (GitHub: mo <c>url</c>; Scoop/winget: hien + sao chep
/// lenh) va <b>Xuat du lieu</b> (<see cref="DataExport"/>, dung chung voi lenh <c>xuat-du-lieu</c>).
///
/// Khong dong duoc: khong co thanh tieu de/nut X; Esc va Alt+F4 bi nuot; <see cref="Window.Closing"/> bi huy tru khi ung
/// dung dang tat (<see cref="AllowClose"/>). Mo bang <see cref="Window.ShowDialog"/> co <c>Owner</c> ⇒ cua so chinh bi
/// vo hieu, focus xoay vong trong modal (TabNavigation=Cycle).
/// </summary>
internal sealed partial class UpdateRequiredWindow : Window
{
    private readonly GateDecision _decision;
    private readonly UpdateAction _action;
    private readonly string? _logRoot;

    /// <param name="logRoot"><c>--thu-muc-nhat-ky</c> neu co; null = thu muc nhat ky mac dinh.</param>
    public UpdateRequiredWindow(GateDecision decision, UpdateAction action, string? logRoot)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(action);
        _decision = decision;
        _action = action;
        _logRoot = logRoot;
        InitializeComponent();
        ApplyText();
        ApplyHighContrast();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Loaded += (_, _) =>
        {
            Activate();
            UpdateButton.Focus();   // focus logic cua cua so: kich hoat lai cua so la focus ve nut nay
        };
        Closed += (_, _) =>
        {
            Loc.Instance.LanguageChanged -= OnLanguageChanged;
            SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        };
    }

    /// <summary>Chi dat <c>true</c> ngay truoc <see cref="Application.Shutdown()"/>.</summary>
    public static bool AllowClose { get; set; }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyText();

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
            Dispatcher.BeginInvoke(ApplyHighContrast);
    }

    private void ApplyText()
    {
        var t = UpdateRequiredText.For(Loc.Instance.Language);
        Title = t.Title;
        Heading.Text = t.Title;
        Body.Text = t.Body(_decision.Latest, _decision.Current);
        UpdateButton.Content = t.Primary;
        ExportButton.Content = t.Secondary;
        AutomationProperties.SetName(this, t.Title);
        AutomationProperties.SetName(Body, Body.Text);
    }

    /// <summary>
    /// Tuong phan cao cua Windows (ACCESSIBILITY): de mau he thong len token NGAY TAI cua so nay (DynamicResource tim
    /// tai nguyen cua so truoc tai nguyen ung dung). Tat tuong phan cao ⇒ bo de, quay ve token Sang/Toi.
    /// </summary>
    private void ApplyHighContrast()
    {
        string[] keys = ["bg-base", "bg-surface", "bg-subtle", "bg-hover", "text-primary", "text-secondary", "border-strong",
            "border-control", "primary", "primary-hover", "primary-active", "on-primary", "focus-ring"];
        if (!SystemParameters.HighContrast)
        {
            foreach (var k in keys)
                Resources.Remove(k);
            return;
        }
        Resources["bg-base"] = SystemColors.WindowBrush;
        Resources["bg-surface"] = SystemColors.WindowBrush;
        Resources["bg-subtle"] = SystemColors.WindowBrush;
        Resources["bg-hover"] = SystemColors.HighlightBrush;
        Resources["text-primary"] = SystemColors.WindowTextBrush;
        Resources["text-secondary"] = SystemColors.WindowTextBrush;
        Resources["border-strong"] = SystemColors.WindowTextBrush;
        Resources["border-control"] = SystemColors.WindowTextBrush;
        Resources["primary"] = SystemColors.HighlightBrush;
        Resources["primary-hover"] = SystemColors.HighlightBrush;
        Resources["primary-active"] = SystemColors.HighlightBrush;
        Resources["on-primary"] = SystemColors.HighlightTextBrush;
        Resources["focus-ring"] = SystemColors.WindowTextBrush;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // Esc khong dong; Alt+F4 di qua SystemKey (bi Closing huy lan nua o duoi).
        if (e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.F4))
        {
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!AllowClose)
            e.Cancel = true;
        base.OnClosing(e);
    }

    /// <summary>
    /// "Cap nhat ngay" = nhuong cho ban moi (giong In-App Update IMMEDIATE): GitHub mo trang tai; Scoop/winget sao chep lenh
    /// (hop thoai hien nguyen lenh, phong khi bo nho tam bi app khac giu). Roi TAT TsuDisk - Scoop/winget va ban tai tay
    /// deu can tep exe dang chay duoc giai phong moi thay duoc. Day la loi thoat duy nhat cua modal ngoai Task Manager.
    /// </summary>
    private void OnUpdate(object sender, RoutedEventArgs e)
    {
        var l = Loc.Instance;
        string message;
        if (_action.Command is { } command)
        {
            var copied = true;
            try { Clipboard.SetText(command); }
            catch (System.Runtime.InteropServices.COMException) { copied = false; }
            message = l.Format(copied ? "LockCommandCopied" : "LockCommandShown", command);
        }
        else
        {
            var url = _action.OpenUrl is { Scheme: "https" } u ? u : UpdateAction.ReleasesPage;
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true })?.Dispose();
            message = l.Format("LockOpened", url.Host);
        }
        MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
        AllowClose = true;
        Application.Current.Shutdown(0);
    }

    private async void OnExport(object sender, RoutedEventArgs e)
    {
        var l = Loc.Instance;
        var dialog = new OpenFolderDialog { Title = l["LockExportPick"], Multiselect = false };
        if (dialog.ShowDialog(this) != true)
            return;
        var destination = dialog.FolderName;
        ExportButton.IsEnabled = false;
        UpdateButton.IsEnabled = false;
        ShowStatus(l["LockExporting"]);
        try
        {
            var result = await Task.Run(() =>
                DataExport.Export(DataExport.StandardSources(_logRoot), destination, App.Version, DateTimeOffset.Now)).ConfigureAwait(true);
            ShowStatus(l.Format("LockExported", result.Files.Count, result.ArchivePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowStatus(l.Format("LockExportFailed", ex.Message));
        }
        finally
        {
            ExportButton.IsEnabled = true;
            UpdateButton.IsEnabled = true;
        }
    }

    private void ShowStatus(string text)
    {
        Status.Text = text;
        Status.Visibility = Visibility.Visible;
        System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(Status)?
            .RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
    }

    /// <summary>Anh cho che do chup: kich thuoc co dinh, khong toan man.</summary>
    internal void PrepareForCapture(double width, double height)
    {
        WindowState = WindowState.Normal;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
        Width = width;
        Height = height;
    }
}
