using System.Windows;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Gui;

internal sealed partial class ConfirmWindow : Window
{
    private readonly char _letter;

    public ConfirmWindow(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        InitializeComponent();
        _letter = plan.TargetLetter;
        var l = Loc.Instance;
        Headline.Text = PlanText.Headline(plan);
        Steps.ItemsSource = plan.Steps.Select(s => $"{s.Index}. {(s.ChangesDisk ? $"[{l["PlanWrite"]}] " : "")}{s.Title}").ToList();
        PromptLabel.Content = l.Format("ConfirmPrompt", _letter);
        ApplyButton.Content = l.Format("ConfirmApply", _letter);
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Loaded += (_, _) => Typed.Focus();
    }

    private void OnTyped(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyButton.IsEnabled = RenameForm.ConfirmationMatches(Typed.Text, _letter);
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (!RenameForm.ConfirmationMatches(Typed.Text, _letter))
            return;
        DialogResult = true;
    }
}
