using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public partial class OverviewView : UserControl
{
    private readonly OverviewViewModel _vm = new();

    public OverviewView()
    {
        InitializeComponent();

        AttentionList.ItemsSource = _vm.Attention;
        ActivityList.ItemsSource = _vm.Activity;
        LayerList.ItemsSource = _vm.Layers;
        DegradedList.ItemsSource = _vm.Degraded;
        RiskList.ItemsSource = _vm.Risks;

        VerdictText.Text = _vm.Verdict;
        VerdictText.Foreground = Theme.ForHealth(_vm.VerdictTone);
        VerdictPip.State = _vm.VerdictTone;
        VerdictDetail.Text = _vm.VerdictDetail;
        AsOfText.Text = "As of " + _vm.AsOf;
        AttentionCount.Text = $"{_vm.Attention.Count} items, ranked by consequence";
        DegradedCount.Text = $"{_vm.Degraded.Count} of {EstateService.Current.Model.Nodes.Count}";
    }

    private void Attention_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AttentionList.SelectedItem is not AttentionItem item || item.FocusKind == FocusKind.None) return;
        FocusService.Current.Set(item.FocusKind, item.Id, item.Title, item.Why);
    }

    private void Attention_Open(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void Attention_Key(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    private void OpenSelected()
    {
        if (AttentionList.SelectedItem is not AttentionItem item) return;
        if (item.FocusKind == FocusKind.None) Navigator.Current.Navigate(item.Workspace);
        else Navigator.Current.NavigateWithSubject(item.Workspace, item.FocusKind, item.Id, item.Title, item.Why);
    }

    private void Degraded_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DegradedList.SelectedItem is not ServiceNode node) return;
        Navigator.Current.NavigateWithSubject("noc", FocusKind.Service, node.Id, node.Name, node.StateReason);
    }

    private void Layer_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: EstateLayer layer }) return;
        Navigator.Current.NavigateWithSubject("infrastructure", FocusKind.Service, layer.ToString(),
            OverviewViewModel.LayerName(layer), "Layer selected from the operations board");
    }

    private void OpenSimulator_Click(object sender, RoutedEventArgs e) =>
        Navigator.Current.Navigate("simulator");
}
