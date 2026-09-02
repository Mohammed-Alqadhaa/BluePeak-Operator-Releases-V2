using BluePeak.App.Services;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

/// <summary>
/// One thing an operator should deal with, with the reason attached. The board is a ranked
/// worklist rather than a set of counters, because a counter tells you a number and a
/// worklist tells you what to do.
/// </summary>
public sealed class AttentionItem
{
    public required int Rank { get; init; }
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Why { get; init; }
    public required string Metric { get; init; }
    public required string MetricLabel { get; init; }
    public required string Age { get; init; }
    public required string NextAction { get; init; }
    public required HealthState Tone { get; init; }
    public required string Workspace { get; init; }
    public required FocusKind FocusKind { get; init; }
    public string Owner { get; init; } = "Unassigned";
}

/// <summary>Something that is not failing yet but has a deadline or a removed safety margin.</summary>
public sealed class RiskItem
{
    public required string Subject { get; init; }
    public required string Statement { get; init; }
    public required string Horizon { get; init; }
    public required HealthState Tone { get; init; }
    public required string ServiceId { get; init; }
}

public sealed class LayerRow
{
    public required EstateLayer Layer { get; init; }
    public required string Name { get; init; }
    public required LayerRollup Rollup { get; init; }
    public required string Detail { get; init; }
    public HealthState Worst => Rollup.Worst;

    /// <summary>Short, fixed-width verdict so the column never truncates. Detail lives in the tooltip.</summary>
    public string Verdict =>
        Rollup.Critical > 0 ? $"{Rollup.Critical} impaired"
        : Rollup.Degraded > 0 ? $"{Rollup.Degraded} degraded"
        : Rollup.Maintenance > 0 ? $"{Rollup.Maintenance} planned"
        : "nominal";
}

public sealed class OverviewViewModel
{
    private readonly EstateModel _m = EstateService.Current.Model;

    public OverviewViewModel()
    {
        Attention = BuildAttention();
        Layers = BuildLayers();
        Degraded = _m.Unhealthy().ToList();
        Risks = BuildRisks();
        Activity = _m.ActivityFeed.OrderByDescending(e => e.At).ToList();
        AsOf = _m.Now.ToString("dddd d MMMM · HH:mm");

        int critical = _m.Nodes.Count(n => n.Health is HealthState.Critical or HealthState.Offline);
        int degraded = _m.Nodes.Count(n => n.Health == HealthState.Degraded);
        Verdict = critical > 0 ? "Impaired" : degraded > 0 ? "Degraded" : "Nominal";
        VerdictTone = critical > 0 ? HealthState.Critical : degraded > 0 ? HealthState.Degraded : HealthState.Healthy;
        VerdictDetail = critical > 0
            ? $"{critical} services impaired and {degraded} degraded. One major incident holds most of the impact."
            : $"{degraded} services degraded. No impaired services.";
    }

    public IReadOnlyList<AttentionItem> Attention { get; }
    public IReadOnlyList<LayerRow> Layers { get; }
    public IReadOnlyList<ServiceNode> Degraded { get; }
    public IReadOnlyList<RiskItem> Risks { get; }
    public IReadOnlyList<TimelineEvent> Activity { get; }
    public string AsOf { get; }
    public string Verdict { get; }
    public string VerdictDetail { get; }
    public HealthState VerdictTone { get; }

