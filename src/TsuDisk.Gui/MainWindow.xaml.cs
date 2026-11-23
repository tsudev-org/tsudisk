using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace TsuDisk.Gui;

internal sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        FooterVersion.Text = $"TsuDisk {App.Version}";
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        // Mau khoi ban do lay qua bo chuyen doi => doi giao dien thi ve lai danh sach.
        ThemeManager.ThemeChanged += (_, _) => Map.Items.Refresh();
        // DATA_TABLE muc 7: thong bao "Dang hien thi a den b trong n muc" sau khi doi moc/trang (aria-live polite).
        model.Pager.Changed += (_, _) => Dispatcher.BeginInvoke(() =>
            UIElementAutomationPeer.FromElement(Summary)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));
        // Xac nhan lan hai truoc khi ghi dia (ADR-0009): hop thoai go lai chu cai o.
        model.ConfirmApply = plan => new ConfirmWindow(plan) { Owner = this }.ShowDialog() == true;
        // Dang ghi dia thi khong dong cua so giua chung (nhat ky van ghi sau moi buoc, nhung dung de nguoi dung tu cat).
        Closing += (_, e) =>
        {
            if (!Model.IsBusy)
                return;
            e.Cancel = true;
            MessageBox.Show(this, Loc.Instance["BusyClosing"], "TsuDisk", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
    }

    internal MainViewModel Model => (MainViewModel)DataContext;

    /// <summary>
    /// Anh tron trang cho che do chup: header + TOAN BO noi dung cuon (khong bi chieu cao man hinh cat) + chan trang,
    /// ghep bang VisualBrush tren nen bg-base.
    /// </summary>
    internal RenderTargetBitmap RenderFullPage()
    {
        UpdateLayout();
        var header = (FrameworkElement)Root.Children[0];
        var footer = (FrameworkElement)Root.Children[1];
        var body = (FrameworkElement)Scroller.Content;
        var width = Root.ActualWidth;
        var parts = new[] { header, body, footer };
        var height = parts.Sum(p => p.ActualHeight + p.Margin.Top + p.Margin.Bottom);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle((Brush)FindResource("bg-base"), null, new Rect(0, 0, width, height));
            var y = 0.0;
            foreach (var part in parts)
            {
                var w = part.ActualWidth;
                var x = (width - w) / 2;
                y += part.Margin.Top;
                dc.DrawRectangle(new VisualBrush(part)
                {
                    Stretch = Stretch.None,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(VisualTreeHelper.GetOffset(part).X, VisualTreeHelper.GetOffset(part).Y, w, part.ActualHeight),
                }, null, new Rect(x, y, w, part.ActualHeight));
                y += part.ActualHeight + part.Margin.Bottom;
            }
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private void OnAbout(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    internal static void OpenLink(RequestNavigateEventArgs e)
    {
        // Chi mo trang https co dinh cua TsuDEV bang trinh duyet mac dinh; app khong tu goi mang.
        if (e.Uri.Scheme == Uri.UriSchemeHttps)
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        e.Handled = true;
    }

    private void OnLink(object sender, RequestNavigateEventArgs e) => OpenLink(e);
}
