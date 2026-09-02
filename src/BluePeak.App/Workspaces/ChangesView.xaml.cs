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

public sealed class ChangeRow
{
    public required ChangeRequest Change { get; init; }
    public required DateTime Now { get; init; }

    public HealthState RiskTone => Change.Risk switch
    {
        ChangeRisk.Critical => HealthState.Critical,
        ChangeRisk.High => HealthState.Critical,
        ChangeRisk.Moderate => HealthState.Degraded,
        _ => HealthState.Healthy
    };

    public Brush StateBrush => Change.State switch
    {
        ChangeState.AwaitingApproval => Theme.Degraded,
        ChangeState.Implementing => Theme.Accent,
        ChangeState.RolledBack or ChangeState.Rejected => Theme.Critical,
        ChangeState.Completed => Theme.Healthy,
        _ => Theme.Brush("B.TextTertiary")
    };

    public string WindowLabel
    {
        get
        {
            if (Now >= Change.WindowStart && Now <= Change.WindowEnd) return "open now";
            if (Change.WindowStart > Now) return "in " + AgoConverter.Format(Change.WindowStart - Now);
            return AgoConverter.Format(Now - Change.WindowEnd) + " ago";
        }
    }
}

public sealed class BlastRow
{
    public required ServiceNode Node { get; init; }
    public required bool IsTarget { get; init; }
    public string RoleLabel => IsTarget ? "TARGET" : "EXPOSED";
    public Brush RoleBrush => IsTarget ? Theme.Accent : Theme.Degraded;
}

