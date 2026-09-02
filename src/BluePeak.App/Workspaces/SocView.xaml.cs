using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public partial class SocView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly List<ToggleButton> _filters = new();
    private string _filter = "open";
    private SecurityCase? _case;

    public SocView()
    {
        InitializeComponent();
        BuildFilters();
        Graph.EntityActivated += ShowEntity;
        RefreshQueue();
        AlertList.SelectedIndex = 0;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        switch (subject.Kind)
        {
            case FocusKind.Case:
                var securityCase = _model.Case(subject.Id);
                if (securityCase is null) return;
                ShowCase(securityCase);
                var first = _model.Alerts.FirstOrDefault(a => a.CaseId == securityCase.Id);
                if (first is not null) AlertList.SelectedItem = first;
                break;
            case FocusKind.Alert:
                var alert = _model.Alert(subject.Id);
                if (alert is not null) AlertList.SelectedItem = alert;
                break;
            case FocusKind.Entity:
                ShowEntity(subject.Id);
                break;
        }
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in new[]
                 {
                     ("open", "Open"), ("critical", "Critical & high"), ("mine", "Assigned"), ("all", "All signals")
                 })
        {
            var button = new ToggleButton
            {
                Content = label,
                Style = (Style)FindResource("Toggle.Segment"),
                Tag = key,
                IsChecked = key == _filter
            };
            button.Click += Filter_Click;
            _filters.Add(button);
            FilterGroup.Children.Add(button);
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string key }) return;
        _filter = key;
        foreach (var button in _filters) button.IsChecked = (string)button.Tag! == key;
        RefreshQueue();
    }

    private void RefreshQueue()
    {
        IEnumerable<SecurityAlert> query = _filter switch
        {
            "open" => _model.Alerts.Where(a => a.Status is not (AlertStatus.Closed or AlertStatus.FalsePositive)),
            "critical" => _model.Alerts.Where(a => a.Severity >= Severity.High),
            "mine" => _model.Alerts.Where(a => a.Assignee != "Unassigned"),
            _ => _model.Alerts
        };

        var items = query
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.FirstSeen)
            .ToList();

        var selected = AlertList.SelectedItem as SecurityAlert;
        AlertList.ItemsSource = items;
        if (selected is not null && items.Contains(selected)) AlertList.SelectedItem = selected;
        else if (items.Count > 0) AlertList.SelectedIndex = 0;

        int unassigned = items.Count(a => a.Assignee == "Unassigned");
        QueueSummary.Text = $"{items.Count} signals · {unassigned} unassigned";
    }

    private void Alert_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AlertList.SelectedItem is not SecurityAlert alert) return;

        var securityCase = _model.Case(alert.CaseId);
        if (securityCase is not null)
        {
            ShowCase(securityCase);
        }
        else
        {
            // An uncorrelated signal is shown on its own terms rather than pretending it has a case.
            ShowUncorrelated(alert);
        }

        var entity = alert.EntityIds.Select(id => _model.Entity(id))
            .Where(x => x is not null)
            .OrderByDescending(x => x!.RiskScore)
            .FirstOrDefault();
        if (entity is not null) ShowEntity(entity.Id);

        FocusService.Current.Set(FocusKind.Alert, alert.Id, alert.Rule, alert.Detail);
    }

    private void ShowCase(SecurityCase securityCase)
    {
        _case = securityCase;
        CaseId.Text = securityCase.Id;
        CaseTitle.Text = securityCase.Title;
        CaseHypothesis.Text = securityCase.Hypothesis;
        CaseOwner.Text = "owner " + securityCase.Owner;
        CaseAge.Text = "opened " + AgoConverter.Format(_model.Now - securityCase.OpenedAt) + " ago";

        var severity = Theme.ForSeverity(securityCase.Severity);
        CaseSeverityText.Text = securityCase.Severity.ToString().ToUpperInvariant();
        CaseSeverityText.Foreground = severity;
        CaseSeverityChip.Background = Theme.Frozen(Theme.WithAlpha(severity.Color, 0.14));
        CaseSeverityChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(severity.Color, 0.4));

        VerdictLabel.Text = "Verdict: " + securityCase.Verdict;

        Graph.Entities = securityCase.EntityIds
            .Select(id => _model.Entity(id))
            .Where(e => e is not null)
            .Cast<SecurityEntity>()
            .ToList();

        TimelineList.ItemsSource = securityCase.Timeline.OrderByDescending(t => t.At).ToList();
        TaskList.ItemsSource = securityCase.Tasks;
        BuildActions(securityCase);
    }

    private void ShowUncorrelated(SecurityAlert alert)
    {
        _case = null;
        CaseId.Text = alert.Id;
        CaseTitle.Text = alert.Rule;
        CaseHypothesis.Text = alert.Detail;
        CaseOwner.Text = "owner " + alert.Assignee;
        CaseAge.Text = "first seen " + AgoConverter.Format(_model.Now - alert.FirstSeen) + " ago";

        var severity = Theme.ForSeverity(alert.Severity);
        CaseSeverityText.Text = alert.Severity.ToString().ToUpperInvariant();
        CaseSeverityText.Foreground = severity;
        CaseSeverityChip.Background = Theme.Frozen(Theme.WithAlpha(severity.Color, 0.14));
        CaseSeverityChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(severity.Color, 0.4));

        VerdictLabel.Text = "Not correlated to a case";

        Graph.Entities = alert.EntityIds
            .Select(id => _model.Entity(id))
            .Where(e => e is not null)
            .Cast<SecurityEntity>()
            .ToList();

        TimelineList.ItemsSource = new List<TimelineEvent>
        {
            new() { At = alert.FirstSeen, Actor = "Detection", Text = $"{alert.Id} first observed", Weight = alert.Severity },
            new() { At = alert.LastSeen, Actor = "Detection", Text = $"Most recent signal, {alert.SignalCount} in total", Weight = Severity.Info }
        };
        TaskList.ItemsSource = Array.Empty<ResponseTask>();
        BuildActions(null);
    }

    private void ShowEntity(string id)
    {
        var entity = _model.Entity(id);
        if (entity is null) return;
        Graph.SelectedId = id;

        EntityKindText.Text = entity.Kind.ToString().ToUpperInvariant();
        EntityName.Text = entity.Name;
        EntityContext.Text = entity.Context;
        EntityRisk.Text = $"risk {entity.RiskScore}";
        EntityRisk.Foreground = entity.RiskScore >= 75 ? Theme.Critical
            : entity.RiskScore >= 45 ? Theme.Degraded : Theme.Unknown;
        EntityManaged.Text = entity.IsManaged ? "managed" : "not managed";
        EntityManaged.Foreground = entity.IsManaged ? Theme.Brush("B.TextTertiary") : Theme.Degraded;

        AttributeList.ItemsSource = entity.Attributes.ToList();
        FocusService.Current.Set(FocusKind.Entity, entity.Id, entity.Name, entity.Context);
    }

    private void BuildActions(SecurityCase? securityCase)
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

        var runbook = _model.Runbook("RB-021");
        if (securityCase is not null && runbook is not null)
            Add($"Stage {runbook.Id} — {runbook.Name}", () => Navigator.Current.NavigateWithSubject(
                "automation", FocusKind.Runbook, runbook.Id, runbook.Name, "Staged from " + securityCase.Id), true);

        var change = _model.Change("CHG-2307");
        if (securityCase is not null && change is not null)
            Add($"Open {change.Id} — structural fix", () => Navigator.Current.NavigateWithSubject(
                "changes", FocusKind.Change, change.Id, change.Title, "Raised from " + securityCase.Id));

        if (securityCase is not null && securityCase.EvidenceIds.Count > 0)
            Add("Review preserved evidence", () => Navigator.Current.NavigateWithSubject(
                "evidence", FocusKind.Evidence, securityCase.EvidenceIds[0],
                _model.EvidenceRecord(securityCase.EvidenceIds[0])?.Claim ?? "Evidence",
                "Opened from " + securityCase.Id));
    }

    private void Simulate_Click(object sender, RoutedEventArgs e) =>
        Navigator.Current.NavigateWithSubject("simulator", FocusKind.Journey, "journey.soc",
            _case?.Title ?? "Security detection and response", "Opened from SOC");
}
