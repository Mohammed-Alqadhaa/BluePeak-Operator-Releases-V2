using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public sealed record Milestone(string Label, string Value, string Note, HealthState Tone);

public sealed class AffectedRow
{
    public required ServiceNode Node { get; init; }
    public required bool IsCause { get; init; }
    public string RoleLabel => IsCause ? "CAUSE" : Node.Health.IsBad() ? "IMPACTED" : "IN SCOPE";
    public Brush RoleBrush => IsCause ? Theme.Critical
        : Node.Health.IsBad() ? Theme.Degraded : Theme.Brush("B.TextTertiary");
}

public sealed record LinkedRow(string Id, string Title, string Workspace, FocusKind Kind);

public partial class IncidentsView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly List<ToggleButton> _filters = new();
    private string _filter = "open";
    private Incident? _incident;

    public IncidentsView()
    {
        InitializeComponent();
        BuildFilters();
        Refresh();
        IncidentList.SelectedIndex = 0;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Incident) return;
        var incident = _model.Incident(subject.Id);
        if (incident is null) return;
        if (IncidentList.ItemsSource is IEnumerable<Incident> items && !items.Contains(incident))
        {
            _filter = "all";
            foreach (var button in _filters) button.IsChecked = (string)button.Tag! == "all";
            Refresh();
        }
        IncidentList.SelectedItem = incident;
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in new[] { ("open", "Open"), ("major", "Major"), ("all", "All") })
        {
            var button = new ToggleButton
            {
                Content = label,
                Style = (Style)FindResource("Toggle.Segment"),
                Tag = key,
                IsChecked = key == _filter
            };
            button.Click += (s, _) =>
            {
                _filter = key;
                foreach (var b in _filters) b.IsChecked = ReferenceEquals(b, s);
                Refresh();
            };
            _filters.Add(button);
            FilterGroup.Children.Add(button);
        }
    }

    private void Refresh()
    {
        IEnumerable<Incident> query = _filter switch
        {
            "open" => _model.Incidents.Where(i => i.State != IncidentState.Resolved),
            "major" => _model.Incidents.Where(i => i.Severity >= Severity.High),
            _ => _model.Incidents
        };

        var items = query.OrderByDescending(i => i.Severity).ThenBy(i => i.StartedAt).ToList();
        var selected = IncidentList.SelectedItem as Incident;
        IncidentList.ItemsSource = items;
        if (selected is not null && items.Contains(selected)) IncidentList.SelectedItem = selected;
        else if (items.Count > 0) IncidentList.SelectedIndex = 0;

        int users = items.Sum(i => i.UsersAffected);
        Summary.Text = $"{items.Count} incidents · {users:N0} users affected";
        BuildProfile(items);
    }

    private void BuildProfile(List<Incident> items)
    {
        var open = _model.Incidents.Where(i => i.State != IncidentState.Resolved).ToList();
        var resolved = _model.Incidents.Where(i => i.State == IncidentState.Resolved).ToList();
        var oldest = open.OrderBy(i => i.StartedAt).FirstOrDefault();

        var rows = new List<PostureLine>
        {
            new("Open", open.Count.ToString(), open.Count > 2 ? HealthState.Degraded : HealthState.Healthy),
            new("Critical severity", open.Count(i => i.Severity >= Severity.Critical).ToString(),
                open.Any(i => i.Severity >= Severity.Critical) ? HealthState.Critical : HealthState.Healthy),
            new("Unassigned command", open.Count(i => i.Commander == "Unassigned").ToString(), HealthState.Healthy),
            new("Longest running", oldest is null ? "—" : AgoConverter.Format(_model.Now - oldest.StartedAt),
                HealthState.Degraded),
            new("Resolved, last 7 days", resolved.Count.ToString(), HealthState.Healthy)
        };

        foreach (var workstream in _model.Incidents.Select(i => i.Workstream).Distinct().OrderBy(w => w))
        {
            int count = open.Count(i => i.Workstream == workstream);
            if (count > 0) rows.Add(new PostureLine(workstream, count.ToString(), HealthState.Unknown));
        }

        ProfileList.ItemsSource = rows;
    }

    private void Incident_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IncidentList.SelectedItem is Incident incident) Show(incident);
    }

    private void Show(Incident incident)
    {
        _incident = incident;
        DetailId.Text = incident.Id;
        DetailTitle.Text = incident.Title;
        DetailImpact.Text = incident.Impact;
        DetailState.Text = "state " + incident.State;
        DetailCommander.Text = "commander " + incident.Commander;

        var severity = Theme.ForSeverity(incident.Severity);
        DetailSeverityText.Text = incident.Severity.ToString().ToUpperInvariant();
        DetailSeverityText.Foreground = severity;
        DetailSeverityChip.Background = Theme.Frozen(Theme.WithAlpha(severity.Color, 0.14));
        DetailSeverityChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(severity.Color, 0.4));

        var detect = incident.DetectedAt is null ? TimeSpan.Zero : incident.DetectedAt.Value - incident.StartedAt;
        MilestoneList.ItemsSource = new List<Milestone>
        {
            new("Time to detect", incident.DetectedAt is null ? "—" : AgoConverter.Format(detect),
                incident.DetectedAt is null ? "not recorded" : "signal after onset",
                detect.TotalMinutes > 10 ? HealthState.Degraded : HealthState.Healthy),
            new("Elapsed", AgoConverter.Format(_model.Now - incident.StartedAt), "since onset",
                incident.State == IncidentState.Resolved ? HealthState.Healthy : HealthState.Degraded),
            new("Users affected", incident.UsersAffected == 0 ? "none" : incident.UsersAffected.ToString("N0"),
                incident.Workstream, incident.UsersAffected > 500 ? HealthState.Critical
                    : incident.UsersAffected > 0 ? HealthState.Degraded : HealthState.Healthy),
            new("Services in scope", incident.AffectedServiceIds.Count.ToString(),
                $"{incident.AffectedServiceIds.Count(id => _model.Node(id)?.Health.IsBad() == true)} not healthy",
                HealthState.Unknown)
        };

        var affected = incident.AffectedServiceIds
            .Select(id => _model.Node(id))
            .Where(n => n is not null)
            .Select(n => new AffectedRow { Node = n!, IsCause = n!.Id == incident.RootCauseServiceId })
            .OrderByDescending(r => r.IsCause)
            .ThenByDescending(r => r.Node.Health.Weight())
            .ToList();
        AffectedList.ItemsSource = affected;
        AffectedSummary.Text = $"{affected.Count} services · {affected.Count(a => a.Node.Health.IsBad())} not healthy";

        TimelineList.ItemsSource = incident.Timeline.OrderByDescending(t => t.At).ToList();

        var cause = _model.Node(incident.RootCauseServiceId);
        CauseBorder.BorderBrush = Theme.ForHealth(cause?.Health ?? HealthState.Unknown);
        CauseName.Text = cause?.Name ?? "Not yet established";
        CauseReason.Text = cause?.StateReason ?? "Investigation has not isolated a first failing component.";
        CauseLink.Visibility = cause is null ? Visibility.Collapsed : Visibility.Visible;

        var linked = new List<LinkedRow>();
        foreach (var id in incident.LinkedTicketIds)
            if (_model.Ticket(id) is { } ticket) linked.Add(new LinkedRow(ticket.Id, ticket.Subject, "tickets", FocusKind.Ticket));
        if (incident.SuspectedChangeId is not null && _model.Change(incident.SuspectedChangeId) is { } suspect)
            linked.Add(new LinkedRow(suspect.Id, "Suspected cause · " + suspect.Title, "changes", FocusKind.Change));
        foreach (var change in _model.Changes.Where(c => c.LinkedIncidentId == incident.Id && c.Id != incident.SuspectedChangeId))
            linked.Add(new LinkedRow(change.Id, "Correction · " + change.Title, "changes", FocusKind.Change));
        LinkedList.ItemsSource = linked;

        EvidenceList.ItemsSource = incident.EvidenceIds
            .Select(id => _model.EvidenceRecord(id))
            .Where(r => r is not null)
            .ToList();

        BuildActions(incident);
        FocusService.Current.Set(FocusKind.Incident, incident.Id, incident.Title, incident.Impact);
    }

    private void BuildActions(Incident incident)
    {
        ActionPanel.Children.Clear();

        void Add(string caption, Action handler, bool primary = false)
        {
            var button = new Button
            {
                Content = caption,
                Style = (Style)FindResource(primary ? "Button.Primary" : "Button.Standard"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8)
            };
            button.Click += (_, _) => handler();
            ActionPanel.Children.Add(button);
        }

        var correction = _model.Changes.FirstOrDefault(c =>
            c.LinkedIncidentId == incident.Id && c.State is ChangeState.AwaitingApproval or ChangeState.Approved);
        if (correction is not null)
            Add($"Review {correction.Id} for approval", () => Navigator.Current.NavigateWithSubject(
                "changes", FocusKind.Change, correction.Id, correction.Title, "Opened from " + incident.Id), true);

        var runbook = _model.Runbooks.FirstOrDefault(r => r.SuggestedForIncidentId == incident.Id);
        if (runbook is not null)
            Add($"Stage {runbook.Id}", () => Navigator.Current.NavigateWithSubject(
                "automation", FocusKind.Runbook, runbook.Id, runbook.Name, "Staged from " + incident.Id));

        var path = _model.DiagnosticPaths.FirstOrDefault(p => p.LinkedIncidentId == incident.Id);
        if (path is not null)
            Add("Open dependency walk", () => Navigator.Current.NavigateWithSubject(
                "diagnostics", FocusKind.Service, path.Id, path.Name, "Opened from " + incident.Id));
        if (path?.JourneyId is not null)
            Add("Replay in simulator", () => Navigator.Current.NavigateWithSubject(
                "simulator", FocusKind.Journey, path.JourneyId, path.Name, "Opened from " + incident.Id));
    }

    private void Walk_Click(object sender, RoutedEventArgs e)
    {
        if (_incident is null) return;
        var path = _model.DiagnosticPaths.FirstOrDefault(p => p.LinkedIncidentId == _incident.Id);
        if (path is null) return;
        Navigator.Current.NavigateWithSubject("diagnostics", FocusKind.Service, path.Id, path.Name,
            "Opened from " + _incident.Id);
    }

    private void Service_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id }) return;
        var node = _model.Node(id);
        if (node is null) return;
        Navigator.Current.NavigateWithSubject("noc", FocusKind.Service, node.Id, node.Name, node.StateReason);
    }

    private void Linked_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LinkedRow row }) return;
        Navigator.Current.NavigateWithSubject(row.Workspace, row.Kind, row.Id, row.Title, "Opened from Incidents");
    }

    private void Evidence_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        var record = _model.EvidenceRecord(id);
        if (record is null) return;
        Navigator.Current.NavigateWithSubject("evidence", FocusKind.Evidence, record.Id, record.Claim,
            "Opened from Incidents");
    }
}
