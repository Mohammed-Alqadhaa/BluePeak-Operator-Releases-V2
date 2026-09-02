using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public sealed record FieldRow(string Label, string Value);

public sealed class TicketRow
{
    public required Ticket Ticket { get; init; }
    public required DateTime Now { get; init; }
    public required string ContextLabel { get; init; }
    public required bool ContextIsIncident { get; init; }

    public Brush ContextBrush => ContextIsIncident ? Theme.Degraded : Theme.Brush("B.TextTertiary");

    public Brush AssigneeBrush => Ticket.Assignee == "Unassigned"
        ? Theme.Degraded
        : Theme.Brush("B.TextSecondary");

    public bool Closed => Ticket.State is TicketState.Resolved or TicketState.Closed or TicketState.Verified;

    public HealthState SlaTone
    {
        get
        {
            if (Closed) return HealthState.Healthy;
            var left = Ticket.SlaRemaining(Now);
            if (left.Ticks < 0) return HealthState.Critical;
            double total = (Ticket.SlaDueAt - Ticket.OpenedAt).TotalMinutes;
            return left.TotalMinutes / Math.Max(1, total) < 0.25 ? HealthState.Degraded : HealthState.Healthy;
        }
    }

    public string SlaLabel
    {
        get
        {
            if (Closed) return "met";
            var left = Ticket.SlaRemaining(Now);
            return left.Ticks < 0 ? AgoConverter.Format(left) + " over" : AgoConverter.Format(left);
        }
    }

    /// <summary>Fraction of the SLA window consumed, clamped for rendering.</summary>
    public double SlaFraction
    {
        get
        {
            if (Closed) return 1;
            double total = (Ticket.SlaDueAt - Ticket.OpenedAt).TotalMinutes;
            double used = (Now - Ticket.OpenedAt).TotalMinutes;
            return Math.Clamp(total <= 0 ? 1 : used / total, 0, 1);
        }
    }
}

