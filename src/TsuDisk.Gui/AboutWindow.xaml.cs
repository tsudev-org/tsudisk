using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace TsuDisk.Gui;

internal sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = Loc.Instance.Format("AboutVersion", App.Version);
        CopyrightText.Text = typeof(App).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
    }

    private void OnLink(object sender, RequestNavigateEventArgs e) => TsuDisk.Gui.MainWindow.OpenLink(e);
}
