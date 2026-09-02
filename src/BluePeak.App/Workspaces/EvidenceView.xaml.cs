using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public partial class EvidenceView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly List<ToggleButton> _filters = new();
    private string _filter = "all";

    public EvidenceView()
    {
        InitializeComponent();
        BuildFilters();
        Refresh();
        RecordList.SelectedIndex = 0;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Evidence) return;
        var record = _model.EvidenceRecord(subject.Id);
        if (record is null) return;
        _filter = "all";
        foreach (var button in _filters) button.IsChecked = (string)button.Tag! == "all";
        Refresh();
        RecordList.SelectedItem = record;
        RecordList.ScrollIntoView(record);
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in new[]
                 {
                     ("all", "All"), ("failing", "Failing"), ("local", "Local operator"), ("attested", "Attested")
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
        IEnumerable<EvidenceRecord> query = _filter switch
        {
            "failing" => _model.Evidence.Where(e => e.Result is EvidenceResult.Fail or EvidenceResult.Inconclusive),
            "local" => _model.Evidence.Where(e => e.Authority == EvidenceAuthority.LocalOperator),
            "attested" => _model.Evidence.Where(e => e.Authority != EvidenceAuthority.LocalOperator),
            _ => _model.Evidence
        };

        var items = query.OrderByDescending(e => e.CapturedAt).ToList();
        var selected = RecordList.SelectedItem as EvidenceRecord;
        RecordList.ItemsSource = items;
        if (selected is not null && items.Contains(selected)) RecordList.SelectedItem = selected;
        else if (items.Count > 0) RecordList.SelectedIndex = 0;

        int local = items.Count(e => e.Authority == EvidenceAuthority.LocalOperator);
        int preserved = items.Count(e => e.Preserved);
        Summary.Text = $"{items.Count} records · {local} local operator · {preserved} preserved";
    }

    private void Record_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordList.SelectedItem is EvidenceRecord record) Show(record);
    }

    private void Show(EvidenceRecord record)
    {
        DetailId.Text = record.Id;
        DetailClaim.Text = record.Claim;

        var resultBrush = record.Result switch
        {
            EvidenceResult.Pass => Theme.Healthy,
            EvidenceResult.Fail => Theme.Critical,
            EvidenceResult.Inconclusive => Theme.Degraded,
            _ => Theme.Unknown
        };
        ResultText.Text = record.Result.ToString().ToUpperInvariant();
        ResultText.Foreground = resultBrush;
        ResultChip.Background = Theme.Frozen(Theme.WithAlpha(resultBrush.Color, 0.14));
        ResultChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(resultBrush.Color, 0.4));

        var authorityBrush = record.Authority switch
        {
            EvidenceAuthority.ProjectAuthoritative => Theme.Healthy,
            EvidenceAuthority.PlatformAttested => Theme.Accent,
            _ => Theme.Degraded
        };
        AuthorityText.Text = record.Authority switch
        {
            EvidenceAuthority.ProjectAuthoritative => "PROJECT AUTHORITATIVE",
            EvidenceAuthority.PlatformAttested => "PLATFORM ATTESTED",
            _ => "LOCAL OPERATOR"
        };
        AuthorityText.Foreground = authorityBrush;
        AuthorityChip.Background = Theme.Frozen(Theme.WithAlpha(authorityBrush.Color, 0.14));
        AuthorityChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(authorityBrush.Color, 0.4));

        ModelList.ItemsSource = new List<FieldRow>
        {
            new("Claim", record.Claim),
            new("Source", record.Source),
            new("Check", record.Check),
            new("Result", record.Result.ToString()),
            new("Timestamp", $"{record.CapturedAt:yyyy-MM-dd HH:mm:ss} · {AgoConverter.Format(_model.Now - record.CapturedAt)} ago"),
            new("Collector", record.Collector),
            new("Authority", AuthorityText.Text)
        };

        ExpectedText.Text = string.IsNullOrEmpty(record.Expected) ? "not stated" : record.Expected;
        ObservedText.Text = string.IsNullOrEmpty(record.Observed) ? "not recorded" : record.Observed;
        ObservedBar.Fill = resultBrush;

        DigestText.Text = "sha256:" + record.Digest;
        SealedText.Text = record.Preserved ? "yes" : "no";
        SealedText.Foreground = record.Preserved ? Theme.Healthy : Theme.Degraded;
        ScopeText.Text = record.Scope;

        AuthorityNote.Text = record.Authority switch
        {
            EvidenceAuthority.LocalOperator =>
                "Produced on this workstation. It is admissible as operator observation and nothing more. "
                + "Presenting it as a project position would overstate what was actually verified.",
            EvidenceAuthority.PlatformAttested =>
                "Countersigned by a platform control plane, so the observation is independent of this workstation. "
                + "It is not yet accepted into the immutable project record.",
            _ =>
                "Accepted into the immutable project record. It can be cited as a project position and cannot be "
                + "amended from here."
        };

        BuildReferences(record);
        FocusService.Current.Set(FocusKind.Evidence, record.Id, record.Claim, record.Check);
    }

    private void BuildReferences(EvidenceRecord record)
    {
        ReferencePanel.Children.Clear();

        void Add(string caption, string workspace, FocusKind kind, string id, string label)
        {
            var button = new Button
            {
                Content = caption,
                Style = (Style)FindResource("Button.Standard"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8)
            };
            button.Click += (_, _) => Navigator.Current.NavigateWithSubject(workspace, kind, id, label,
                "Opened from evidence " + record.Id);
            ReferencePanel.Children.Add(button);
        }

        foreach (var incident in _model.Incidents.Where(i => i.EvidenceIds.Contains(record.Id)))
            Add($"Incident {incident.Id}", "incidents", FocusKind.Incident, incident.Id, incident.Title);
        foreach (var ticket in _model.Tickets.Where(t => t.EvidenceIds.Contains(record.Id)))
            Add($"Ticket {ticket.Id}", "tickets", FocusKind.Ticket, ticket.Id, ticket.Subject);
        foreach (var securityCase in _model.Cases.Where(c => c.EvidenceIds.Contains(record.Id)))
            Add($"Case {securityCase.Id}", "soc", FocusKind.Case, securityCase.Id, securityCase.Title);
        foreach (var change in _model.Changes.Where(c => c.EvidenceIds.Contains(record.Id)))
            Add($"Change {change.Id}", "changes", FocusKind.Change, change.Id, change.Title);
        foreach (var path in _model.DiagnosticPaths.Where(p => p.Hops.Any(h => h.EvidenceId == record.Id)))
            Add($"Dependency walk {path.Id}", "diagnostics", FocusKind.Service, path.Id, path.Name);
        if (record.SubjectId is not null && _model.Node(record.SubjectId) is { } node)
            Add($"Service {node.Name}", "noc", FocusKind.Service, node.Id, node.Name);

        if (ReferencePanel.Children.Count == 0)
            ReferencePanel.Children.Add(new TextBlock
            {
                Text = "Not referenced by any open record.",
                Style = (Style)FindResource("Text.Tertiary")
            });
    }
}