    private List<AttentionItem> BuildAttention()
    {
        var items = new List<(double Score, AttentionItem Item)>();
        var now = _m.Now;

        foreach (var incident in _m.Incidents.Where(i => i.State != IncidentState.Resolved))
        {
            double score = 1000 - (int)incident.Severity * 100 + (incident.State == IncidentState.Monitoring ? 240 : 0);
            string next = incident.State switch
            {
                IncidentState.Identified when incident.SuspectedChangeId is not null =>
                    $"Approve {NextChangeFor(incident)} and execute the correction",
                IncidentState.Mitigating => "Confirm mitigation holds, then schedule the permanent fix",
                IncidentState.Monitoring => "Close, or extend the watch window with a reason",
                _ => "Establish the first failing component"
            };
            items.Add((score, new AttentionItem
            {
                Rank = 0,
                Kind = "INCIDENT",
                Id = incident.Id,
                Title = incident.Title,
                Why = incident.Impact,
                Metric = incident.UsersAffected > 0 ? incident.UsersAffected.ToString("N0") : "none",
                MetricLabel = incident.UsersAffected > 0 ? "users affected" : "user impact",
                Age = Design.AgoConverter.Format(now - incident.StartedAt),
                NextAction = next,
                Tone = incident.Severity >= Severity.Critical ? HealthState.Critical : HealthState.Degraded,
                Workspace = "incidents",
                FocusKind = FocusKind.Incident,
                Owner = incident.Commander
            }));
        }

        foreach (var c in _m.Cases.Where(c => c.Status is not (AlertStatus.Closed or AlertStatus.FalsePositive)))
        {
            double score = 1050 - (int)c.Severity * 100;
            var blocked = c.Tasks.FirstOrDefault(t => t.State == GateState.WaitingApproval);
            items.Add((score, new AttentionItem
            {
                Rank = 0,
                Kind = "SECURITY",
                Id = c.Id,
                Title = c.Title,
                Why = c.Hypothesis,
                Metric = c.AlertIds.Count.ToString(),
                MetricLabel = "alerts",
                Age = Design.AgoConverter.Format(now - c.OpenedAt),
                NextAction = blocked is not null
                    ? $"{blocked.Name} — waiting on {blocked.Owner}"
                    : "Continue scoping, then stage containment",
                Tone = HealthState.Critical,
                Workspace = "soc",
                FocusKind = FocusKind.Case,
                Owner = c.Owner
            }));
        }

        foreach (var change in _m.Changes.Where(c => c.State == ChangeState.AwaitingApproval))
        {
            var pending = change.Approvals.FirstOrDefault(a => a.State is GateState.Pending or GateState.WaitingApproval);
            double minutes = (change.WindowStart - now).TotalMinutes;
            items.Add((1120, new AttentionItem
            {
                Rank = 0,
                Kind = "CHANGE",
                Id = change.Id,
                Title = change.Title,
                Why = $"Correction for {change.LinkedIncidentId ?? "an open risk"}. "
                    + $"Blast radius covers {change.BlastRadiusServiceIds.Count} services with a {change.BackoutTime} backout.",
                Metric = minutes >= 0 ? $"{(int)minutes}m" : "open",
                MetricLabel = "to window",
                Age = Design.AgoConverter.Format(now - change.WindowStart.AddMinutes(-30)),
                NextAction = pending is not null
                    ? $"{pending.Board} approval outstanding — {pending.Approver}"
                    : "Schedule and execute",
                Tone = HealthState.Degraded,
                Workspace = "changes",
                FocusKind = FocusKind.Change,
                Owner = change.Implementer
            }));
        }

        foreach (var ticket in _m.Tickets.Where(t => t.SlaBreached(now) || (t.SlaRemaining(now).TotalMinutes is > 0 and < 45 && t.State != TicketState.Resolved)))
        {
            bool breached = ticket.SlaBreached(now);
            items.Add((breached ? 1300 : 1400, new AttentionItem
            {
                Rank = 0,
                Kind = breached ? "SLA BREACH" : "SLA RISK",
                Id = ticket.Id,
                Title = ticket.Subject,
                Why = $"{ticket.Requester}, {ticket.Department}. {(ticket.LinkedIncidentId is not null ? $"Inside the blast radius of {ticket.LinkedIncidentId}." : "No linked incident — this one is genuinely on its own.")}",
                Metric = Design.AgoConverter.Format(ticket.SlaRemaining(now)),
                MetricLabel = breached ? "past target" : "to target",
                Age = Design.AgoConverter.Format(now - ticket.OpenedAt),
                NextAction = ticket.Assignee == "Unassigned" ? "Assign an owner" : $"Owned by {ticket.Assignee} — update the requester",
                Tone = breached ? HealthState.Critical : HealthState.Degraded,
                Workspace = "tickets",
                FocusKind = FocusKind.Ticket,
                Owner = ticket.Assignee
            }));
        }

        // Unowned work is invisible work. Surface it explicitly rather than letting a queue hide it.
        int unassigned = _m.Tickets.Count(t => t.Assignee == "Unassigned" && t.State is TicketState.New or TicketState.Triage);
        if (unassigned > 0)
        {
            items.Add((1500, new AttentionItem
            {
                Rank = 0,
                Kind = "QUEUE",
                Id = "L1 QUEUE",
                Title = $"{unassigned} contacts in triage with no owner",
                Why = "Unowned contacts do not appear in anyone's personal queue and are the usual source of silent breaches.",
                Metric = unassigned.ToString(),
                MetricLabel = "waiting",
                Age = "—",
                NextAction = "Assign or attach to an existing incident",
                Tone = HealthState.Degraded,
                Workspace = "servicedesk",
                FocusKind = FocusKind.None
            }));
        }

        return items.OrderBy(i => i.Score)
                    .Select((t, i) => new AttentionItem
                    {
                        Rank = i + 1,
                        Kind = t.Item.Kind,
                        Id = t.Item.Id,
                        Title = t.Item.Title,
                        Why = t.Item.Why,
                        Metric = t.Item.Metric,
                        MetricLabel = t.Item.MetricLabel,
                        Age = t.Item.Age,
                        NextAction = t.Item.NextAction,
                        Tone = t.Item.Tone,
                        Workspace = t.Item.Workspace,
                        FocusKind = t.Item.FocusKind,
                        Owner = t.Item.Owner
                    })
                    .ToList();
    }

