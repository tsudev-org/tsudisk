using System.Windows;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Gui;

internal sealed partial class ConfirmWindow : Window
{
    private readonly Func<string?, bool> _matches;

    public ConfirmWindow(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        InitializeComponent();
        var l = Loc.Instance;
        // Doi ten / mo rong: go lai chu cai o dang bi dong vao (ADR-0009, ADR-0011). Tao phan vung: go lai SO DIA se bi ghi
        // (chu cai moi chua ton tai) - ADR-0010.
        var create = plan.Operation == CreatePartitionPlanner.Operation;
        var extend = plan.Operation == ExtendPlanner.Operation;
        var letter = plan.TargetLetter;
        var disk = plan.DiskNumber;
        _matches = create ? typed => CreateForm.ConfirmationMatches(typed, disk)
            : extend ? typed => ExtendForm.ConfirmationMatches(typed, letter)
            : typed => RenameForm.ConfirmationMatches(typed, letter);
        Headline.Text = PlanText.Headline(plan);
        Steps.ItemsSource = plan.Steps.Select(s => $"{s.Index}. {(s.ChangesDisk ? $"[{l["PlanWrite"]}] " : "")}{s.Title}").ToList();
        Warning.Text = l[create ? "ConfirmWarningCreate"
            : extend ? ExtendForm.RelocatesRecovery(plan) ? "ConfirmWarningExtendRecovery" : "ConfirmWarningExtend"
            : "ConfirmWarning"];
        PromptLabel.Content = create ? l.Format("ConfirmPromptDisk", disk) : l.Format("ConfirmPrompt", letter);
        ApplyButton.Content = create ? l.Format("ConfirmApplyCreate", disk)
            : extend ? l.Format("ConfirmApplyExtend", letter)
            : l.Format("ConfirmApply", letter);
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        Loaded += (_, _) => Typed.Focus();
    }

    private void OnTyped(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyButton.IsEnabled = _matches(Typed.Text);
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (!_matches(Typed.Text))
            return;
        DialogResult = true;
    }
}