public partial class ChangesView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly List<ToggleButton> _filters = new();
    private string _filter = "active";
    private ChangeRequest? _change;

    public ChangesView()
    {
        InitializeComponent();
        BuildFilters();
        Refresh();
        ChangeList.SelectedIndex = 0;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Change) return;
        var change = _model.Change(subject.Id);
        if (change is null) return;
        _filter = "all";
        foreach (var button in _filters) button.IsChecked = (string)button.Tag! == "all";
        Refresh();
        var row = (ChangeList.ItemsSource as IEnumerable<ChangeRow>)?.FirstOrDefault(r => r.Change.Id == change.Id);
        if (row is not null) ChangeList.SelectedItem = row;
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in new[]
                 {
                     ("active", "Active"), ("approval", "Awaiting approval"), ("all", "All")
                 })
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
        IEnumerable<ChangeRequest> query = _filter switch
        {
            "active" => _model.Changes.Where(c => c.State is not (ChangeState.Completed or ChangeState.Rejected)),
            "approval" => _model.Changes.Where(c => c.State == ChangeState.AwaitingApproval ||
                                                    c.Approvals.Any(a => a.State is GateState.Pending or GateState.WaitingApproval)),
            _ => _model.Changes
        };

        var rows = query
            .OrderByDescending(c => c.State == ChangeState.AwaitingApproval)
            .ThenByDescending(c => c.Risk)
            .ThenBy(c => c.WindowStart)
            .Select(c => new ChangeRow { Change = c, Now = _model.Now })
            .ToList();

        var selected = ChangeList.SelectedItem as ChangeRow;
        ChangeList.ItemsSource = rows;
        var restored = selected is null ? null : rows.FirstOrDefault(r => r.Change.Id == selected.Change.Id);
        if (restored is not null) ChangeList.SelectedItem = restored;
        else if (rows.Count > 0) ChangeList.SelectedIndex = 0;

        int waiting = rows.Count(r => r.Change.State == ChangeState.AwaitingApproval);
        Summary.Text = $"{rows.Count} changes · {waiting} awaiting approval";
    }

    private void Change_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChangeList.SelectedItem is ChangeRow row) Show(row.Change);
    }

    private void Show(ChangeRequest change)
    {
        _change = change;
        DetailId.Text = change.Id;
        DetailTitle.Text = change.Title;
        DetailDescription.Text = change.Description;
        DetailType.Text = change.Type.ToLowerInvariant() + " change";
        DetailWindow.Text = $"{change.WindowStart:HH:mm}–{change.WindowEnd:HH:mm} · backout {change.BackoutTime}";

        var tone = change.Risk switch
        {
            ChangeRisk.Critical or ChangeRisk.High => HealthState.Critical,
            ChangeRisk.Moderate => HealthState.Degraded,
            _ => HealthState.Healthy
        };
        var brush = Theme.ForHealth(tone);
        RiskText.Text = change.Risk.ToString().ToUpperInvariant() + " RISK";
        RiskText.Foreground = brush;
        RiskChip.Background = Theme.Frozen(Theme.WithAlpha(brush.Color, 0.14));
        RiskChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(brush.Color, 0.4));

        var target = _model.Node(change.TargetServiceId);
        var blast = new List<BlastRow>();
        if (target is not null) blast.Add(new BlastRow { Node = target, IsTarget = true });
        foreach (var id in change.BlastRadiusServiceIds)
            if (_model.Node(id) is { } node && node.Id != target?.Id)
                blast.Add(new BlastRow { Node = node, IsTarget = false });
        BlastList.ItemsSource = blast;
        BlastSummary.Text = $"{blast.Count} services in scope · "
                          + $"{blast.Count(b => b.Node.Tier == 1)} tier-1";

        ApprovalList.ItemsSource = change.Approvals;

        VerificationList.ItemsSource = change.Verification;
        int notRun = change.Verification.Count(v => v.Result == EvidenceResult.NotRun);
        int inconclusive = change.Verification.Count(v => v.Result == EvidenceResult.Inconclusive);
        VerificationSummary.Text = inconclusive > 0
            ? $"{inconclusive} inconclusive · {notRun} not run"
            : notRun > 0 ? $"{notRun} not yet run" : "all checks recorded";

        RollbackText.Text = change.RollbackPlan;
        var evidence = change.EvidenceIds
            .Select(id => _model.EvidenceRecord(id))
            .Where(r => r is not null)
            .ToList();
        EvidenceList.ItemsSource = evidence;

        // An empty section heading is worse than none. Say why there is nothing here.
        EvidenceEmpty.Visibility = evidence.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EvidenceEmpty.Text = change.State switch
        {
            ChangeState.Completed => "No evidence was sealed for this change. Nothing proves it worked.",
            ChangeState.Implementing => "Nothing sealed yet — the change is still in its window.",
            _ => "Nothing sealed yet. Records appear once the verification plan runs."
        };

        BuildBrief(change, blast, inconclusive, notRun);
        BuildActions(change);
        FocusService.Current.Set(FocusKind.Change, change.Id, change.Title, change.Description);
    }

    private void BuildBrief(ChangeRequest change, List<BlastRow> blast, int inconclusive, int notRun)
    {
        var pending = change.Approvals.Where(a => a.State is GateState.Pending or GateState.WaitingApproval).ToList();

        BriefList.ItemsSource = new List<AnswerRow>
        {
            new("What is being changed?",
                $"{_model.NameOf(change.TargetServiceId)} — {change.Description.Split('.')[0]}.",
                HealthState.Unknown),

            new("Who is exposed?",
                $"{blast.Count} services, {blast.Count(b => b.Node.Tier == 1)} of them tier-1. "
                + $"{blast.Count(b => b.Node.Health.IsBad())} are already unhealthy.",
                blast.Count(b => b.Node.Tier == 1) > 2 ? HealthState.Critical : HealthState.Degraded),

            new("Can it be undone?",
                $"Yes — {change.BackoutTime} backout. {change.RollbackPlan.Split('.')[0]}.",
                HealthState.Healthy),

            new("How will we know it worked?",
                inconclusive > 0
                    ? $"{change.Verification.Count} checks defined, but {inconclusive} were recorded inconclusive. "
                      + "This change closed without proving its own outcome."
                    : $"{change.Verification.Count} checks defined, asserted from consumers of the target.",
                inconclusive > 0 ? HealthState.Critical : HealthState.Healthy),

            new("What is outstanding?",
                pending.Count == 0
                    ? "All approvals recorded."
                    : $"{pending.Count} approval{(pending.Count == 1 ? "" : "s")} outstanding: "
                      + string.Join(", ", pending.Select(p => $"{p.Board} ({p.Approver})")) + ".",
                pending.Count > 0 ? HealthState.Degraded : HealthState.Healthy)
        };
    }

    private void BuildActions(ChangeRequest change)
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

        var pending = change.Approvals.FirstOrDefault(a => a.State is GateState.Pending or GateState.WaitingApproval);
        if (pending is not null)
            Add($"Record {pending.Board} decision", () => RecordApproval(change, pending), true);

        if (change.LinkedIncidentId is not null && _model.Incident(change.LinkedIncidentId) is { } incident)
            Add($"Open {incident.Id}", () => Navigator.Current.NavigateWithSubject(
                "incidents", FocusKind.Incident, incident.Id, incident.Title, "Opened from Changes"));

        var runbook = _model.Runbooks.FirstOrDefault(r => r.TargetServiceIds.Contains(change.TargetServiceId));
        if (runbook is not null)
            Add($"Open {runbook.Id}", () => Navigator.Current.NavigateWithSubject(
                "automation", FocusKind.Runbook, runbook.Id, runbook.Name, "Opened from Changes"));

        Add("Inspect target in Infrastructure", () => Navigator.Current.NavigateWithSubject(
            "infrastructure", FocusKind.Service, change.TargetServiceId,
            _model.NameOf(change.TargetServiceId), "Opened from Changes"));
    }

    /// <summary>
    /// Records an approval decision in the local session. This never leaves the process and is
    /// explicitly not an authorisation for anything outside it.
    /// </summary>
    private void RecordApproval(ChangeRequest change, Approval approval)
    {
        approval.State = GateState.Passed;
        approval.DecidedAt = _model.Now;
        approval.Comment = "Recorded in the local operator session. Not authoritative outside it.";
        if (change.Approvals.All(a => a.State == GateState.Passed) && change.State == ChangeState.AwaitingApproval)
            change.State = ChangeState.Approved;
        Refresh();
        Show(change);
    }

    private void Service_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id }) return;
        var node = _model.Node(id);
        if (node is null) return;
        Navigator.Current.NavigateWithSubject("noc", FocusKind.Service, node.Id, node.Name, node.StateReason);
    }

    private void Evidence_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id }) return;
        var record = _model.EvidenceRecord(id);
        if (record is null) return;
        Navigator.Current.NavigateWithSubject("evidence", FocusKind.Evidence, record.Id, record.Claim,
            "Opened from Changes");
    }
}
