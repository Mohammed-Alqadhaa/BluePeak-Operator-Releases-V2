using BluePeak.Domain;

namespace BluePeak.Simulation.Journeys;

internal static class AutomationJourney
{
    public static Journey Create() => new()
    {
        Id = "journey.automation",
        Name = "Gated automation run",
        Discipline = "Automation",
        Question = "Can I safely change this, and how will I know it worked?",
        Summary = "Automation earns trust by refusing to run, not by running fast. This journey walks every gate the "
                + "actuator must clear before it is permitted to mutate anything, shows a pre-check failing and blocking "
                + "the run, and ends with the evidence that makes the change defensible afterwards.",
        RunbookId = "RB-014",
        ChangeId = "CHG-2304",
        IncidentId = "INC-4412",
        Weight = Severity.High,
        ModulePath = new[] { "mod.automation", "mod.control", "mod.resolution", "mod.identity", "mod.evidence" },
        Stages = new[]
        {
            B.Stage("s0", "A request to change something", StageKind.Establish, 4.2,
                "An operator wants to restore a DNS forwarder zone during a critical incident. Nothing about that intention grants permission.",
                B.Wide(320, 20, 10.6f, 0.9f),
                links: new[] { B.Bus("mod.automation", "request", HealthState.Degraded, 0.8f, 1.2f) },
                detail: new[]
                {
                    B.Row("Runbook", "RB-014 — restore DNS conditional forwarder"),
                    B.Row("Target", "Internal DNS resolvers — svc-dns"),
                    B.Row("Requested by", "R. Achebe, incident commander for INC-4412"),
                    B.Row("Mutating", "Yes — writes configuration to two production resolvers", HealthState.Degraded),
                    B.Row("Default posture", "Refused. Every gate must pass before anything is written.", HealthState.Critical)
                }),

            B.Stage("s1", "Expose the gate stack", StageKind.Disassemble, 3.6,
                "The actuator wedge opens. Its gates are physical stages in series: nothing reaches the piston until each one clears.",
                B.Wide(332, 26, 11.4f, 1.2f, 40),
                poses: B.Bloom(0.5f, 0.4f, 0.2f,
                    ("mod.automation", ModulePose.Extracted(1.5f, 0.4f, -18, 5, 0.6f, 1f))),
                links: new[]
                {
                    B.Bus("mod.automation", "gate stack", HealthState.Degraded, 1f, 1.2f),
                    B.Trust("mod.automation", "mod.identity", "operator authorisation", HealthState.Healthy, 0.7f, 1f)
                },
                detail: new[]
                {
                    B.Row("Gate 1", "Request — capture inputs, target and requesting operator"),
                    B.Row("Gate 2", "Policy — role, scope and change freeze"),
                    B.Row("Gate 3", "Pre-check — assert the world is as the runbook assumes"),
                    B.Row("Gate 4", "Simulate — show exactly what would change"),
                    B.Row("Gate 5", "Approve — an authorised change record must exist"),
                    B.Row("Gate 6", "Execute — the only stage permitted to write", HealthState.Degraded),
                    B.Row("Gate 7", "Verify — prove the intended state was reached"),
                    B.Row("Gate 8", "Evidence — seal inputs, outputs and results")
                }),

            B.Stage("s2", "Policy", StageKind.Inspect, 4.4,
                "Authorisation is evaluated against the operator's role and the estate's current posture, not against their intent.",
                B.Look("mod.automation", 1.9f, 5.8f, 14, -22, 32),
                poses: B.Focus(0.12f,
                    ("mod.automation", ModulePose.Extracted(1.9f, 1f, -26, 7, 0.2f)),
                    ("mod.identity", ModulePose.Extracted(0.9f, 0.4f, 0, 0, 0f, 0.55f))),
                links: new[]
                {
                    B.Trust("mod.automation", "mod.identity", "role assertion", HealthState.Healthy, 1f, 1.4f),
                    B.Bus("mod.automation", "policy", HealthState.Healthy, 1f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Operator role", "Name Resolution change role — held", HealthState.Healthy),
                    B.Row("Target in scope", "svc-dns is within the role's permitted scope", HealthState.Healthy),
                    B.Row("Change freeze", "None active for this estate", HealthState.Healthy),
                    B.Row("Separation of duty", "Requester and approver must differ", HealthState.Healthy),
                    B.Row("Result", "Policy gate passed", HealthState.Healthy)
                },
                focus: "mod.automation", verdict: HealthState.Healthy, service: "ctl-automation"),

            B.Stage("s3", "Pre-check against the real estate", StageKind.Inspect, 5.4,
                "Pre-checks read the world the runbook is about to act on. One of them fails, and the actuator holds.",
                B.Between("mod.automation", "mod.resolution", 8.2f, 18, 38),
                poses: B.Focus(0.14f,
                    ("mod.automation", ModulePose.Extracted(1.6f, 0.9f, -22, 6, 0.2f)),
                    ("mod.resolution", ModulePose.Extracted(1.6f, 0.9f, 20, -5, 0.1f, 0.9f))),
                links: new[]
                {
                    B.Dep("mod.automation", "mod.resolution", "read running config", HealthState.Degraded, 1f, 1.2f),
                    B.Bus("mod.automation", "pre-check", HealthState.Degraded, 1f, 0.9f)
                },
                detail: new[]
                {
                    B.Row("Check 1", "Both resolvers answer a control query within 200 ms", HealthState.Healthy),
                    B.Row("Check 2", "Previous revision r-2290 present and restorable", HealthState.Healthy),
                    B.Row("Check 3", "Rollback completes within the stated 5 minute backout time", HealthState.Healthy),
                    B.Row("Check 4", "No configuration drift outside the target stanza", HealthState.Critical),
                    B.Row("Drift found", "Resolver 2 carries an unrelated manual edit to its forwarder list", HealthState.Critical),
                    B.Row("Actuator response", "Run held. Applying a template over an undocumented manual edit would silently destroy it.", HealthState.Critical),
                    B.Row("Operator decision", "Reconcile the drift into source of truth, then re-run", HealthState.Degraded)
                },
                focus: "mod.automation", verdict: HealthState.Critical, service: "svc-dns"),

            B.Stage("s4", "Simulate", StageKind.Diagnose, 5.2,
                "With drift reconciled, the run re-enters simulation. The exact diff is rendered and the failing queries are replayed against an isolated instance.",
                B.Look("mod.resolution", 1.95f, 5.6f, 8, -22, 32),
                poses: B.Focus(0.12f,
                    ("mod.resolution", ModulePose.Extracted(1.95f, 1f, 24, -5, 0.15f)),
                    ("mod.automation", ModulePose.Extracted(1.1f, 0.5f, -14, 3, 0f, 0.6f))),
                links: new[]
                {
                    B.Dep("mod.automation", "mod.resolution", "simulate diff", HealthState.Healthy, 1f, 1.6f),
                    B.Bus("mod.resolution", "shadow instance", HealthState.Healthy, 0.9f, 1.4f)
                },
                detail: new[]
                {
                    B.Row("Diff", "2 lines added per resolver — one zone stanza, one forwarder list"),
                    B.Row("Lines removed", "0 — the change is purely additive", HealthState.Healthy),
                    B.Row("Shadow replay", "412 previously failing queries replayed against a shadow resolver"),
                    B.Row("Predicted outcome", "412 of 412 return NOERROR with the expected A record", HealthState.Healthy),
                    B.Row("Predicted side effects", "None — no other zone's resolution path is altered", HealthState.Healthy),
                    B.Row("Mutation state", "Still nothing written to production", HealthState.Degraded)
                },
                focus: "mod.resolution", verdict: HealthState.Healthy, service: "svc-dns"),

            B.Stage("s5", "Approval is a record, not a click", StageKind.Act, 4.6,
                "The actuator will not arm on an operator's say-so. It requires an approved change record, and it checks the record rather than trusting the caller.",
                B.Look("mod.automation", 1.85f, 5.8f, 13, -20, 32),
                poses: B.Focus(0.14f, ("mod.automation", ModulePose.Extracted(1.85f, 1f, -24, 6, 0.35f))),
                links: new[]
                {
                    B.Bus("mod.automation", "approval gate", HealthState.Degraded, 1f, 0.8f),
                    B.Trust("mod.automation", "mod.identity", "approver identity", HealthState.Healthy, 0.8f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Change record", "CHG-2304 — emergency, restore federation conditional forwarder"),
                    B.Row("Approval 1", "Incident commander R. Achebe — granted", HealthState.Healthy),
                    B.Row("Approval 2", "Emergency CAB S. Okonkwo — outstanding", HealthState.Degraded),
                    B.Row("Separation of duty", "Satisfied: implementer H. Nowak is neither approver", HealthState.Healthy),
                    B.Row("Window", "Opens in 6 minutes, closes in 26"),
                    B.Row("State", "Armed but held. The piston has not moved.", HealthState.Degraded)
                },
                focus: "mod.automation", verdict: HealthState.Degraded, service: "ctl-automation"),

            B.Stage("s6", "Execute under observation", StageKind.Act, 5.2,
                "The piston drives. Each resolver is changed in sequence, and the second only proceeds once the first has answered a control query.",
                B.Between("mod.automation", "mod.resolution", 8.0f, 16, 37),
                poses: B.Focus(0.14f,
                    ("mod.automation", ModulePose.Extracted(1.5f, 0.85f, -20, 5, 0.9f)),
                    ("mod.resolution", ModulePose.Extracted(1.5f, 0.85f, 18, -4, 0.2f, 0.95f))),
                links: new[]
                {
                    B.Dep("mod.automation", "mod.resolution", "write and reload", HealthState.Healthy, 1f, 2.2f),
                    B.Data("mod.control", "mod.resolution", "live observation", HealthState.Healthy, 0.85f, 1.8f)
                },
                detail: new[]
                {
                    B.Row("Step", "Write forwarder stanza to resolver 1, reload", HealthState.Healthy),
                    B.Row("Guard", "Resolver 2 continues serving throughout — no window with zero resolvers", HealthState.Healthy),
                    B.Row("Gate", "Control query against resolver 1 must succeed before resolver 2 is touched", HealthState.Healthy),
                    B.Row("Step", "Write forwarder stanza to resolver 2, reload", HealthState.Healthy),
                    B.Row("Abort condition", "Any control query failure halts the run and triggers rollback", HealthState.Degraded),
                    B.Row("Elapsed", "41 seconds, within the simulated estimate")
                },
                verdict: HealthState.Healthy, service: "svc-dns"),

            B.Stage("s7", "Verify what was intended, not what was easy", StageKind.Verify, 5.0,
                "Verification asserts the outcome the request was made for, from the consumer's position, not from the target's own status page.",
                B.Look("mod.control", 1.75f, 6.2f, 17, -18, 34),
                poses: B.Focus(0.16f,
                    ("mod.control", ModulePose.Extracted(1.75f, 1f, -18, 8)),
                    ("mod.resolution", ModulePose.Extracted(0.9f, 0.4f, 8, -2, 0f, 0.8f)),
                    ("mod.workload", ModulePose.Extracted(0.9f, 0.4f, 0, 0, 0f, 0.8f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.resolution", "20 queries", HealthState.Healthy, 1f, 1.8f),
                    B.Data("mod.control", "mod.workload", "synthetic transaction", HealthState.Healthy, 1f, 1.8f),
                    B.Data("mod.control", "mod.identity", "metadata refresh", HealthState.Healthy, 0.9f, 1.6f)
                },
                detail: new[]
                {
                    B.Row("Check 1", "Zone present on both resolvers — config assertion", HealthState.Healthy),
                    B.Row("Check 2", "20 of 20 queries NOERROR from each resolver", HealthState.Healthy),
                    B.Row("Check 3", "Federation metadata endpoint reachable from the application tier", HealthState.Healthy),
                    B.Row("Check 4", "Partner API synthetic transaction returns HTTP 200", HealthState.Healthy),
                    B.Row("Check 5", "Rolling 5 minute error rate below 0.5%", HealthState.Healthy),
                    B.Row("Deliberately excluded", "The resolver's own health endpoint — it was green throughout the outage", HealthState.Critical)
                },
                focus: "mod.control", verdict: HealthState.Healthy, service: "ctl-telemetry"),

            B.Stage("s8", "Seal the run", StageKind.Verify, 4.4,
                "Inputs, diff, outputs and verification are hashed into the crown vault. The run is now defensible without anyone remembering it.",
                B.Look("mod.evidence", 0.9f, 4.4f, 21, 0, 34, 0.02f),
                poses: B.Focus(0.18f, ("mod.evidence", ModulePose.Extracted(0.9f, 0.55f, 40, 0, 1f))),
                links: new[]
                {
                    B.Data("mod.automation", "mod.evidence", "execution record", HealthState.Healthy, 1f, 1.6f),
                    B.Data("mod.control", "mod.evidence", "verification record", HealthState.Healthy, 0.9f, 1.4f)
                },
                detail: new[]
                {
                    B.Row("Sealed", "Request inputs, policy decision, pre-check results, simulated diff"),
                    B.Row("Sealed", "Applied diff, per-step output, elapsed time, operator and approver identities"),
                    B.Row("Sealed", "All five verification results with their observed values"),
                    B.Row("Digest", "SHA-256 over the run manifest"),
                    B.Row("Authority", "Platform attested — the control plane countersigned the run", HealthState.Healthy),
                    B.Row("Boundary", "A local dry run on an operator workstation would have remained local operator authority", HealthState.Degraded)
                },
                focus: "mod.evidence", verdict: HealthState.Healthy, evidence: "EV-1003", service: "prf-ledger"),

            B.Stage("s9", "Stand down", StageKind.Reassemble, 4.8,
                "The piston retracts, the gate stack resets to refused, and the machine closes. The next run starts from denial again.",
                B.Wide(320, 20, 10.6f, 0.9f),
                links: new[]
                {
                    B.Bus("mod.automation", "idle", HealthState.Healthy, 0.7f, 0.8f),
                    B.Bus("mod.resolution", "resolution", HealthState.Healthy, 0.9f, 1.4f),
                    B.Bus("mod.control", "telemetry", HealthState.Healthy, 0.8f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Outcome", "Service restored and verified from the consumer's position", HealthState.Healthy),
                    B.Row("Gates cleared", "8 of 8, with one pre-check failure that correctly stopped the first attempt", HealthState.Healthy),
                    B.Row("Time in gates", "3 minutes 12 seconds"),
                    B.Row("Time executing", "41 seconds"),
                    B.Row("Design point", "The ratio is intentional. The gates are the product; the write is the trivial part.", HealthState.Healthy),
                    B.Row("Next run", "Begins refused, as this one did")
                },
                verdict: HealthState.Healthy)
        }
    };
}
