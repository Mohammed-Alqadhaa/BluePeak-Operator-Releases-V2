using BluePeak.Domain;

namespace BluePeak.Simulation.Journeys;

internal static class DnsJourney
{
    public static Journey Create() => new()
    {
        Id = "journey.dns",
        Name = "Name resolution failure",
        Discipline = "NOC",
        Question = "Every authenticated path failed at once. Which component actually broke?",
        Summary = "A change removed a conditional forwarder zone. The symptom appears at the partner API, "
                + "but the first failure is two layers below it. This journey walks the request until expected "
                + "and actual diverge, then proves that everything above the divergence is consequence.",
        IncidentId = "INC-4412",
        DiagnosticPathId = "DX-2201",
        ChangeId = "CHG-2291",
        RunbookId = "RB-014",
        Weight = Severity.Critical,
        ModulePath = new[] { "mod.ingress", "mod.routing", "mod.workload", "mod.resolution", "mod.identity", "mod.automation", "mod.evidence" },
        Stages = new[]
        {
            B.Stage("s0", "Sealed system", StageKind.Establish, 4.2,
                "The estate as the operator finds it: one machine, eleven subsystems, everything reporting through the control ring.",
                B.Wide(38, 17, 10.6f),
                links: new[]
                {
                    B.Bus("mod.control", "telemetry", HealthState.Healthy, 0.55f),
                    B.Bus("mod.workload", "service", HealthState.Critical, 0.9f),
                    B.Bus("mod.resolution", "resolution", HealthState.Critical, 0.9f)
                },
                detail: new[]
                {
                    B.Row("Reported symptom", "Partner API returning HTTP 503 on every authenticated endpoint", HealthState.Critical),
                    B.Row("Reported by", "Integration monitoring, then four separate human contacts"),
                    B.Row("Elapsed", "74 minutes since first signal"),
                    B.Row("Naive conclusion", "The API is broken", HealthState.Degraded),
                    B.Row("What we will test", "Whether the API is the first thing to fail, or the first thing to complain")
                }),

            B.Stage("s1", "Chassis disengage", StageKind.Disassemble, 3.4,
                "Retaining collars release and every ring stands off its seat. The connector buses that were hidden inside the stack become visible.",
                B.Wide(58, 24, 12.4f, 0.7f, 40),
                poses: B.Bloom(0.62f, 0.5f),
                links: new[]
                {
                    B.Bus("mod.ingress", "admission", HealthState.Healthy, 0.7f),
                    B.Bus("mod.routing", "delivery", HealthState.Healthy, 0.7f),
                    B.Bus("mod.workload", "service", HealthState.Critical, 0.9f),
                    B.Bus("mod.resolution", "resolution", HealthState.Critical, 0.95f),
                    B.Bus("mod.identity", "trust", HealthState.Degraded, 0.8f),
                    B.Bus("mod.control", "telemetry", HealthState.Healthy, 0.6f)
                },
                detail: new[]
                {
                    B.Row("Method", "Walk the request path in order and compare expected against actual at every hop"),
                    B.Row("Why in order", "The first divergence is the cause. Everything after it is a consequence and must not be acted on."),
                    B.Row("Path length", "6 hops from perimeter to partner response"),
                    B.Row("Evidence policy", "Each hop records its own observation before the walk continues")
                }),

            B.Stage("s2", "Hop 1 · Request ingress", StageKind.Inspect, 4.0,
                "Transport security completes and admission policy allows the request. The perimeter is not implicated.",
                B.Look("mod.ingress", 1.5f, 6.4f, 10),
                poses: B.Focus(0.2f, ("mod.ingress", ModulePose.Extracted(1.5f, 1f, -16, 6))),
                links: new[] { B.Bus("mod.ingress", "TLS 1.3 admit", HealthState.Healthy, 1f, 1.4f) },
                detail: new[]
                {
                    B.Row("Protocol", "TLS 1.3 over TCP 443"),
                    B.Row("Dependency", "Perimeter firewall pair — net-edge"),
                    B.Row("Expected", "Session established, policy verdict allow", HealthState.Healthy),
                    B.Row("Actual", "Session established in 34 ms, policy verdict allow", HealthState.Healthy),
                    B.Row("Evidence", "Edge session log, 412 matching sessions in the window"),
                    B.Row("Impact if this failed", "Total loss of external reachability — not what we observe")
                },
                focus: "mod.ingress", verdict: HealthState.Healthy, service: "net-edge"),

            B.Stage("s3", "Hop 2 · Routing and delivery", StageKind.Inspect, 3.8,
                "A healthy pool member is selected. Capacity and health monitoring are both nominal, so this is not a delivery problem.",
                B.Look("mod.routing", 1.5f, 6.4f, 10),
                poses: B.Focus(0.2f, ("mod.routing", ModulePose.Extracted(1.5f, 1f, -14, 5))),
                links: new[]
                {
                    B.Bus("mod.routing", "pool select", HealthState.Healthy, 1f, 1.4f),
                    B.Dep("mod.ingress", "mod.routing", "forward", HealthState.Healthy, 0.7f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Protocol", "HTTPS to virtual server, HTTP/2 to pool"),
                    B.Row("Dependency", "Application delivery tier — net-lb"),
                    B.Row("Expected", "Healthy member selected from the pool", HealthState.Healthy),
                    B.Row("Actual", "Member api-02 selected, health monitor green, 3 ms", HealthState.Healthy),
                    B.Row("Evidence", "Delivery tier decision log"),
                    B.Row("Impact if this failed", "5xx with no backend contact — the access log would show no upstream attempt")
                },
                focus: "mod.routing", verdict: HealthState.Healthy, service: "net-lb"),

            B.Stage("s4", "Hop 3 · Application workload", StageKind.Inspect, 4.4,
                "The gateway accepts the request and starts token introspection. The process is healthy; it is about to be blocked by something it depends on.",
                B.Look("mod.workload", 1.6f, 6.2f, 9),
                poses: B.Focus(0.2f, ("mod.workload", ModulePose.Extracted(1.6f, 1f, -18, 4))),
                links: new[]
                {
                    B.Bus("mod.workload", "request handling", HealthState.Healthy, 1f, 1.4f),
                    B.Dep("mod.routing", "mod.workload", "dispatch", HealthState.Healthy, 0.7f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Protocol", "HTTP/2"),
                    B.Row("Dependency", "Partner API Gateway — app-api"),
                    B.Row("Expected", "Route matched, introspection initiated", HealthState.Healthy),
                    B.Row("Actual", "Route matched, introspection initiated, 2 ms", HealthState.Healthy),
                    B.Row("Process state", "All 6 instances running, no restarts, memory nominal", HealthState.Healthy),
                    B.Row("Reading", "The component reporting the error is working correctly. Restarting it would change nothing.", HealthState.Degraded)
                },
                focus: "mod.workload", verdict: HealthState.Healthy, service: "app-api"),

            B.Stage("s5", "Following the dependency down", StageKind.Trace, 3.2,
                "Introspection needs an address before it can need a token. The path drops from the application ring into core services.",
                B.Between("mod.workload", "mod.resolution", 8.6f, 20, 40),
                poses: B.Focus(0.16f,
                    ("mod.workload", ModulePose.Extracted(1.35f, 0.9f, -18, 4, 0.15f, 0.85f)),
                    ("mod.resolution", ModulePose.Extracted(1.35f, 0.9f, 14, -4, -0.15f))),
                links: new[]
                {
                    B.Dep("mod.workload", "mod.resolution", "resolve federation host", HealthState.Critical, 1f, 2.2f),
                    B.Bus("mod.resolution", "resolution", HealthState.Critical, 0.9f)
                },
                detail: new[]
                {
                    B.Row("Relationship", "Synchronous — the caller cannot proceed without an answer", HealthState.Critical),
                    B.Row("Question asked", "What is the address of the federation metadata endpoint?"),
                    B.Row("Why this matters", "A synchronous dependency converts a downstream fault into an upstream outage without any error of its own"),
                    B.Row("Next hop", "Name resolution — svc-dns")
                }),

            B.Stage("s6", "Hop 4 · First failure", StageKind.Diagnose, 6.0,
                "The resolver drum opens. The zone the request needs is not loaded on either resolver. This is where expected and actual diverge.",
                B.Look("mod.resolution", 2.15f, 5.5f, 6, -24, 31),
                poses: B.Focus(0.12f, ("mod.resolution", ModulePose.Extracted(2.15f, 1f, 26, -6, 0.1f))),
                links: new[]
                {
                    B.Dep("mod.workload", "mod.resolution", "SERVFAIL", HealthState.Critical, 1f, 0.2f),
                    B.Bus("mod.resolution", "zone table", HealthState.Critical, 1f, 0.15f)
                },
                detail: new[]
                {
                    B.Row("Protocol", "DNS over UDP 53, two retries then TCP"),
                    B.Row("Dependency", "Internal DNS resolvers — svc-dns"),
                    B.Row("Expected", "NOERROR with an A record", HealthState.Healthy),
                    B.Row("Actual", "SERVFAIL after 2 800 ms on 100% of 412 queries, both resolvers", HealthState.Critical),
                    B.Row("Mechanism", "The conditional forwarder zone for the federation domain is absent from the running configuration"),
                    B.Row("Introduced by", "CHG-2291, applied 76 minutes ago", HealthState.Degraded),
                    B.Row("Evidence", "EV-1001 — resolver query log, platform attested", HealthState.Critical),
                    B.Row("Verdict", "First failure. Everything above this hop is a consequence.", HealthState.Critical)
                },
                focus: "mod.resolution", verdict: HealthState.Critical, evidence: "EV-1001", service: "svc-dns"),

            B.Stage("s7", "Hop 5 · Consequence, not cause", StageKind.Trace, 4.6,
                "The trust vault is healthy and still signing. It is never reached, because the caller cannot find its address.",
                B.Look("mod.identity", 1.7f, 6.4f, 10, -18, 34),
                poses: B.Focus(0.14f,
                    ("mod.resolution", ModulePose.Extracted(1.5f, 0.7f, 20, -4, 0f, 0.55f)),
                    ("mod.identity", ModulePose.Extracted(1.7f, 1f, -20, 5))),
                links: new[]
                {
                    B.Trust("mod.workload", "mod.identity", "introspect — never attempted", HealthState.Critical, 0.85f, 0.1f),
                    B.Dep("mod.identity", "mod.resolution", "metadata refresh", HealthState.Critical, 0.8f, 0.2f)
                },
                detail: new[]
                {
                    B.Row("Protocol", "OAuth 2.0 token introspection over HTTPS"),
                    B.Row("Dependency", "Federation service — idp-fed"),
                    B.Row("Expected", "HTTP 200 with an active token assertion", HealthState.Healthy),
                    B.Row("Actual", "Not attempted — endpoint address unresolved", HealthState.Critical),
                    B.Row("Service state", "Running and signing correctly from cached key material", HealthState.Degraded),
                    B.Row("Hidden deadline", "Cached signing keys expire in 41 minutes; after that interactive sign-in fails too", HealthState.Critical),
                    B.Row("Reading", "Restarting or failing over identity would not help and would discard the cache that is currently masking the outage", HealthState.Degraded)
                },
                focus: "mod.identity", verdict: HealthState.Degraded, service: "idp-fed"),

            B.Stage("s8", "Blast radius", StageKind.Trace, 4.2,
                "One absent zone, three failing services. The machine shows what shares the dependency rather than what shares a symptom.",
                B.Wide(150, 26, 12.0f, 0.8f, 42),
                poses: B.Bloom(0.72f, 0.35f, 0.3f,
                    ("mod.resolution", ModulePose.Extracted(1.85f, 0.85f, 18, -4, 0f, 1f)),
                    ("mod.workload", ModulePose.Extracted(1.1f, 0.4f, 0, 0, 0f, 0.9f)),
                    ("mod.identity", ModulePose.Extracted(1.1f, 0.4f, 0, 0, 0f, 0.9f))),
                links: new[]
                {
                    B.Dep("mod.workload", "mod.resolution", "partner API", HealthState.Critical, 1f, 0.3f),
                    B.Dep("mod.identity", "mod.resolution", "federation metadata", HealthState.Critical, 0.9f, 0.3f),
                    B.Trust("mod.workload", "mod.identity", "customer portal sign-in", HealthState.Degraded, 0.75f, 0.3f),
                    B.Bus("mod.control", "impact rollup", HealthState.Degraded, 0.7f)
                },
                detail: new[]
                {
                    B.Row("Failing outright", "Partner API Gateway — 14 partner tenants, 2 contractual commitments", HealthState.Critical),
                    B.Row("Degraded", "Customer Web Portal — sign-in only, anonymous browse unaffected", HealthState.Degraded),
                    B.Row("Degraded", "Collaboration Suite — presence and meeting join delayed", HealthState.Degraded),
                    B.Row("Not affected", "Finance and ERP — uses Kerberos against the directory, not the federation path", HealthState.Healthy),
                    B.Row("Users affected", "2 140"),
                    B.Row("Why grouping by symptom fails", "Three different symptoms, one dependency. Symptom clustering would have produced three investigations.")
                }),

            B.Stage("s9", "Staged correction", StageKind.Act, 5.0,
                "The automation actuator arms. It cannot mutate anything until policy, pre-check, simulation and an approved change have all cleared.",
                B.Look("mod.automation", 1.85f, 6.0f, 13, -20, 33),
                poses: B.Focus(0.14f,
                    ("mod.automation", ModulePose.Extracted(1.85f, 1f, -22, 6)),
                    ("mod.resolution", ModulePose.Extracted(1.2f, 0.6f, 12, -3, 0f, 0.6f))),
                links: new[]
                {
                    B.Dep("mod.automation", "mod.resolution", "restore forwarder zone", HealthState.Degraded, 1f, 1.1f),
                    B.Bus("mod.automation", "gated", HealthState.Degraded, 0.9f)
                },
                detail: new[]
                {
                    B.Row("Proposed action", "RB-014 — restore DNS conditional forwarder from configuration management"),
                    B.Row("Policy", "Operator holds the Name Resolution change role; estate is not in a change freeze", HealthState.Healthy),
                    B.Row("Pre-check", "Both resolvers reachable; no configuration drift outside the target stanza", HealthState.Healthy),
                    B.Row("Pre-check", "Previous revision r-2290 present; simulated reload completed in 41 s", HealthState.Healthy),
                    B.Row("Simulation", "Two lines added per resolver; failing queries replayed successfully in an isolated instance", HealthState.Healthy),
                    B.Row("Authorisation", "CHG-2304 emergency change — awaiting Emergency CAB", HealthState.Degraded),
                    B.Row("Mutation state", "Held. Nothing has been written.", HealthState.Degraded)
                },
                focus: "mod.automation", verdict: HealthState.Degraded, evidence: "EV-1003", service: "ctl-automation"),

            B.Stage("s10", "Verification", StageKind.Verify, 4.6,
                "Recovery is asserted from the same path that failed, not from the absence of alerts.",
                B.Between("mod.control", "mod.resolution", 8.8f, 22, 40),
                poses: B.Focus(0.2f,
                    ("mod.control", ModulePose.Extracted(1.5f, 1f, -16, 8)),
                    ("mod.resolution", ModulePose.Extracted(0.9f, 0.4f, 8, -2, 0f, 0.8f)),
                    ("mod.workload", ModulePose.Extracted(0.9f, 0.4f, 0, 0, 0f, 0.8f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.resolution", "20 queries", HealthState.Healthy, 1f, 1.8f),
                    B.Data("mod.control", "mod.workload", "synthetic transaction", HealthState.Healthy, 1f, 1.8f),
                    B.Bus("mod.control", "verify", HealthState.Healthy, 1f, 1.6f)
                },
                detail: new[]
                {
                    B.Row("Check 1", "Conditional forwarder zone present on both resolvers", HealthState.Healthy),
                    B.Row("Check 2", "20 of 20 queries return NOERROR with the expected A record", HealthState.Healthy),
                    B.Row("Check 3", "Federation metadata endpoint answers HTTP 200 from the application tier", HealthState.Healthy),
                    B.Row("Check 4", "Partner API synthetic transaction returns HTTP 200 with a valid token", HealthState.Healthy),
                    B.Row("Check 5", "Five minute rolling error rate below 0.5%", HealthState.Healthy),
                    B.Row("Not accepted as proof", "Alerts stopping. Absence of a signal is not evidence of recovery.", HealthState.Degraded)
                },
                focus: "mod.control", verdict: HealthState.Healthy, service: "ctl-telemetry"),

            B.Stage("s11", "Evidence sealed", StageKind.Verify, 4.0,
                "Claim, source, check, result and authority are written to the crown vault. The local walk stays local until countersigned.",
                B.Look("mod.evidence", 0.9f, 4.4f, 21, 0, 34, 0.02f),
                poses: B.Focus(0.18f, ("mod.evidence", ModulePose.Extracted(0.9f, 0.55f, 40, 0, 1f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.evidence", "attest", HealthState.Healthy, 1f, 1.4f),
                    B.Data("mod.automation", "mod.evidence", "execution record", HealthState.Healthy, 0.8f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Claim", "Name resolution was the first failing component on the partner API path"),
                    B.Row("Source", "Diagnostics dependency walk DX-2201 plus resolver query log"),
                    B.Row("Check", "Ordered hop evaluation with expected and actual recorded per hop"),
                    B.Row("Result", "Fail at hop 4; hops 1-3 pass; hops 5-6 not reached", HealthState.Critical),
                    B.Row("Authority", "EV-1002 local operator; EV-1001 platform attested", HealthState.Degraded),
                    B.Row("Preservation", "Sealed with digest, retained against INC-4412"),
                    B.Row("Boundary", "A local operator record never becomes project-authoritative without countersigning", HealthState.Degraded)
                },
                focus: "mod.evidence", verdict: HealthState.Healthy, evidence: "EV-1002", service: "prf-ledger"),

            B.Stage("s12", "Reassembly", StageKind.Reassemble, 5.0,
                "Every module returns along its extraction axis, seats against the spine and locks. The machine is whole and the path it broke on is proven good.",
                B.Wide(38, 17, 10.6f),
                links: new[]
                {
                    B.Bus("mod.resolution", "resolution", HealthState.Healthy, 0.9f, 1.6f),
                    B.Bus("mod.workload", "service", HealthState.Healthy, 0.9f, 1.6f),
                    B.Bus("mod.identity", "trust", HealthState.Healthy, 0.8f, 1.2f),
                    B.Bus("mod.control", "telemetry", HealthState.Healthy, 0.7f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Outcome", "Service restored on the path that failed", HealthState.Healthy),
                    B.Row("Cause", "Conditional forwarder zone removed by CHG-2291"),
                    B.Row("Correction", "RB-014 under emergency change CHG-2304"),
                    B.Row("Follow-up", "CHG-2291 verification step for conditional forwarder zones was recorded as not executed. That gap is the real defect.", HealthState.Degraded),
                    B.Row("Time to first failure identification", "22 minutes from major incident declaration")
                },
                verdict: HealthState.Healthy)
        }
    };
}
