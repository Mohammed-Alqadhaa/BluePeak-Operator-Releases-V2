using BluePeak.Domain;

namespace BluePeak.Simulation.Journeys;

internal static class NetworkJourney
{
    public static Journey Create() => new()
    {
        Id = "journey.network",
        Name = "Network path failure",
        Discipline = "NOC",
        Question = "Sessions drop every few minutes in one building. Throughput looks fine. What is wrong?",
        Summary = "A degraded link that still forwards traffic is worse than one that is down, because redundancy hides it "
                + "from every availability metric. This journey opens the switching fabric to the individual bundle member, "
                + "shows why an average conceals the fault, and drains the member under gated automation.",
        IncidentId = "INC-4415",
        DiagnosticPathId = "DX-2205",
        ChangeId = "CHG-2298",
        RunbookId = "RB-007",
        Weight = Severity.High,
        ModulePath = new[] { "mod.ingress", "mod.switching", "mod.routing", "mod.workload", "mod.automation", "mod.control" },
        Stages = new[]
        {
            B.Stage("s0", "A fault that averages away", StageKind.Establish, 4.2,
                "Every dashboard for this path is green. Utilisation, throughput and availability are all within objective, and users are still losing their desktops.",
                B.Wide(196, 15, 10.4f, 0.35f),
                links: new[]
                {
                    B.Bus("mod.switching", "fabric", HealthState.Degraded, 0.8f, 1.2f),
                    B.Bus("mod.workload", "sessions", HealthState.Degraded, 0.7f, 0.7f)
                },
                detail: new[]
                {
                    B.Row("Symptom", "Virtual desktop sessions dropping every 4 to 9 minutes, Building C only", HealthState.Degraded),
                    B.Row("Affected", "Approximately 180 users on one floor"),
                    B.Row("Link utilisation", "34% — well within objective", HealthState.Healthy),
                    B.Row("Interface availability", "100% — the bundle never went down", HealthState.Healthy),
                    B.Row("Why the metrics lie", "The bundle stayed up because the other member carried it. The average has no idea a member is failing.", HealthState.Degraded)
                }),

            B.Stage("s1", "Open the transport ring", StageKind.Disassemble, 3.4,
                "The access ring disengages. Switching sits between the endpoint and everything else, so it is inspected before anything above it.",
                B.Wide(210, 22, 11.6f, 0.3f, 40),
                poses: B.Bloom(0.6f, 0.5f, 0.24f,
                    ("mod.switching", ModulePose.Extracted(1.0f, 0.4f, 0, 0, 0.4f, 1f)),
                    ("mod.routing", ModulePose.Extracted(1.0f, 0.4f, 0, 0, 0.4f, 0.85f))),
                links: new[]
                {
                    B.Dep("mod.ingress", "mod.switching", "campus attachment", HealthState.Healthy, 0.8f, 1.2f),
                    B.Bus("mod.switching", "fabric", HealthState.Degraded, 0.9f, 1.1f)
                },
                detail: new[]
                {
                    B.Row("Scope", "Access attachment, uplink bundle, core transit"),
                    B.Row("Excluded early", "Identity — Kerberos ticket issue times are nominal", HealthState.Healthy),
                    B.Row("Excluded early", "Desktop platform — sessions in other buildings are stable", HealthState.Healthy),
                    B.Row("Method", "Descend from the aggregate to the individual member")
                }),

            B.Stage("s2", "Attachment is clean", StageKind.Inspect, 3.8,
                "The endpoint port is up, in the right VLAN, with no errors. The fault is not where the user is.",
                B.Look("mod.ingress", 1.45f, 6.2f, 10),
                poses: B.Focus(0.2f, ("mod.ingress", ModulePose.Extracted(1.45f, 1f, -16, 6))),
                links: new[] { B.Bus("mod.ingress", "port up", HealthState.Healthy, 1f, 1.4f) },
                detail: new[]
                {
                    B.Row("Protocol", "802.1Q, 802.1X"),
                    B.Row("Expected", "Interface up, VLAN 240, no input errors", HealthState.Healthy),
                    B.Row("Actual", "Interface up, VLAN 240, 0 input errors over 24 hours", HealthState.Healthy),
                    B.Row("Ruled out", "Cabling, endpoint NIC, access port configuration", HealthState.Healthy)
                },
                focus: "mod.ingress", verdict: HealthState.Healthy, service: "net-dist-c"),

            B.Stage("s3", "Open the lattice", StageKind.Inspect, 5.6,
                "The switching wedge opens and the bundle is shown as its individual members rather than as one aggregate. One member is transitioning.",
                B.Look("mod.switching", 2.1f, 5.4f, 7, -24, 31),
                poses: B.Focus(0.1f, ("mod.switching", ModulePose.Extracted(2.1f, 1f, 28, -6, 0.15f))),
                links: new[] { B.Bus("mod.switching", "Po1 members", HealthState.Degraded, 1f, 0.8f) },
                detail: new[]
                {
                    B.Row("Protocol", "LACP port-channel, 2 x 10G members"),
                    B.Row("Dependency", "Distribution switch Building C — net-dist-c"),
                    B.Row("Expected", "Both members forwarding, zero link transitions", HealthState.Healthy),
                    B.Row("Actual", "Gi1/0/47 recorded 214 transitions in 30 minutes; Gi1/0/48 recorded 0", HealthState.Critical),
                    B.Row("Aggregate view", "Bundle up, 100% availability — which is why nothing alerted", HealthState.Degraded),
                    B.Row("Evidence", "EV-1010 — interface counters, platform attested"),
                    B.Row("Verdict", "First failure. A single bundle member, not the bundle.", HealthState.Critical)
                },
                focus: "mod.switching", verdict: HealthState.Critical, evidence: "EV-1010", service: "net-dist-c"),

            B.Stage("s4", "Why a working link breaks sessions", StageKind.Diagnose, 5.2,
                "Flows are hashed onto members and stay there. Half the sessions live on a member that disappears every few minutes.",
                B.Between("mod.switching", "mod.routing", 8.0f, 16, 38),
                poses: B.Focus(0.14f,
                    ("mod.switching", ModulePose.Extracted(1.7f, 1f, 22, -5, 0.1f)),
                    ("mod.routing", ModulePose.Extracted(1.4f, 0.8f, -14, 4, 0f, 0.9f))),
                links: new[]
                {
                    B.Dep("mod.switching", "mod.routing", "hashed flows", HealthState.Critical, 1f, 0.8f),
                    B.Bus("mod.switching", "member down", HealthState.Critical, 0.9f, 0.2f)
                },
                detail: new[]
                {
                    B.Row("Mechanism", "Load balancing is per flow, not per packet. A flow pinned to a failing member dies with it."),
                    B.Row("Expected", "Session survives a member transition through re-hashing", HealthState.Healthy),
                    B.Row("Actual", "Long-lived session tears down and must be re-established", HealthState.Critical),
                    B.Row("Why 50%", "Two members, even hash distribution — roughly half of sessions are exposed"),
                    B.Row("Why it looks intermittent", "Only sessions crossing during a transition are affected"),
                    B.Row("Physical cause", "Optic on Gi1/0/47 degrading; receive power drifting below threshold", HealthState.Critical)
                },
                verdict: HealthState.Critical, service: "net-dist-c"),

            B.Stage("s5", "Downstream consequence", StageKind.Trace, 4.0,
                "The desktop platform is healthy. It is being blamed for a transport fault two layers beneath it.",
                B.Look("mod.workload", 1.5f, 6.4f, 9),
                poses: B.Focus(0.16f,
                    ("mod.workload", ModulePose.Extracted(1.5f, 1f, -18, 4)),
                    ("mod.switching", ModulePose.Extracted(1.2f, 0.6f, 16, -3, 0f, 0.6f))),
                links: new[]
                {
                    B.Dep("mod.workload", "mod.switching", "session transport", HealthState.Critical, 0.95f, 0.4f),
                    B.Bus("mod.workload", "broker healthy", HealthState.Healthy, 0.8f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Dependency", "Virtual desktop service — app-vdi"),
                    B.Row("Broker state", "Healthy, no capacity pressure, no failed logons", HealthState.Healthy),
                    B.Row("Observed", "Session disconnects with transport reset, not authentication failure", HealthState.Degraded),
                    B.Row("Misdirection risk", "Desktop team would find nothing and return the ticket, losing another 90 minutes", HealthState.Degraded),
                    B.Row("Correct owner", "Network Engineering")
                },
                focus: "mod.workload", verdict: HealthState.Degraded, service: "app-vdi"),

            B.Stage("s6", "Drain before replace", StageKind.Act, 5.4,
                "The actuator arms for a drain. The pre-check refuses to proceed if removing the member would isolate anything.",
                B.Look("mod.automation", 1.85f, 6.0f, 13, -20, 33),
                poses: B.Focus(0.14f,
                    ("mod.automation", ModulePose.Extracted(1.85f, 1f, -22, 6)),
                    ("mod.switching", ModulePose.Extracted(1.2f, 0.6f, 16, -3, 0f, 0.65f))),
                links: new[]
                {
                    B.Dep("mod.automation", "mod.switching", "shut Gi1/0/47", HealthState.Degraded, 1f, 1.1f),
                    B.Bus("mod.automation", "gated", HealthState.Degraded, 0.9f, 1f)
                },
                detail: new[]
                {
                    B.Row("Proposed action", "RB-007 — administratively shut the flapping bundle member"),
                    B.Row("Policy", "Network change role held; device is not the last path to any tier-1 service", HealthState.Healthy),
                    B.Row("Pre-check", "Surviving member carries current load at 68% with headroom", HealthState.Healthy),
                    B.Row("Pre-check", "Redundancy assertion — no downstream node becomes isolated", HealthState.Healthy),
                    B.Row("Simulation", "Post-drain utilisation modelled at 68%, peak 79%", HealthState.Healthy),
                    B.Row("Accepted cost", "Redundancy is lost until the optic is replaced under CHG-2298", HealthState.Degraded),
                    B.Row("Authorisation", "Approved — incident commander and change board")
                },
                focus: "mod.automation", verdict: HealthState.Degraded, service: "ctl-automation"),

            B.Stage("s7", "Watch the counter, not the dashboard", StageKind.Verify, 4.8,
                "Verification watches the exact counter that exposed the fault, for long enough to mean something.",
                B.Between("mod.control", "mod.switching", 8.6f, 20, 40),
                poses: B.Focus(0.2f,
                    ("mod.control", ModulePose.Extracted(1.5f, 1f, -16, 8)),
                    ("mod.switching", ModulePose.Extracted(0.9f, 0.4f, 8, -2, 0f, 0.85f)),
                    ("mod.workload", ModulePose.Extracted(0.85f, 0.4f, 0, 0, 0f, 0.8f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.switching", "transition counter", HealthState.Healthy, 1f, 1.8f),
                    B.Data("mod.control", "mod.workload", "session stability", HealthState.Healthy, 1f, 1.6f)
                },
                detail: new[]
                {
                    B.Row("Check 1", "Zero link transitions over a 60 minute watch window", HealthState.Healthy),
                    B.Row("Check 2", "Desktop disconnect rate returns to the 7-day baseline", HealthState.Healthy),
                    B.Row("Check 3", "Surviving member utilisation stays below 80% through peak", HealthState.Healthy),
                    B.Row("Open risk", "Single member until CHG-2298 replaces the optic in the maintenance window", HealthState.Degraded),
                    B.Row("Monitoring added", "Per-member transition rate alert — the gap that let this run for two hours")
                },
                focus: "mod.control", verdict: HealthState.Healthy),

            B.Stage("s8", "Seal and close the ring", StageKind.Reassemble, 4.8,
                "The lattice folds back into the wedge, the access ring seats, and the estate carries the residual risk knowingly rather than silently.",
                B.Wide(196, 15, 10.4f, 0.35f),
                links: new[]
                {
                    B.Bus("mod.switching", "fabric", HealthState.Degraded, 0.8f, 1.4f),
                    B.Bus("mod.workload", "sessions", HealthState.Healthy, 0.8f, 1.4f),
                    B.Data("mod.control", "mod.evidence", "attest", HealthState.Healthy, 0.7f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Outcome", "Session disconnects stopped; cause identified and isolated", HealthState.Healthy),
                    B.Row("State", "Running on a single bundle member by design", HealthState.Degraded),
                    B.Row("Scheduled", "CHG-2298 optic replacement, tonight's window"),
                    B.Row("Evidence", "EV-1010 counters before and after, sealed against INC-4415"),
                    B.Row("Lesson recorded", "Aggregate availability is not a substitute for per-member health", HealthState.Degraded)
                },
                verdict: HealthState.Healthy)
        }
    };
}
