using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.App.Shell;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

public sealed class ReasonRow
{
    public required TriageReason Reason { get; init; }
    public string Label => Reason.Label;
    public string Detail => Reason.Detail;
    public Geometry? GlyphData => Application.Current.TryFindResource(Reason.Supports ? "I.Soc" : "I.Incident") as Geometry;
    public Brush GlyphBrush => Reason.Supports ? Theme.Degraded : Theme.Healthy;
}

public partial class ServiceDeskView : UserControl, IFocusAware
{
    private readonly EstateModel _model = EstateService.Current.Model;
    private TriageAssessment? _assessment;

    public ServiceDeskView()
    {
        InitializeComponent();
        Refresh();
        if (QueueList.Items.Count > 0) QueueList.SelectedIndex = 0;
    }

    public void ApplyFocus(FocusSubject subject)
    {
        if (subject.Kind != FocusKind.Ticket) return;
        var ticket = _model.Ticket(subject.Id);
        if (ticket is null) return;
        if (QueueList.ItemsSource is IEnumerable<Ticket> items && !items.Contains(ticket))
        {
            QueueList.ItemsSource = items.Concat(new[] { ticket }).ToList();
        }
        QueueList.SelectedItem = ticket;
    }

    private void Refresh()
    {
        var now = _model.Now;
        var queue = _model.Tickets
            .Where(t => t.State is TicketState.New or TicketState.Triage or TicketState.Escalated or TicketState.InProgress)
            .OrderBy(t => t.Assignee == "Unassigned" ? 0 : 1)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.OpenedAt)
            .ToList();

        var selected = QueueList.SelectedItem as Ticket;
        QueueList.ItemsSource = queue;
        if (selected is not null && queue.Contains(selected)) QueueList.SelectedItem = selected;
        else if (queue.Count > 0) QueueList.SelectedIndex = 0;

