using BluePeak.App.Design;
using BluePeak.Domain;

namespace BluePeak.App.Workspaces;

/// <summary>
/// The triage question, answered with reasons. First line does not need a list of tickets; it
/// needs to know whether this contact already has a cause somebody else owns.
/// </summary>
public sealed class TriageAssessment
{
    public required Ticket Ticket { get; init; }
    public Incident? Match { get; init; }
    public int Confidence { get; init; }
    public required IReadOnlyList<TriageReason> Reasons { get; init; }
    public required IReadOnlyList<Ticket> Similar { get; init; }
    public required string Recommendation { get; init; }
    public required string RecommendationDetail { get; init; }
    public required HealthState Tone { get; init; }
    public required string SuggestedPriority { get; init; }
    public required string SuggestedClassification { get; init; }
    public required string RequesterUpdate { get; init; }

    public static TriageAssessment For(EstateModel model, Ticket ticket)
    {
        var reasons = new List<TriageReason>();
        Incident? best = null;
        int score = 0;

        foreach (var incident in model.Incidents.Where(i => i.State != IncidentState.Resolved))
        {
            int local = 0;
            var local_reasons = new List<TriageReason>();

            if (ticket.LinkedServiceId is not null && incident.AffectedServiceIds.Contains(ticket.LinkedServiceId))
            {
                local += 45;
                local_reasons.Add(new TriageReason(
                    "Shared service",
                    $"{model.NameOf(ticket.LinkedServiceId)} is inside the declared blast radius of {incident.Id}.",
                    true));
            }

            if (ticket.LinkedServiceId is not null && incident.RootCauseServiceId is not null)
            {
                var closure = model.DependencyClosure(ticket.LinkedServiceId);
                if (closure.Any(n => n.Id == incident.RootCauseServiceId))
                {
                    local += 30;
                    local_reasons.Add(new TriageReason(
                        "Shared dependency",
                        $"The reported service depends on {model.NameOf(incident.RootCauseServiceId)}, "
                        + $"which is the first failing component in {incident.Id}.",
                        true));
                }
            }

            var window = ticket.OpenedAt - incident.StartedAt;
            if (window.TotalMinutes is > -5 and < 240)
            {
                local += 15;
                local_reasons.Add(new TriageReason(
                    "Time correlation",
                    $"Raised {AgoConverter.Format(window)} after {incident.Id} began.",
                    true));
            }

            int siblings = model.Tickets.Count(t =>
                t.Id != ticket.Id && t.LinkedIncidentId == incident.Id &&
                Math.Abs((t.OpenedAt - ticket.OpenedAt).TotalMinutes) < 180);
            if (siblings > 0)
            {
                local += Math.Min(20, siblings * 7);
                local_reasons.Add(new TriageReason(
                    "Contact cluster",
                    $"{siblings} other contacts in the same window are already attached to {incident.Id}.",
                    true));
            }

            if (local > score)
            {
                score = local;
                best = incident;
                reasons = local_reasons;
            }
        }

        var similar = model.Tickets
            .Where(t => t.Id != ticket.Id &&
                        (ticket.SimilarTicketIds.Contains(t.Id) ||
                         (ticket.LinkedServiceId is not null && t.LinkedServiceId == ticket.LinkedServiceId)))
            .OrderByDescending(t => t.OpenedAt)
            .Take(4)
            .ToList();

        if (best is null || score < 40)
        {
            reasons.Add(new TriageReason(
                "No shared cause found",
                "No open incident covers the reported service or its dependencies. Treat this as an "
                + "independent fault and diagnose it.",
                false));
        }

        bool attach = best is not null && score >= 40;
        return new TriageAssessment
        {
            Ticket = ticket,
            Match = attach ? best : null,
            Confidence = Math.Min(98, score),
            Reasons = reasons,
            Similar = similar,
            Tone = attach ? HealthState.Degraded : HealthState.Healthy,
            Recommendation = attach
                ? $"Attach to {best!.Id}"
                : "Treat as a new fault",
            RecommendationDetail = attach
                ? "This contact is impact evidence for an incident that already has a commander and a correction in "
                + "flight. Diagnosing it again would duplicate work and delay the requester's update."
                : "Nothing open explains this symptom. Classify it, assign an owner, and begin diagnosis at first line.",
            SuggestedPriority = attach && best!.Severity >= Severity.Critical ? "High — inside a critical incident"
                : ticket.Priority.ToString(),
            SuggestedClassification = ticket.LinkedServiceId is not null
                ? $"Availability / {model.Node(ticket.LinkedServiceId)?.LayerLabel}"
                : "Unclassified — needs a service",
            RequesterUpdate = attach
                ? "A shared component used by this service is failing. It is being corrected under an emergency "
                + "change. No action is needed on your device, and you will be updated when the fix is verified."
                : "We have logged your report and are investigating. We will contact you once we have identified "
                + "the cause or need more information from you."
        };
    }
}

public sealed record TriageReason(string Label, string Detail, bool Supports);
