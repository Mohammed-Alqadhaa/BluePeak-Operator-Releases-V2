namespace BluePeak.Domain;

/// <summary>A time-series sample used by inline trend renderers.</summary>
public readonly record struct MetricPoint(DateTime At, double Value);

public sealed class MetricSeries
{
    public required string Key { get; init; }
    public required string Unit { get; init; }
    public double Warn { get; init; } = double.NaN;
    public double Breach { get; init; } = double.NaN;
    public List<MetricPoint> Points { get; init; } = new();

    public double Latest => Points.Count == 0 ? 0 : Points[^1].Value;
    public double Min => Points.Count == 0 ? 0 : Points.Min(p => p.Value);
    public double Max => Points.Count == 0 ? 0 : Points.Max(p => p.Value);
}

/// <summary>A managed element of the estate: service, device, platform or facility.</summary>
public sealed class ServiceNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required EstateLayer Layer { get; init; }
    public required string Kind { get; init; }
    public string Owner { get; init; } = "Unassigned";
    public string Location { get; init; } = "DC-Alpha";
    public int Tier { get; init; } = 2;
    public HealthState Health { get; set; } = HealthState.Healthy;
    public string StateReason { get; set; } = "";
    public double Availability { get; set; } = 99.99;
    public double LatencyMs { get; set; }
    public double ErrorRate { get; set; }
    public int OpenSignals { get; set; }
    public DateTime? DegradedSince { get; set; }
    public List<string> Tags { get; init; } = new();
    public Dictionary<string, MetricSeries> Metrics { get; init; } = new();

    /// <summary>Ids of nodes this node requires in order to function.</summary>
    public List<string> DependsOn { get; init; } = new();

    /// <summary>Display form of the layer, used wherever the estate is listed.</summary>
    public string LayerLabel => Layer switch
    {
        EstateLayer.Foundation => "Foundation",
        EstateLayer.Network => "Network",
        EstateLayer.CoreServices => "Core Services",
        EstateLayer.Identity => "Identity & Trust",
        EstateLayer.Control => "Control",
        EstateLayer.Applications => "Applications",
        EstateLayer.Proof => "Proof",
        _ => Layer.ToString()
    };

    public override string ToString() => Id + " " + Name;
}

public sealed class DependencyEdge
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required DependencyKind Kind { get; init; }
    public required string Protocol { get; init; }
    public string Port { get; init; } = "";
    public HealthState Health { get; set; } = HealthState.Healthy;
    public double LatencyMs { get; set; }
    public string Note { get; set; } = "";
    public bool IsCritical { get; init; } = true;
}

public sealed class TimelineEvent
{
    public required DateTime At { get; init; }
    public required string Actor { get; init; }
    public required string Text { get; init; }
    public string Channel { get; init; } = "system";
    public Severity Weight { get; init; } = Severity.Info;
    public string? EvidenceId { get; init; }
}

public sealed class Ticket
{
    public required string Id { get; init; }
    public required string Subject { get; init; }
    public required string Requester { get; init; }
    public required string Department { get; init; }
    public string Channel { get; init; } = "Portal";
    public Severity Priority { get; set; } = Severity.Medium;
    public TicketState State { get; set; } = TicketState.New;
    public string Assignee { get; set; } = "Unassigned";
    public string Queue { get; set; } = "Service Desk L1";
    public required DateTime OpenedAt { get; init; }
    public DateTime SlaDueAt { get; init; }
    public string? LinkedServiceId { get; set; }
    public string? LinkedIncidentId { get; set; }
    public string Summary { get; set; } = "";
    public List<string> SimilarTicketIds { get; init; } = new();
    public List<TimelineEvent> Timeline { get; init; } = new();
    public List<string> EvidenceIds { get; init; } = new();

    public TimeSpan SlaRemaining(DateTime now) => SlaDueAt - now;

    public bool SlaBreached(DateTime now) =>
        now > SlaDueAt && State is not (TicketState.Resolved or TicketState.Closed or TicketState.Verified);
}

public sealed class Incident
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public Severity Severity { get; set; } = Severity.High;
    public IncidentState State { get; set; } = IncidentState.Investigating;
    public string Commander { get; set; } = "Unassigned";
    public required DateTime StartedAt { get; init; }
    public DateTime? DetectedAt { get; init; }
    public DateTime? MitigatedAt { get; set; }
    public string Impact { get; set; } = "";
    public string? RootCauseServiceId { get; set; }
    public string? SuspectedChangeId { get; set; }
    public List<string> AffectedServiceIds { get; init; } = new();
    public List<string> LinkedTicketIds { get; init; } = new();
    public List<string> LinkedAlertIds { get; init; } = new();
    public List<string> EvidenceIds { get; init; } = new();
    public List<TimelineEvent> Timeline { get; init; } = new();
    public int UsersAffected { get; set; }
    public string Workstream { get; set; } = "Infrastructure";
}

public sealed class SecurityEntity
{
    public required string Id { get; init; }
    public required EntityKind Kind { get; init; }
    public required string Name { get; init; }
    public int RiskScore { get; set; }
    public string Context { get; set; } = "";
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
    public List<string> RelatedEntityIds { get; init; } = new();
    public bool IsManaged { get; init; } = true;
}

