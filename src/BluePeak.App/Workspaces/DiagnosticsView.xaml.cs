using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public sealed class HopRow
{
    public required DiagnosticHop Hop { get; init; }
    public required string ServiceName { get; init; }

    public string IndexLabel => $"{Hop.Index:00}";
    public string Label => Hop.Label;
    public string Operation => $"{Hop.Protocol} · {Hop.Operation}";
    public string Expected => Hop.Expected;
    public string Actual => Hop.Actual;
    public string Reasoning => Hop.Reasoning;
    public HealthState Result => Hop.Result;
    public bool IsFirstFailure => Hop.IsFirstFailure;
    public bool IsConsequence => Hop.IsDownstreamConsequence;
    public bool ShowBand => Hop.IsFirstFailure || Hop.IsDownstreamConsequence;
    public string? EvidenceId => Hop.EvidenceId;
    public string EvidenceCaption => Hop.EvidenceId is null ? "" : $"Evidence {Hop.EvidenceId}";
    public string Elapsed => Hop.ElapsedMs <= 0 ? "not reached" : $"{Hop.ElapsedMs:0} ms";

    public Brush ActualBrush => Hop.Result.IsBad() ? Theme.ForHealth(Hop.Result) : Theme.Brush("B.TextPrimary");

    public Brush RowBackground => Hop.IsFirstFailure
        ? Theme.Frozen(Theme.WithAlpha(Theme.Critical.Color, 0.07))
        : Brushes.Transparent;
}

public partial class DiagnosticsView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly List<ToggleButton> _pathButtons = new();
    private DiagnosticPath _path;

    public DiagnosticsView()
    {
        InitializeComponent();
        _path = _model.DiagnosticPaths[0];
        BuildPathSelector();
        Show(_path);
    }

    public void ApplyFocus(FocusSubject subject)
    {
        var byId = _model.DiagnosticPaths.FirstOrDefault(p => p.Id == subject.Id);
        if (byId is not null) { Show(byId); return; }

        // A service subject selects the walk that reaches it.
        var byService = _model.DiagnosticPaths.FirstOrDefault(p =>
            p.FirstFailureServiceId == subject.Id || p.Hops.Any(h => h.ServiceId == subject.Id));
        if (byService is not null) Show(byService);
    }

    private void BuildPathSelector()
    {
        foreach (var path in _model.DiagnosticPaths)
        {
            var button = new ToggleButton
            {
                Content = path.Name,
                Style = (Style)FindResource("Toggle.Segment"),
                Tag = path,
                IsChecked = ReferenceEquals(path, _path)
            };
            button.Click += Path_Click;
            _pathButtons.Add(button);
            PathGroup.Children.Add(button);
        }
    }

    private void Path_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: DiagnosticPath path }) return;
        Show(path);
    }

    private void Show(DiagnosticPath path)
    {
        _path = path;
        foreach (var button in _pathButtons)
            button.IsChecked = ReferenceEquals(button.Tag, path);

        RequestText.Text = path.Request;
        RunMeta.Text = $"{path.Id} · {path.Origin} · ran {AgoConverter.Format(_model.Now - path.RunAt)} ago";

        HopList.ItemsSource = path.Hops
            .Select(hop => new HopRow { Hop = hop, ServiceName = _model.NameOf(hop.ServiceId) })
            .ToList();

        ConclusionText.Text = path.Conclusion;
        var firstFailure = _model.Node(path.FirstFailureServiceId);
        ConclusionBorder.BorderBrush = Theme.ForHealth(firstFailure?.Health ?? HealthState.Unknown);

        FirstFailurePip.State = firstFailure?.Health ?? HealthState.Unknown;
        FirstFailureName.Text = firstFailure?.Name ?? "Not established";
        FirstFailureReason.Text = firstFailure?.StateReason ?? "";
        OpenServiceButton.Visibility = firstFailure is null ? Visibility.Collapsed : Visibility.Visible;

        BlastList.ItemsSource = path.BlastRadiusServiceIds
            .Select(id => _model.Node(id))
            .Where(n => n is not null)
            .ToList();

        var evidence = path.Hops.Select(h => h.EvidenceId)
            .Where(id => id is not null)
            .Distinct()
            .Select(id => _model.EvidenceRecord(id))
            .Where(e => e is not null)
            .ToList();
        EvidenceList.ItemsSource = evidence;

        BuildActions(path);
    }

    private void BuildActions(DiagnosticPath path)
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

        if (path.LinkedIncidentId is not null && _model.Incident(path.LinkedIncidentId) is { } incident)
            Add($"Open {incident.Id}", () => Navigator.Current.NavigateWithSubject(
                "incidents", FocusKind.Incident, incident.Id, incident.Title, "Opened from Diagnostics"));

        var runbook = _model.Runbooks.FirstOrDefault(r =>
            path.FirstFailureServiceId is not null && r.TargetServiceIds.Contains(path.FirstFailureServiceId));
        if (runbook is not null)
            Add($"Stage {runbook.Id} — {runbook.Name}", () => Navigator.Current.NavigateWithSubject(
                "automation", FocusKind.Runbook, runbook.Id, runbook.Name, "Staged from Diagnostics"));

        if (path.JourneyId is not null)
            Add("Replay in simulator", () => Navigator.Current.NavigateWithSubject(
                "simulator", FocusKind.Journey, path.JourneyId, path.Name, "Opened from Diagnostics"));
    }

    private void Evidence_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } || string.IsNullOrEmpty(id)) return;
        var record = _model.EvidenceRecord(id);
        if (record is null) return;
        Navigator.Current.NavigateWithSubject("evidence", FocusKind.Evidence, record.Id, record.Claim,
            "Opened from Diagnostics");
    }

    private void OpenService_Click(object sender, RoutedEventArgs e)
    {
        var node = _model.Node(_path.FirstFailureServiceId);
        if (node is null) return;
        Navigator.Current.NavigateWithSubject("noc", FocusKind.Service, node.Id, node.Name, node.StateReason);
    }

    private void Simulate_Click(object sender, RoutedEventArgs e)
    {
        if (_path.JourneyId is null) return;
        Navigator.Current.NavigateWithSubject("simulator", FocusKind.Journey, _path.JourneyId, _path.Name,
            "Opened from Diagnostics");
    }
}
