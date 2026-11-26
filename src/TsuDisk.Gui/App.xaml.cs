using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TsuDisk.Core.Integrity;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Gui;

/// <summary>
/// Diem vao giao dien. Tham so:
/// <c>--du-lieu-mau</c> dung bo cuc mau thay o that;
/// <c>--chup-man-hinh &lt;thu-muc&gt;</c> ve cua so o vi/en x Sang/Toi ra PNG roi thoat (ma 0 = ve duoc het) - dung cho
/// anh website/PR va kiem tren CI Windows. Che do chup KHONG doc/ghi tep cai dat.
/// Cho E2E lai giao dien tren o ao (tests/e2e/run-e2e-gui.ps1): <c>--chi-o-ao</c> chi liet ke VA chi ghi o VHD/VHDX,
/// <c>--dia N</c> chon san dia N, <c>--thu-muc-nhat-ky &lt;thu-muc&gt;</c> noi ghi nhat ky lan chay.
/// <c>--du-lieu-mau --xem-khoa-mau</c> mo san modal khoa voi phien ban MAU (chi hien thi, ca E2E G11); co nay KHONG bo
/// qua cong - cong phien ban (ADR-0012) chay o moi lan mo that, khong co co/bien moi truong nao tat duoc.
/// </summary>
internal sealed partial class App : Application
{
    public static string Version =>
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0";

    /// <summary>Ngon ngu giao dien cua Windows (GetUserDefaultUILanguage qua CurrentUICulture), uu tien truoc.</summary>
    public static IEnumerable<string> SystemLanguages() => [CultureInfo.CurrentUICulture.Name, CultureInfo.CurrentCulture.Name];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args;
        var sample = args.Contains("--du-lieu-mau");
        var shotIndex = Array.IndexOf(args, "--chup-man-hinh");
        var shotDir = shotIndex >= 0 && shotIndex + 1 < args.Length ? args[shotIndex + 1] : null;
        SettingsStore.Disabled = shotDir is not null;
        if (shotDir is not null)
            Directory.CreateDirectory(shotDir);

        var settings = SettingsStore.Load();
        Loc.Instance.SetLanguage(UiLanguage.Resolve(settings.Language, SystemLanguages()));
        ThemeManager.Start(settings.Theme);
        string? Value(string name) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        IDiskSource source = sample ? new SampleDiskSource() : new WindowsDiskSource(args.Contains("--chi-o-ao"));
        // Duong ghi (ADR-0009) chi co khi chay o that, khong o che do du lieu mau/chup man hinh.
        IPlanExecutor? executor = sample || shotDir is not null ? null : new WindowsPlanExecutor(Value("--thu-muc-nhat-ky"));
        var model = new MainViewModel(source, settings, executor)
        {
            InitialDisk = int.TryParse(Value("--dia"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null,
        };
        var window = new MainWindow(model);
        MainWindow = window;

        if (shotDir is null)
        {
            window.Show();
            if (sample && args.Contains("--xem-khoa-mau"))
            {
                await model.LoadAsync().ConfigureAwait(true);
                GuiGate.Show(window, GuiGate.SampleDecision(), UpdateAction.For("github", UpdateAction.ReleasesPage), Value("--thu-muc-nhat-ky"));
                return;
            }
            new GuiGate(window, model, Value("--thu-muc-nhat-ky")).Start();
            await model.LoadAsync().ConfigureAwait(true);
            return;
        }

        try
        {
            await CaptureAsync(window, model, shotDir).ConfigureAwait(true);
            Shutdown(0);
        }
#pragma warning disable CA1031 // Che do chup: moi loi => in ra va ma thoat 1 de CI do.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // WinExe khong co console: ghi loi ra tep de CI in ra.
            await File.WriteAllTextAsync(Path.Combine(shotDir, "loi.txt"), ex.ToString()).ConfigureAwait(true);
            Shutdown(1);
        }
    }

    private static async Task CaptureAsync(MainWindow window, MainViewModel model, string directory)
    {
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -32000;
        window.Top = -32000;
        window.Width = 1280;
        window.Height = 900;
        window.Show();
        await model.LoadAsync().ConfigureAwait(true);
        if (model.Error is { } error)
            throw new InvalidOperationException($"Không đọc được bố cục đĩa: {error}");
        if (model.PlanExtend.CanExecute(null))
            model.PlanExtend.Execute(null);

        foreach (var language in UiLanguage.Supported)
        {
            Loc.Instance.SetLanguage(language);
            foreach (var mode in new[] { Core.Presentation.ThemeMode.Light, Core.Presentation.ThemeMode.Dark })
            {
                ThemeManager.Apply(ThemeChoice.System, mode);
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var bitmap = window.RenderFullPage();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var path = Path.Combine(directory, $"tsudisk-gui_{language}_{(mode == Core.Presentation.ThemeMode.Dark ? "toi" : "sang")}.png");
                using var file = File.Create(path);
                encoder.Save(file);
                await Console.Out.WriteLineAsync(path).ConfigureAwait(true);
            }
        }
        await CaptureLockAsync(window, directory).ConfigureAwait(true);
    }

    /// <summary>Modal khoa vi/en x Sang/Toi voi quyet dinh MAU (khong di qua cong, khong mang) - ADR-0012 muc 10.</summary>
    private static async Task CaptureLockAsync(Window owner, string directory)
    {
        var modal = new UpdateRequiredWindow(GuiGate.SampleDecision(), UpdateAction.For("github", UpdateAction.ReleasesPage), null)
        {
            Owner = owner,
            ShowInTaskbar = false,
        };
        modal.PrepareForCapture(1280, 800);
        modal.Show();
        try
        {
            foreach (var language in UiLanguage.Supported)
            {
                Loc.Instance.SetLanguage(language);
                foreach (var mode in new[] { Core.Presentation.ThemeMode.Light, Core.Presentation.ThemeMode.Dark })
                {
                    ThemeManager.Apply(ThemeChoice.System, mode);
                    await modal.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    modal.UpdateLayout();
                    var root = (FrameworkElement)modal.Content;
                    var dpi = VisualTreeHelper.GetDpi(modal);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX),
                        (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                    bitmap.Render(root);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var path = Path.Combine(directory, $"tsudisk-gui-khoa_{language}_{(mode == Core.Presentation.ThemeMode.Dark ? "toi" : "sang")}.png");
                    using var file = File.Create(path);
                    encoder.Save(file);
                    await Console.Out.WriteLineAsync(path).ConfigureAwait(true);
                }
            }
        }
        finally
        {
            UpdateRequiredWindow.AllowClose = true;
            modal.Close();
            UpdateRequiredWindow.AllowClose = false;
        }
    }
}