    private string NextChangeFor(Incident incident) =>
        _m.Changes.FirstOrDefault(c => c.LinkedIncidentId == incident.Id && c.State == ChangeState.AwaitingApproval)?.Id ?? "the correction";

    private List<LayerRow> BuildLayers()
    {
        var rows = new List<LayerRow>();
        foreach (EstateLayer layer in Enum.GetValues<EstateLayer>())
        {
            var rollup = _m.Rollup(layer);
            if (rollup.Total == 0) continue;
            var worst = _m.ByLayer(layer).Where(n => n.Health.IsBad()).OrderByDescending(n => n.Health.Weight()).FirstOrDefault();
            rows.Add(new LayerRow
            {
                Layer = layer,
                Name = LayerName(layer),
                Rollup = rollup,
                Detail = worst is not null ? worst.Name : $"{rollup.Total} elements nominal"
            });
        }
        return rows;
    }

    public static string LayerName(EstateLayer layer) => layer switch
    {
        EstateLayer.Foundation => "Foundation",
        EstateLayer.Network => "Network",
        EstateLayer.CoreServices => "Core Services",
        EstateLayer.Identity => "Identity & Trust",
        EstateLayer.Control => "Control",
        EstateLayer.Applications => "Applications",
        EstateLayer.Proof => "Proof",
        _ => layer.ToString()
    };

    private List<RiskItem> BuildRisks() => new()
    {
        new RiskItem
        {
            Subject = "Federation Service",
            Statement = "Signing correctly from a metadata cache it can no longer refresh. When the cache expires, every "
                      + "federated sign-in fails, not just the ones failing now.",
            Horizon = "41 min",
            Tone = HealthState.Critical,
            ServiceId = "idp-fed"
        },
        new RiskItem
        {
            Subject = "Distribution Switch — Building C",
            Statement = "Running on a single port-channel member after the flapping member was drained. No redundancy "
                      + "until the optic is replaced tonight.",
            Horizon = "9 h",
            Tone = HealthState.Degraded,
            ServiceId = "net-dist-c"
        },
        new RiskItem
        {
            Subject = "Partner API Gateway",
            Statement = "92% of this month's error budget consumed. A second event of this size breaches the contractual "
                      + "availability commitment.",
            Horizon = "This month",
            Tone = HealthState.Degraded,
            ServiceId = "app-api"
        },
        new RiskItem
        {
            Subject = "Change verification practice",
            Statement = "CHG-2291 recorded a conditional-forwarder check as not executed and was still closed as successful. "
                      + "The same gap exists in eleven other closed changes.",
            Horizon = "Systemic",
            Tone = HealthState.Degraded,
            ServiceId = "svc-dns"
        },
        new RiskItem
        {
            Subject = "Finance reporting client",
            Statement = "Still issues bearer refresh tokens with no device binding. The path exercised in CASE-118 remains "
                      + "open to any holder of a token.",
            Horizon = "Until CHG-2307",
            Tone = HealthState.Critical,
            ServiceId = "idp-fed"
        }
    };
}