public sealed class SecurityAlert
{
    public required string Id { get; init; }
    public required string Rule { get; init; }
    public required Severity Severity { get; init; }
    public required DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
    public AlertStatus Status { get; set; } = AlertStatus.New;
    public string Assignee { get; set; } = "Unassigned";
    public int Confidence { get; set; } = 60;
    public string Tactic { get; init; } = "";
    public string Technique { get; init; } = "";
    public string DataSource { get; init; } = "";
    public string? CaseId { get; set; }
    public List<string> EntityIds { get; init; } = new();
    public string Detail { get; set; } = "";
    public int SignalCount { get; set; } = 1;
}

public sealed class SecurityCase
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public Severity Severity { get; set; } = Severity.High;
    public string Owner { get; set; } = "Unassigned";
    public AlertStatus Status { get; set; } = AlertStatus.Investigating;
    public required DateTime OpenedAt { get; init; }
    public string Hypothesis { get; set; } = "";
    public string Verdict { get; set; } = "Undetermined";
    public List<string> AlertIds { get; init; } = new();
    public List<string> EntityIds { get; init; } = new();
    public List<TimelineEvent> Timeline { get; init; } = new();
    public List<string> EvidenceIds { get; init; } = new();
    public List<ResponseTask> Tasks { get; init; } = new();
}

public sealed class ResponseTask
{
    public required string Name { get; init; }
    public required string Phase { get; init; }
    public GateState State { get; set; } = GateState.Pending;
    public string Owner { get; set; } = "Unassigned";
    public string Detail { get; set; } = "";
}

public sealed class ChangeRequest
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Type { get; init; }
    public ChangeRisk Risk { get; set; } = ChangeRisk.Moderate;
    public ChangeState State { get; set; } = ChangeState.Assessment;
    public required string Requester { get; init; }
    public string Implementer { get; set; } = "Unassigned";
    public required DateTime WindowStart { get; init; }
    public required DateTime WindowEnd { get; init; }
    public required string TargetServiceId { get; init; }
    public string Description { get; set; } = "";
    public string RollbackPlan { get; set; } = "";
    public string BackoutTime { get; set; } = "15 min";
    public List<string> BlastRadiusServiceIds { get; init; } = new();
    public List<Approval> Approvals { get; init; } = new();
    public List<VerificationCheck> Verification { get; init; } = new();
    public List<string> EvidenceIds { get; init; } = new();
    public string? LinkedIncidentId { get; set; }
}

public sealed class Approval
{
    public required string Board { get; init; }
    public required string Approver { get; init; }
    public GateState State { get; set; } = GateState.Pending;
    public DateTime? DecidedAt { get; set; }
    public string Comment { get; set; } = "";
}

public sealed class VerificationCheck
{
    public required string Name { get; init; }
    public required string Method { get; init; }
    public string Expected { get; init; } = "";
    public string Actual { get; set; } = "";
    public EvidenceResult Result { get; set; } = EvidenceResult.NotRun;
    public string? EvidenceId { get; set; }
}

public sealed class RunbookStep
{
    public required string Name { get; init; }
    public required string Gate { get; init; }
    public string Detail { get; init; } = "";
    public GateState State { get; set; } = GateState.Pending;
    public double EstimatedSeconds { get; init; } = 2;
    public string Output { get; set; } = "";
    public bool RequiresApproval { get; init; }
    public bool Mutating { get; init; }
}

public sealed class Runbook
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public string Purpose { get; set; } = "";
    public string Owner { get; set; } = "Platform Operations";
    public ChangeRisk Risk { get; set; } = ChangeRisk.Low;
    public bool RequiresChange { get; init; }
    public DateTime? LastRunAt { get; set; }
    public string LastRunResult { get; set; } = "Never run";
    public int RunCount { get; set; }
    public List<RunbookStep> Steps { get; init; } = new();
    public List<string> TargetServiceIds { get; init; } = new();
    public string? SuggestedForIncidentId { get; set; }
}

public sealed class EvidenceRecord
{
    public required string Id { get; init; }
    public required string Claim { get; init; }
    public required string Source { get; init; }
    public required string Check { get; init; }
    public EvidenceResult Result { get; set; } = EvidenceResult.NotRun;
    public required DateTime CapturedAt { get; init; }
    public EvidenceAuthority Authority { get; set; } = EvidenceAuthority.LocalOperator;
    public string Observed { get; set; } = "";
    public string Expected { get; set; } = "";
    public string Digest { get; set; } = "";
    public string Scope { get; set; } = "Session";
    public bool Preserved { get; set; }
    public string? SubjectId { get; set; }
    public string Collector { get; set; } = "operator";
}

/// <summary>One hop in a reasoned diagnostic path from request to first failure.</summary>
public sealed class DiagnosticHop
{
    public required int Index { get; init; }
    public required string ServiceId { get; init; }
    public required string Label { get; init; }
    public required string Protocol { get; init; }
    public string Operation { get; init; } = "";
    public string Expected { get; init; } = "";
    public string Actual { get; init; } = "";
    public HealthState Result { get; init; } = HealthState.Healthy;
    public double ElapsedMs { get; init; }
    public bool IsFirstFailure { get; init; }
    public bool IsDownstreamConsequence { get; init; }
    public string? EvidenceId { get; init; }
    public string Reasoning { get; init; } = "";
}

public sealed class DiagnosticPath
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Request { get; init; }
    public string Origin { get; init; } = "";
    public required DateTime RunAt { get; init; }
    public List<DiagnosticHop> Hops { get; init; } = new();
    public string Conclusion { get; set; } = "";
    public string? FirstFailureServiceId { get; set; }
    public List<string> BlastRadiusServiceIds { get; init; } = new();
    public string? LinkedIncidentId { get; set; }
    public string? JourneyId { get; set; }
}
