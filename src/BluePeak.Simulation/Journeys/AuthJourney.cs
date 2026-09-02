using BluePeak.Domain;

namespace BluePeak.Simulation.Journeys;

internal static class AuthJourney
{
    public static Journey Create() => new()
    {
        Id = "journey.auth",
        Name = "Authentication and trust",
        Discipline = "Identity",
        Question = "Sign-in works for some people and not others. What is holding it up, and how long have we got?",
        Summary = "Identity is the most misleading subsystem to troubleshoot, because it degrades by succeeding from cache. "
                + "This journey opens the trust vault, exposes the key material it is signing from, and turns an "
                + "intermittent symptom into a countdown with a deadline.",
        IncidentId = "INC-4412",
        DiagnosticPathId = "DX-2210",
        ChangeId = "CHG-2307",
        Weight = Severity.Critical,
        ModulePath = new[] { "mod.ingress", "mod.workload", "mod.identity", "mod.resolution", "mod.control", "mod.evidence" },
        Stages = new[]
        {
            B.Stage("s0", "Two users, two outcomes", StageKind.Establish, 4.2,
                "The same request, made twice, produces a session once. Intermittent failure is the hardest signal to act on and the easiest to dismiss.",
                B.Wide(255, 16, 10.6f, 0.6f),
                links: new[]
                {
                    B.Bus("mod.identity", "trust", HealthState.Degraded, 0.85f, 0.6f),
                    B.Bus("mod.workload", "sign-in", HealthState.Degraded, 0.7f, 0.6f)
                },
                detail: new[]
                {
                    B.Row("Symptom", "Federated sign-in succeeds intermittently across three applications", HealthState.Degraded),
                    B.Row("Success rate", "Approximately 55% and falling"),
                    B.Row("Unaffected", "Kerberos sign-in to Finance and ERP", HealthState.Healthy),
                    B.Row("Common factor", "Every failing application uses the federation service"),
                    B.Row("What must be answered", "Is identity the cause, or is identity a victim?")
                }),

            B.Stage("s1", "Open the trust ring", StageKind.Disassemble, 3.6,
                "The core ring stands off the spine. Trust, resolution and workload are the three subsystems in scope; everything else recedes.",
                B.Wide(270, 24, 11.8f, 0.75f, 40),
                poses: B.Bloom(0.58f, 0.45f, 0.24f,
                    ("mod.identity", ModulePose.Extracted(0.95f, 0.2f, 0, 0, 0.35f, 1f)),
                    ("mod.resolution", ModulePose.Extracted(0.95f, 0.2f, 0, 0, 0.35f, 0.9f)),
                    ("mod.workload", ModulePose.Extracted(0.95f, 0.2f, 0, 0, 0.35f, 0.9f))),
                links: new[]
                {
                    B.Trust("mod.workload", "mod.identity", "SAML 2.0", HealthState.Degraded, 0.9f, 0.8f),
                    B.Dep("mod.identity", "mod.resolution", "metadata refresh", HealthState.Critical, 0.9f, 0.3f)
                },
                detail: new[]
                {
                    B.Row("In scope", "Identity and trust, name resolution, application workload"),
                    B.Row("Out of scope", "Directory services — Kerberos sign-in is unaffected, which excludes the directory", HealthState.Healthy),
                    B.Row("Method", "Inspect what the trust vault is signing with, and where that material comes from")
                }),

            B.Stage("s2", "The collar and the vault", StageKind.Inspect, 5.4,
                "The keyed collar retracts and the signing vault is exposed. It is working — that is precisely the problem.",
                B.Look("mod.identity", 2.05f, 5.4f, 8, -24, 31),
                poses: B.Focus(0.12f, ("mod.identity", ModulePose.Extracted(2.05f, 1f, -30, 7, 0.15f))),
                links: new[] { B.Bus("mod.identity", "signing", HealthState.Degraded, 1f, 0.7f) },
                detail: new[]
                {
                    B.Row("Protocol", "SAML 2.0 and OpenID Connect"),
                    B.Row("Dependency", "Federation service — idp-fed"),
                    B.Row("Expected", "Assertions signed with current key material refreshed on schedule", HealthState.Healthy),
                    B.Row("Actual", "Assertions signed from a cached key set; last successful refresh 66 minutes ago", HealthState.Degraded),
                    B.Row("Cache remaining", "41 minutes", HealthState.Critical),
                    B.Row("Why intermittent", "Requests served from the warm cache succeed. Requests that trigger a refresh path fail."),
                    B.Row("Reading", "This is not a stable degraded state. It is a countdown.", HealthState.Critical)
                },
                focus: "mod.identity", verdict: HealthState.Degraded, service: "idp-fed"),

            B.Stage("s3", "Where the key material comes from", StageKind.Trace, 4.2,
                "The refresh path leaves the trust vault and drops into core services. The vault cannot renew what it cannot reach.",
                B.Between("mod.identity", "mod.resolution", 8.4f, 18, 38),
                poses: B.Focus(0.14f,
                    ("mod.identity", ModulePose.Extracted(1.5f, 0.8f, -22, 5, 0.1f, 0.95f)),
                    ("mod.resolution", ModulePose.Extracted(1.5f, 0.8f, 18, -5, -0.1f))),
                links: new[]
                {
                    B.Dep("mod.identity", "mod.resolution", "resolve metadata host", HealthState.Critical, 1f, 0.25f),
                    B.Bus("mod.resolution", "zone table", HealthState.Critical, 0.9f, 0.15f)
                },
                detail: new[]
                {
                    B.Row("Relationship", "Synchronous, on a timer rather than on the request path", HealthState.Critical),
                    B.Row("Consequence of that", "The failure is invisible until the cache expires — a delayed-action fault"),
                    B.Row("Question asked", "What is the address of the federation metadata endpoint?"),
                    B.Row("Why identity looked healthy", "Its own health check tests signing, not refresh", HealthState.Degraded)
                }),

            B.Stage("s4", "First failure, again", StageKind.Diagnose, 5.2,
                "The resolver drum exposes the same absent zone found on the partner API path. One fault, two very different symptoms.",
                B.Look("mod.resolution", 2.1f, 5.5f, 6, -22, 31),
                poses: B.Focus(0.1f, ("mod.resolution", ModulePose.Extracted(2.1f, 1f, 24, -6, 0.1f))),
                links: new[]
                {
                    B.Dep("mod.identity", "mod.resolution", "SERVFAIL", HealthState.Critical, 1f, 0.2f),
                    B.Bus("mod.resolution", "zone table", HealthState.Critical, 1f, 0.15f)
                },
                detail: new[]
                {
                    B.Row("Protocol", "DNS over UDP 53"),
                    B.Row("Expected", "NOERROR with an A record", HealthState.Healthy),
                    B.Row("Actual", "SERVFAIL, both resolvers", HealthState.Critical),
                    B.Row("Same fault as", "DX-2201, the partner API path", HealthState.Critical),
                    B.Row("Evidence", "EV-1001 — resolver query log, platform attested"),
                    B.Row("Verdict", "Identity is a victim. Acting on identity would destroy the cache that is currently holding the estate up.", HealthState.Critical)
                },
                focus: "mod.resolution", verdict: HealthState.Critical, evidence: "EV-1001", service: "svc-dns"),

            B.Stage("s5", "Trust blast radius", StageKind.Trace, 4.4,
                "Everything that authenticates through federation shares the deadline. Everything on Kerberos does not.",
                B.Wide(320, 26, 12.2f, 0.8f, 42),
                poses: B.Bloom(0.7f, 0.32f, 0.26f,
                    ("mod.identity", ModulePose.Extracted(1.8f, 0.8f, -20, 4, 0f, 1f)),
                    ("mod.workload", ModulePose.Extracted(1.15f, 0.45f, 0, 0, 0f, 0.9f))),
                links: new[]
                {
                    B.Trust("mod.workload", "mod.identity", "OIDC and SAML consumers", HealthState.Degraded, 1f, 0.5f),
                    B.Dep("mod.identity", "mod.resolution", "refresh blocked", HealthState.Critical, 0.9f, 0.2f),
                    B.Bus("mod.control", "deadline tracking", HealthState.Critical, 0.8f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Fails at cache expiry", "Customer Web Portal, Partner API, Collaboration Suite, CRM", HealthState.Critical),
                    B.Row("Fails now", "Any client that forces a fresh metadata fetch", HealthState.Critical),
                    B.Row("Survives", "Finance and ERP — Kerberos against the directory", HealthState.Healthy),
                    B.Row("Survives", "Network access control — RADIUS against the directory", HealthState.Healthy),
                    B.Row("Deadline", "41 minutes to full federated authentication outage", HealthState.Critical),
                    B.Row("Escalation trigger", "Deadline is inside the emergency change window, so the change must not slip")
                }),

            B.Stage("s6", "Protect the cache while you fix the cause", StageKind.Act, 4.8,
                "The one safe identity action is to extend the survival window. The automation actuator arms for the resolver, not for identity.",
                B.Look("mod.automation", 1.8f, 6.0f, 13, -20, 33),
                poses: B.Focus(0.14f,
                    ("mod.automation", ModulePose.Extracted(1.8f, 1f, -22, 6)),
                    ("mod.identity", ModulePose.Extracted(1.0f, 0.5f, 0, 0, 0f, 0.6f))),
                links: new[]
                {
                    B.Dep("mod.automation", "mod.resolution", "restore zone", HealthState.Degraded, 1f, 1.1f),
                    B.Trust("mod.automation", "mod.identity", "extend cache lifetime", HealthState.Healthy, 0.8f, 1f)
                },
                detail: new[]
                {
                    B.Row("Action 1", "Extend federation metadata cache lifetime — non-mutating to trust material", HealthState.Healthy),
                    B.Row("Effect", "Deadline moves from 41 minutes to 4 hours, removing the time pressure"),
                    B.Row("Action 2", "RB-014 restore the resolver zone — the actual correction"),
                    B.Row("Explicitly not done", "Restarting the federation service", HealthState.Critical),
                    B.Row("Why not", "A restart discards the cache and converts a partial outage into a total one immediately", HealthState.Critical),
                    B.Row("Policy", "Both actions gated; the trust action requires the Identity change role")
                },
                focus: "mod.automation", verdict: HealthState.Degraded, service: "ctl-automation"),

            B.Stage("s7", "Prove trust end to end", StageKind.Verify, 4.6,
                "Verification exercises a full sign-in with a forced refresh, which is the case that was failing, rather than the cached case that was passing.",
                B.Between("mod.control", "mod.identity", 8.6f, 20, 40),
                poses: B.Focus(0.2f,
                    ("mod.control", ModulePose.Extracted(1.5f, 1f, -16, 8)),
                    ("mod.identity", ModulePose.Extracted(1.0f, 0.5f, 0, 0, 0f, 0.85f)),
                    ("mod.workload", ModulePose.Extracted(0.9f, 0.4f, 0, 0, 0f, 0.8f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.identity", "forced refresh", HealthState.Healthy, 1f, 1.8f),
                    B.Trust("mod.workload", "mod.identity", "full sign-in", HealthState.Healthy, 1f, 1.6f)
                },
                detail: new[]
                {
                    B.Row("Check 1", "Metadata endpoint resolves and returns current key material", HealthState.Healthy),
                    B.Row("Check 2", "Key rollover completes with a forced refresh", HealthState.Healthy),
                    B.Row("Check 3", "Full sign-in succeeds 10 of 10 with a cold cache", HealthState.Healthy),
                    B.Row("Check 4", "Kerberos path remains unaffected throughout", HealthState.Healthy),
                    B.Row("Rejected as proof", "Sign-in succeeding from the warm cache, which was already true while broken", HealthState.Degraded)
                },
                focus: "mod.control", verdict: HealthState.Healthy),

            B.Stage("s8", "Close the class of fault", StageKind.Verify, 4.2,
                "A verified fix restores the service. Recording why the fault was invisible is what stops it recurring.",
                B.Look("mod.evidence", 0.9f, 4.4f, 21, 0, 34, 0.02f),
                poses: B.Focus(0.18f, ("mod.evidence", ModulePose.Extracted(0.9f, 0.55f, 40, 0, 1f))),
                links: new[] { B.Data("mod.control", "mod.evidence", "attest", HealthState.Healthy, 1f, 1.4f) },
                detail: new[]
                {
                    B.Row("Claim", "Federation degradation was caused by resolver failure, masked by a metadata cache"),
                    B.Row("Systemic finding", "The identity health check tests signing but not refresh", HealthState.Degraded),
                    B.Row("Correction raised", "Health check extended to assert a successful metadata refresh within the cache period"),
                    B.Row("Related change", "CHG-2307 — device-bound tokens, raised from CASE-118 against the same subsystem"),
                    B.Row("Authority", "Platform attested for the resolver record; local operator for the walk", HealthState.Degraded)
                },
                focus: "mod.evidence", verdict: HealthState.Healthy, evidence: "EV-1001", service: "prf-ledger"),

            B.Stage("s9", "Seat and lock", StageKind.Reassemble, 4.8,
                "The trust vault collar re-engages, the core ring seats and the machine closes with the refresh path proven rather than assumed.",
                B.Wide(255, 16, 10.6f, 0.6f),
                links: new[]
                {
                    B.Bus("mod.identity", "trust", HealthState.Healthy, 0.9f, 1.4f),
                    B.Bus("mod.resolution", "resolution", HealthState.Healthy, 0.9f, 1.4f),
                    B.Bus("mod.workload", "service", HealthState.Healthy, 0.8f, 1.4f)
                },
                detail: new[]
                {
                    B.Row("Outcome", "Federated authentication restored and proven with a cold cache", HealthState.Healthy),
                    B.Row("Deadline", "Cleared with 26 minutes of cache remaining"),
                    B.Row("Residual risk", "None on this path; the masking behaviour is now monitored")
                },
                verdict: HealthState.Healthy)
        }
    };
}
