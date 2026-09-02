namespace BluePeak.Domain;

/// <summary>Operational health of any observable element.</summary>
public enum HealthState
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Critical = 3,
    Offline = 4,
    Maintenance = 5
}

public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>Architectural tiers of the estate, ordered foundation-upward.</summary>
public enum EstateLayer
{
    Foundation = 0,
    Network = 1,
    CoreServices = 2,
    Identity = 3,
    Control = 4,
    Applications = 5,
    Proof = 6
}

public enum DependencyKind
{
    /// <summary>Caller blocks on callee. Failure propagates immediately.</summary>
    Synchronous,
    /// <summary>Queued / buffered. Failure propagates with delay.</summary>
    Asynchronous,
    /// <summary>Trust or authorisation relationship.</summary>
    Trust,
    /// <summary>Data or replication relationship.</summary>
    Data,
    /// <summary>Physical or facility hosting relationship.</summary>
    Hosting
}

public enum TicketState { New, Triage, InProgress, Pending, Escalated, Resolved, Verified, Closed }

public enum IncidentState { Detected, Investigating, Identified, Mitigating, Monitoring, Resolved }

public enum AlertStatus { New, Triaged, Investigating, Contained, Closed, FalsePositive }

public enum ChangeState { Draft, Assessment, AwaitingApproval, Approved, Scheduled, Implementing, Verification, Completed, RolledBack, Rejected }

public enum ChangeRisk { Low, Moderate, High, Critical }

public enum EntityKind { User, Host, IpAddress, Domain, Process, File, Account, Token, Mailbox }

public enum EvidenceAuthority
{
    /// <summary>Produced by this workstation. Never authoritative outside the local session.</summary>
    LocalOperator,
    /// <summary>Countersigned by a platform control plane.</summary>
    PlatformAttested,
    /// <summary>Accepted into the immutable project record.</summary>
    ProjectAuthoritative
}

public enum EvidenceResult { Pass, Fail, Inconclusive, NotRun }

public enum GateState { Pending, Running, Passed, Failed, Blocked, Skipped, WaitingApproval }

public static class HealthRank
{
    /// <summary>Ordering weight used to sort worst-first across the product.</summary>
    public static int Weight(this HealthState h) => h switch
    {
        HealthState.Critical => 5,
        HealthState.Offline => 4,
        HealthState.Degraded => 3,
        HealthState.Unknown => 2,
        HealthState.Maintenance => 1,
        _ => 0
    };

    public static bool IsBad(this HealthState h) =>
        h is HealthState.Critical or HealthState.Degraded or HealthState.Offline;

    public static string Label(this HealthState h) => h switch
    {
        HealthState.Healthy => "Healthy",
        HealthState.Degraded => "Degraded",
        HealthState.Critical => "Critical",
        HealthState.Offline => "Offline",
        HealthState.Maintenance => "Maintenance",
        _ => "Unknown"
    };
}
