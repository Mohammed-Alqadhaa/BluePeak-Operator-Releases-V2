using BluePeak.Domain;

namespace BluePeak.Simulation;

/// <summary>
/// The physical metaphor: the estate rendered as one engineered machine. A central spine
/// carries four stacked rings of chassis wedges. Every wedge is a functional subsystem and
/// every wedge docks to the spine through a visible connector bus.
/// </summary>
public static class OperationsCore
{
    public const string SpineId = "spine";

    public const double RingPlinth = -1.72;
    public const double RingAccess = -0.52;
    public const double RingCore = 0.74;
    public const double RingControl = 2.00;
    public const double RingCrown = 3.16;

    public static IReadOnlyList<SceneModule> Modules { get; } = Build();

    private static readonly Dictionary<string, SceneModule> ById =
        Modules.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

    public static SceneModule? Module(string? id) =>
        id is not null && ById.TryGetValue(id, out var m) ? m : null;

    public static IEnumerable<SceneModule> Ring(int ring) => Modules.Where(m => m.Ring == ring);

    private static IReadOnlyList<SceneModule> Build() => new List<SceneModule>
    {
        // ---- Ring 1 · Access and transport -------------------------------------------------
        new()
        {
            Id = "mod.ingress", Code = "IGR", Name = "Request Ingress",
            Role = "Client and partner requests enter the machine here. Terminates transport security and applies admission policy.",
            Mechanism = MechanismKind.PortArray, Layer = EstateLayer.Network,
            Ring = 1, Azimuth = 90, Height = RingAccess, ServiceId = "net-edge"
        },
        new()
        {
            Id = "mod.switching", Code = "SWF", Name = "Switching Fabric",
            Role = "Moves frames between attached segments. Port-channel members share load; a flapping member costs sessions, not throughput.",
            Mechanism = MechanismKind.SwitchLattice, Layer = EstateLayer.Network,
            Ring = 1, Azimuth = 210, Height = RingAccess, ServiceId = "net-core"
        },
        new()
        {
            Id = "mod.routing", Code = "RTG", Name = "Routing and Delivery",
            Role = "Selects the path and the pool member. Health monitors here decide which application instance receives work.",
            Mechanism = MechanismKind.RoutingPrism, Layer = EstateLayer.Network,
            Ring = 1, Azimuth = 330, Height = RingAccess, ServiceId = "net-lb"
        },

        // ---- Ring 2 · Core services, identity, workload ------------------------------------
        new()
        {
            Id = "mod.resolution", Code = "DNS", Name = "Name Resolution",
            Role = "Turns names into addresses. Nothing above this ring can reach anything it cannot first resolve.",
            Mechanism = MechanismKind.IndexDrum, Layer = EstateLayer.CoreServices,
            Ring = 2, Azimuth = 150, Height = RingCore, ServiceId = "svc-dns"
        },
        new()
        {
            Id = "mod.identity", Code = "IDT", Name = "Identity and Trust",
            Role = "Issues and validates assertions. Holds signing material behind a keyed collar and refreshes it on a clock.",
            Mechanism = MechanismKind.TrustVault, Layer = EstateLayer.Identity,
            Ring = 2, Azimuth = 270, Height = RingCore, ServiceId = "idp-fed"
        },
        new()
        {
            Id = "mod.workload", Code = "APP", Name = "Application Workload",
            Role = "Where business requests are actually served. Stacked instances behind one address.",
            Mechanism = MechanismKind.ServiceStack, Layer = EstateLayer.Applications,
            Ring = 2, Azimuth = 30, Height = RingCore, ServiceId = "app-api"
        },

        // ---- Ring 3 · Control, inspection, action ------------------------------------------
        new()
        {
            Id = "mod.control", Code = "CTL", Name = "Observation and Control",
            Role = "Samples every ring below it. Produces the signal that starts an investigation and the proof that ends it.",
            Mechanism = MechanismKind.SensorDome, Layer = EstateLayer.Control,
            Ring = 3, Azimuth = 90, Height = RingControl, ServiceId = "ctl-telemetry"
        },
        new()
        {
            Id = "mod.inspection", Code = "SOC", Name = "Security Inspection",
            Role = "Examines identity, session and content behaviour against detection logic. Correlates signals onto shared entities.",
            Mechanism = MechanismKind.InspectionAperture, Layer = EstateLayer.Control,
            Ring = 3, Azimuth = 210, Height = RingControl, ServiceId = "ctl-siem"
        },
        new()
        {
            Id = "mod.automation", Code = "AUT", Name = "Gated Automation",
            Role = "The only part of the machine permitted to change state, and only after policy, pre-check, simulation and approval.",
            Mechanism = MechanismKind.Actuator, Layer = EstateLayer.Control,
            Ring = 3, Azimuth = 330, Height = RingControl, ServiceId = "ctl-automation"
        },

        // ---- Ring 0 · Foundation plinth ----------------------------------------------------
        new()
        {
            Id = "mod.foundation", Code = "FND", Name = "Foundation Platform",
            Role = "Facility, compute and storage. Everything above stands on it and nothing above it can outlive its failure.",
            Mechanism = MechanismKind.ServiceStack, Layer = EstateLayer.Foundation,
            Ring = 0, Azimuth = 0, Height = RingPlinth, ServiceId = "fnd-compute",
            Sweep = 360, InnerRadius = 0.0, OuterRadius = 2.16, Thickness = 0.46
        },

        // ---- Ring 4 · Evidence crown -------------------------------------------------------
        new()
        {
            Id = "mod.evidence", Code = "EVD", Name = "Evidence Vault",
            Role = "Seals what was claimed, what was checked and what was observed. Local findings never silently become authoritative.",
            Mechanism = MechanismKind.ArchiveVault, Layer = EstateLayer.Proof,
            Ring = 4, Azimuth = 0, Height = RingCrown, ServiceId = "prf-ledger",
            Sweep = 360, InnerRadius = 0.0, OuterRadius = 0.92, Thickness = 0.72
        }
    };
}