public partial class TicketsView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private readonly List<ToggleButton> _filters = new();
    private string _filter = "open";
    private string _search = "";

    public TicketsView()
    {
        InitializeComponent();
        BuildFilters();
        Refresh();
        TicketList.SelectedIndex = 0;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Ticket) return;
        var ticket = _model.Ticket(subject.Id);
        if (ticket is null) return;
        _filter = "all";
        _search = "";
        SearchBox.Text = "";
        foreach (var button in _filters) button.IsChecked = (string)button.Tag! == "all";
        Refresh();
        var row = (TicketList.ItemsSource as IEnumerable<TicketRow>)?.FirstOrDefault(r => r.Ticket.Id == ticket.Id);
        if (row is not null) TicketList.SelectedItem = row;
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in new[]
                 {
                     ("open", "Open"), ("breach", "SLA at risk"), ("unassigned", "Unowned"), ("all", "All")
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

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text.Trim();
        Refresh();
    }

    private void Refresh()
    {
        var now = _model.Now;
        IEnumerable<Ticket> query = _filter switch
        {
            "open" => _model.Tickets.Where(t => t.State is not (TicketState.Closed or TicketState.Verified)),
            "breach" => _model.Tickets.Where(t => t.SlaBreached(now) ||
                            (t.SlaRemaining(now).TotalMinutes is > 0 and < 60 &&
                             t.State is not (TicketState.Resolved or TicketState.Closed or TicketState.Verified))),
            "unassigned" => _model.Tickets.Where(t => t.Assignee == "Unassigned"),
            _ => _model.Tickets
        };

        if (_search.Length > 0)
            query = query.Where(t =>
                t.Id.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                t.Subject.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                t.Requester.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                t.Department.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                t.Assignee.Contains(_search, StringComparison.OrdinalIgnoreCase));

        var rows = query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.SlaDueAt)
            .Select(t => new TicketRow
            {
                Ticket = t,
                Now = now,
                ContextLabel = Context(t),
                ContextIsIncident = t.LinkedIncidentId is not null
            })
            .ToList();

        var selected = TicketList.SelectedItem as TicketRow;
        TicketList.ItemsSource = rows;
        var restored = selected is null ? null : rows.FirstOrDefault(r => r.Ticket.Id == selected.Ticket.Id);
        if (restored is not null) TicketList.SelectedItem = restored;
        else if (rows.Count > 0) TicketList.SelectedIndex = 0;

        int breaching = rows.Count(r => r.SlaTone == HealthState.Critical);
        Summary.Text = $"{rows.Count} tickets · {breaching} past SLA";
    }

    private string Context(Ticket ticket)
    {
        if (ticket.LinkedIncidentId is not null)
            return $"Inside blast radius of {ticket.LinkedIncidentId}";
        if (ticket.LinkedServiceId is not null)
            return _model.NameOf(ticket.LinkedServiceId);
        return "No linked service";
    }

    private void Ticket_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TicketList.SelectedItem is TicketRow row) Show(row);
    }

    private void Show(TicketRow row)
    {
        var ticket = row.Ticket;
        DetailId.Text = ticket.Id;
        DetailSubject.Text = ticket.Subject;
        DetailSummary.Text = ticket.Summary;
        DetailChannel.Text = ticket.Channel.ToLowerInvariant() + " · " + ticket.Queue;

        var priority = Theme.ForSeverity(ticket.Priority);
        DetailPriorityText.Text = ticket.Priority.ToString().ToUpperInvariant();
        DetailPriorityText.Foreground = priority;
        DetailPriorityChip.Background = Theme.Frozen(Theme.WithAlpha(priority.Color, 0.14));
        DetailPriorityChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(priority.Color, 0.4));

        SlaMeter.Value = row.SlaFraction;
        SlaMeter.State = row.SlaTone;
        SlaHeadline.Text = row.Closed ? "Met" : row.SlaTone switch
        {
            HealthState.Critical => "Breached",
            HealthState.Degraded => "At risk",
            _ => "Within target"
        };
        SlaHeadline.Foreground = Theme.ForHealth(row.SlaTone);
        SlaDetail.Text = row.Closed
            ? $"Resolved after {AgoConverter.Format(_model.Now - ticket.OpenedAt)}"
            : $"{AgoConverter.FormatTarget(ticket.SlaRemaining(_model.Now))} · target {ticket.SlaDueAt:HH:mm}";

        var context = new List<FieldRow>
        {
            new("Requester", $"{ticket.Requester} — {ticket.Department}"),
            new("Opened", $"{ticket.OpenedAt:HH:mm} · {AgoConverter.Format(_model.Now - ticket.OpenedAt)} ago"),
            new("State", ticket.State.ToString()),
            new("Assignee", ticket.Assignee),
            new("Queue", ticket.Queue)
        };
        if (ticket.LinkedServiceId is not null)
            context.Add(new FieldRow("Service", _model.NameOf(ticket.LinkedServiceId)));
        if (ticket.LinkedIncidentId is not null)
            context.Add(new FieldRow("Incident", $"{ticket.LinkedIncidentId} — {_model.Incident(ticket.LinkedIncidentId)?.Title}"));
        if (ticket.SimilarTicketIds.Count > 0)
            context.Add(new FieldRow("Similar", string.Join(", ", ticket.SimilarTicketIds)));
        ContextList.ItemsSource = context;

        TimelineList.ItemsSource = ticket.Timeline.OrderByDescending(t => t.At).ToList();

        BuildActions(ticket);
        FocusService.Current.Set(FocusKind.Ticket, ticket.Id, ticket.Subject, ticket.Summary);
    }

    private void BuildActions(Ticket ticket)
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

        if (ticket.LinkedIncidentId is not null && _model.Incident(ticket.LinkedIncidentId) is { } incident)
            Add($"Open {incident.Id}", () => Navigator.Current.NavigateWithSubject(
                "incidents", FocusKind.Incident, incident.Id, incident.Title, "Opened from ticket " + ticket.Id), true);

        if (ticket.LinkedServiceId is not null && _model.Node(ticket.LinkedServiceId) is { } node)
            Add($"Inspect {node.Name}", () => Navigator.Current.NavigateWithSubject(
                "noc", FocusKind.Service, node.Id, node.Name, node.StateReason));

        foreach (var id in ticket.EvidenceIds)
            if (_model.EvidenceRecord(id) is { } record)
                Add($"Evidence {record.Id}", () => Navigator.Current.NavigateWithSubject(
                    "evidence", FocusKind.Evidence, record.Id, record.Claim, "Opened from ticket " + ticket.Id));

        if (ticket.LinkedIncidentId is null)
            Add("Triage in Service Desk", () => Navigator.Current.NavigateWithSubject(
                "servicedesk", FocusKind.Ticket, ticket.Id, ticket.Subject, "Sent for triage"));
    }
}
