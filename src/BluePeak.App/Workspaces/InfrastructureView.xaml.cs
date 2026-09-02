using System.Windows;
using System.Windows.Controls;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public sealed record AnswerRow(string Question, string Answer, HealthState Tone);
public sealed record RequirementRow(ServiceNode Node, string Protocol);

public partial class InfrastructureView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;

    public InfrastructureView()
    {
        InitializeComponent();
        Map.Model = _model;
        Map.NodeActivated += id => Select(id, carryFocus: true);
        UpdateSummary();
        Select("svc-dns", carryFocus: false);
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Service) return;
        if (_model.Node(subject.Id) is not null) { Select(subject.Id, carryFocus: false); return; }

        // Overview hands over a layer name rather than a node; select its worst element.
        if (Enum.TryParse<EstateLayer>(subject.Id, out var layer))
        {
            var worst = _model.ByLayer(layer).OrderByDescending(n => n.Health.Weight()).FirstOrDefault();
            if (worst is not null) Select(worst.Id, carryFocus: false);
        }
    }

    private void UpdateSummary()
    {
        int bad = _model.Nodes.Count(n => n.Health.IsBad());
        Summary.Text = $"{_model.Nodes.Count} elements · {_model.Edges.Count} modelled dependencies · {bad} not healthy";
    }

    private void Select(string id, bool carryFocus)
    {
        var node = _model.Node(id);
        if (node is null) return;
        Map.SelectedId = id;

        DetailPip.State = node.Health;
        DetailState.Text = node.Health.Label();
        DetailState.Foreground = Design.Theme.ForHealth(node.Health);
        DetailLayer.Text = node.LayerLabel + " · tier " + node.Tier;
        DetailName.Text = node.Name;
        DetailReason.Text = node.StateReason;

        var requires = _model.DependenciesOf(node.Id)
            .Select(edge => new RequirementRow(_model.Node(edge.ToId)!, edge.Protocol))
            .Where(r => r.Node is not null)
            .ToList();
        var breaks = _model.BlastRadius(node.Id);
        var closure = _model.DependencyClosure(node.Id);

        RequiresList.ItemsSource = requires;
        BreaksList.ItemsSource = breaks;

        var criticalPath = closure.Where(n => n.Health.IsBad()).ToList();
        AnswerList.ItemsSource = new List<AnswerRow>
        {
            new("What depends on this?",
                breaks.Count == 0
                    ? "Nothing in the modelled estate. A failure here is contained to this element."
                    : $"{breaks.Count} elements across {breaks.Select(b => b.Layer).Distinct().Count()} layers, "
                      + $"including {string.Join(", ", breaks.Take(3).Select(b => b.Name))}"
                      + (breaks.Count > 3 ? $" and {breaks.Count - 3} more." : "."),
                breaks.Count > 6 ? HealthState.Critical : breaks.Count > 0 ? HealthState.Degraded : HealthState.Healthy),

            new("What breaks if it fails?",
                breaks.Count == 0
                    ? "No downstream impact."
                    : $"{breaks.Count(b => b.Tier == 1)} tier-1 services would be impaired, "
                      + $"{breaks.Count(b => b.Layer == EstateLayer.Applications)} of them business applications.",
                breaks.Any(b => b.Tier == 1) ? HealthState.Critical : HealthState.Degraded),

            new("What is it degraded by?",
                criticalPath.Count == 0
                    ? "Nothing it depends on is unhealthy. Any fault here originates locally."
                    : $"{criticalPath.Count} of its {closure.Count} transitive requirements are unhealthy: "
                      + string.Join(", ", criticalPath.Take(3).Select(c => c.Name)) + ".",
                criticalPath.Count > 0 ? HealthState.Critical : HealthState.Healthy),

            new("How deep is it?",
                $"{closure.Count} transitive requirements down to the foundation; "
                + $"{_model.DependenciesOf(node.Id).Count} direct.",
                HealthState.Unknown)
        };

        AttributeList.ItemsSource = new List<FieldRow>
        {
            new("Identifier", node.Id),
            new("Kind", node.Kind),
            new("Owner", node.Owner),
            new("Location", node.Location),
            new("Tier", node.Tier.ToString()),
            new("Availability", $"{node.Availability:0.00} %"),
            new("Open signals", node.OpenSignals.ToString()),
            new("Tags", node.Tags.Count == 0 ? "none" : string.Join(", ", node.Tags))
        };

        BuildActions(node);
        if (carryFocus) FocusService.Current.Set(FocusKind.Service, node.Id, node.Name, node.StateReason);
    }

    private void BuildActions(ServiceNode node)
    {
        ActionPanel.Children.Clear();

        void Add(string caption, Action handler)
        {
            var button = new Button
            {
                Content = caption,
                Style = (Style)FindResource("Button.Standard"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8)
            };
            button.Click += (_, _) => handler();
            ActionPanel.Children.Add(button);
        }

        Add("Inspect in NOC", () => Navigator.Current.NavigateWithSubject(
            "noc", FocusKind.Service, node.Id, node.Name, node.StateReason));

        var change = _model.Changes.FirstOrDefault(c => c.TargetServiceId == node.Id);
        if (change is not null)
            Add($"Change {change.Id} targets this", () => Navigator.Current.NavigateWithSubject(
                "changes", FocusKind.Change, change.Id, change.Title, "Opened from Infrastructure"));
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Map.SelectedId = null;
        Map.InvalidateVisual();
    }
}