        BuildCounters(queue, now);
    }

    private void BuildCounters(List<Ticket> queue, DateTime now)
    {
        CounterStrip.Children.Clear();

        void Add(string label, string value, HealthState tone)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 22, 0), VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontFamily = (FontFamily)FindResource("F.Mono"),
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = Theme.ForHealth(tone)
            });
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("Text.Tertiary"),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            CounterStrip.Children.Add(panel);
        }

        int unowned = queue.Count(t => t.Assignee == "Unassigned");
        int breaching = queue.Count(t => t.SlaBreached(now));
        int attached = queue.Count(t => t.LinkedIncidentId is not null);

        Add("in queue", queue.Count.ToString(), HealthState.Unknown);
        Add("unowned", unowned.ToString(), unowned > 0 ? HealthState.Degraded : HealthState.Healthy);
        Add("past SLA", breaching.ToString(), breaching > 0 ? HealthState.Critical : HealthState.Healthy);
        Add("already attached", attached.ToString(), HealthState.Unknown);
    }

    private void Queue_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QueueList.SelectedItem is Ticket ticket) Show(ticket);
    }

    private void Show(Ticket ticket)
    {
        var assessment = TriageAssessment.For(_model, ticket);
        _assessment = assessment;

        ContactId.Text = ticket.Id;
        ContactMeta.Text = $"{ticket.Channel.ToLowerInvariant()} · {ticket.Requester}, {ticket.Department} · "
                         + $"raised {AgoConverter.Format(_model.Now - ticket.OpenedAt)} ago";
        ContactSubject.Text = ticket.Subject;
        ContactSummary.Text = ticket.Summary;

        bool attach = assessment.Match is not null;
        var tone = attach ? Theme.Degraded : Theme.Healthy;
        DecisionBorder.BorderBrush = tone;
        DecisionHeadline.Text = assessment.Recommendation;
        DecisionHeadline.Foreground = tone;
        DecisionDetail.Text = assessment.RecommendationDetail;

        ConfidenceText.Text = attach ? $"{assessment.Confidence}% MATCH" : "NO MATCH";
        ConfidenceText.Foreground = tone;
        ConfidenceChip.Background = Theme.Frozen(Theme.WithAlpha(tone.Color, 0.14));
        ConfidenceChip.BorderBrush = Theme.Frozen(Theme.WithAlpha(tone.Color, 0.4));

        ReasonList.ItemsSource = assessment.Reasons.Select(r => new ReasonRow { Reason = r }).ToList();

        bool alreadyAttached = ticket.LinkedIncidentId is not null;
        if (alreadyAttached)
        {
            AttachButton.Visibility = Visibility.Collapsed;
            AttachedNotice.Visibility = Visibility.Visible;
            AttachedText.Text = $"ATTACHED TO {ticket.LinkedIncidentId}";
        }
        else
        {
            AttachedNotice.Visibility = Visibility.Collapsed;
            AttachButton.Visibility = Visibility.Visible;
            AttachButton.Content = attach ? $"Attach to {assessment.Match!.Id}" : "Attach to an incident";
            AttachButton.IsEnabled = attach;
        }
        NewFaultButton.IsEnabled = !alreadyAttached;
        OpenIncidentButton.Visibility = attach || alreadyAttached ? Visibility.Visible : Visibility.Collapsed;

        HistoryList.ItemsSource = ticket.Timeline.OrderByDescending(t => t.At).ToList();
        HistorySummary.Text = $"{ticket.Timeline.Count} entries since {ticket.OpenedAt:HH:mm}";
        BuildCost(assessment, attach);

        SimilarList.ItemsSource = assessment.Similar;
        SimilarSummary.Text = assessment.Similar.Count == 0
            ? "none in the last 24 hours"
            : $"{assessment.Similar.Count} in the last 24 hours";

        ClassificationList.ItemsSource = new List<FieldRow>
        {
            new("Suggested", assessment.SuggestedClassification),
            new("Priority", assessment.SuggestedPriority),
            new("Service", ticket.LinkedServiceId is null ? "not set" : _model.NameOf(ticket.LinkedServiceId)),
            new("Queue", ticket.Queue),
            new("Assignee", ticket.Assignee),
            new("SLA target", ticket.SlaDueAt.ToString("HH:mm"))
        };

        RequesterUpdate.Text = assessment.RequesterUpdate;
        HonestyNote.Text = attach
            ? "No workaround is offered. A workaround that does not work costs more trust than an honest wait."
            : "Do not promise a resolution time before the cause is known.";

        RequesterList.ItemsSource = new List<FieldRow>
        {
            new("Name", ticket.Requester),
            new("Department", ticket.Department),
            new("Channel", ticket.Channel),
            new("Opened", ticket.OpenedAt.ToString("HH:mm")),
            new("Contacts", _model.Tickets.Count(t => t.Requester == ticket.Requester).ToString())
        };

        FocusService.Current.Set(FocusKind.Ticket, ticket.Id, ticket.Subject, ticket.Summary);
    }

    /// <summary>
    /// Triage is usually presented as a routing chore. Stating what each option costs is what
    /// makes it a decision rather than a dropdown.
    /// </summary>
    private void BuildCost(TriageAssessment assessment, bool attach)
    {
        var rows = new List<FieldRow>();
        if (attach)
        {
            rows.Add(new FieldRow("If attached", "Requester gets the incident's update cadence. No duplicate diagnosis. "
                                               + "The contact still counts as impact evidence."));
            rows.Add(new FieldRow("If diagnosed separately",
                "40 to 60 minutes of first and second line time reproducing a fault that already has a commander, "
                + "and a requester who is told nothing while it happens."));
            rows.Add(new FieldRow("Risk of attaching wrongly",
                "The contact inherits a resolution it did not need. Detach is one click and the history is preserved."));
        }
        else
        {
            rows.Add(new FieldRow("If diagnosed", "The fault gets an owner and a first-line diagnosis inside the SLA window."));
            rows.Add(new FieldRow("If attached wrongly",
                "It disappears into an incident that will close without fixing it, and the requester is never contacted."));
            rows.Add(new FieldRow("Evidence available",
                assessment.Similar.Count == 0
                    ? "No comparable contact in the last 24 hours to reason from."
                    : $"{assessment.Similar.Count} comparable contacts to check for a pattern."));
        }
        CostList.ItemsSource = rows;
    }

    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        if (_assessment?.Match is null) return;
        var ticket = _assessment.Ticket;
        var incident = _assessment.Match;

        // Local session state only. Nothing here reaches a production record system.
        ticket.LinkedIncidentId = incident.Id;
        ticket.State = TicketState.InProgress;
        ticket.Assignee = incident.Commander;
        ticket.Queue = "Incident Response";
        if (incident.Severity >= Severity.Critical && ticket.Priority < Severity.High)
            ticket.Priority = Severity.High;
        ticket.Timeline.Insert(0, new TimelineEvent
        {
            At = _model.Now,
            Actor = "Service Desk",
            Text = $"Attached to {incident.Id} at triage — {_assessment.Confidence}% match on shared dependency",
            Channel = "human",
            Weight = Severity.Medium
        });
        if (!incident.LinkedTicketIds.Contains(ticket.Id)) incident.LinkedTicketIds.Add(ticket.Id);

        Refresh();
        Show(ticket);
    }

    private void NewFault_Click(object sender, RoutedEventArgs e)
    {
        if (_assessment is null) return;
        var ticket = _assessment.Ticket;
        ticket.State = TicketState.InProgress;
        if (ticket.Assignee == "Unassigned")
        {
            ticket.Assignee = "D. Marchetti";
            ticket.Queue = "Service Desk L2";
        }
        ticket.Timeline.Insert(0, new TimelineEvent
        {
            At = _model.Now,
            Actor = "Service Desk",
            Text = "Triaged as an independent fault — no open incident explains the reported service",
            Channel = "human"
        });
        Refresh();
        Show(ticket);
    }

    private void OpenIncident_Click(object sender, RoutedEventArgs e)
    {
        string? id = _assessment?.Match?.Id ?? _assessment?.Ticket.LinkedIncidentId;
        if (id is null) return;
        var incident = _model.Incident(id);
        if (incident is null) return;
        Navigator.Current.NavigateWithSubject("incidents", FocusKind.Incident, incident.Id, incident.Title,
            "Opened from Service Desk triage");
    }

    private void Similar_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id }) return;
        var ticket = _model.Ticket(id);
        if (ticket is null) return;
        Navigator.Current.NavigateWithSubject("tickets", FocusKind.Ticket, ticket.Id, ticket.Subject,
            "Opened from Service Desk");
    }
}
