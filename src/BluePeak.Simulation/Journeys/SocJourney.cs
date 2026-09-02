using BluePeak.Domain;

namespace BluePeak.Simulation.Journeys;

internal static class SocJourney
{
    public static Journey Create() => new()
    {
        Id = "journey.soc",
        Name = "Security detection and response",
        Discipline = "SOC",
        Question = "Four alerts fired on one account. Is this one intruder, or four coincidences?",
        Summary = "Correlation is the whole job. This journey opens the inspection aperture, binds four separate "
                + "detections onto a single subject, exposes the token that made them possible, and stages containment "
                + "that preserves evidence before it destroys it.",
        CaseId = "CASE-118",
        ChangeId = "CHG-2307",
        RunbookId = "RB-021",
        Weight = Severity.Critical,
        ModulePath = new[] { "mod.inspection", "mod.identity", "mod.ingress", "mod.workload", "mod.automation", "mod.evidence" },
        Stages = new[]
        {
            B.Stage("s0", "Four signals, one hour", StageKind.Establish, 4.4,
                "Individually, each of these detections is survivable noise. The question is whether they share a subject.",
                B.Wide(200, 20, 10.8f, 0.9f),
                links: new[]
                {
                    B.Bus("mod.inspection", "detections", HealthState.Critical, 0.9f, 1.4f),
                    B.Bus("mod.identity", "sign-in stream", HealthState.Degraded, 0.7f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("ANL-0038", "Refresh token presented from a previously unseen device", HealthState.Critical),
                    B.Row("ANL-0041", "Impossible travel between sign-ins", HealthState.Degraded),
                    B.Row("ANL-0042", "Mailbox rule created that hides financial correspondence", HealthState.Degraded),
                    B.Row("ANL-0044", "Report export volume 68 times baseline", HealthState.Degraded),
                    B.Row("Naive triage", "Four alerts, four analysts, four closures", HealthState.Critical),
                    B.Row("What decides it", "Whether they resolve to the same entity")
                }),

            B.Stage("s1", "Open the inspection aperture", StageKind.Disassemble, 3.6,
                "The control ring opens onto the inspection module. Its apertures index every signal by the entities it touches, not by the rule that produced it.",
                B.Wide(212, 26, 11.6f, 1.1f, 40),
                poses: B.Bloom(0.56f, 0.45f, 0.22f,
                    ("mod.inspection", ModulePose.Extracted(1.35f, 0.5f, -12, 4, 0.5f, 1f))),
                links: new[]
                {
                    B.Bus("mod.inspection", "entity index", HealthState.Critical, 1f, 1.6f),
                    B.Data("mod.inspection", "mod.identity", "sign-in events", HealthState.Degraded, 0.7f, 1.2f),
                    B.Data("mod.inspection", "mod.workload", "application audit", HealthState.Degraded, 0.7f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Correlation key", "Entity, not rule"),
                    B.Row("Entities extracted", "1 user, 2 hosts, 1 address, 1 token, 1 mailbox"),
                    B.Row("Result", "All four detections resolve onto the same user entity", HealthState.Critical),
                    B.Row("Case", "CASE-118 opened, severity Critical, owner N. Petrova")
                }),

            B.Stage("s2", "Separate the legitimate from the anomalous", StageKind.Inspect, 5.4,
                "The aperture rings separate the two device populations. The enrolled laptop is still in normal use — this is an addition, not a relocation.",
                B.Look("mod.inspection", 2.0f, 5.6f, 15, -22, 32),
                poses: B.Focus(0.12f, ("mod.inspection", ModulePose.Extracted(2.0f, 1f, -28, 8, 0.1f))),
                links: new[] { B.Bus("mod.inspection", "entity graph", HealthState.Critical, 1f, 1.2f) },
                detail: new[]
                {
                    B.Row("Subject", "j.harding — Finance, privileged for quarter-end close"),
                    B.Row("Device A", "FIN-LT-2207 — enrolled, compliant, endpoint protection healthy", HealthState.Healthy),
                    B.Row("Device B", "UNMANAGED-9f2c — no enrolment record, no endpoint protection", HealthState.Critical),
                    B.Row("Source", "203.0.113.47 — hosting provider, no sign-in history for this tenant", HealthState.Critical),
                    B.Row("Concurrency", "Both devices active in the same window", HealthState.Critical),
                    B.Row("Reading", "Not a relocated user. A second party holding the same credential material.", HealthState.Critical),
                    B.Row("Endpoint check", "No suspicious process lineage on the enrolled device — the theft did not happen there", HealthState.Healthy)
                },
                focus: "mod.inspection", verdict: HealthState.Critical, evidence: "EV-1030", service: "ctl-siem"),

            B.Stage("s3", "How the access was possible", StageKind.Diagnose, 5.6,
                "The trust vault opens. The refresh token was issued without device binding, so possession alone is sufficient to use it.",
                B.Look("mod.identity", 2.05f, 5.4f, 8, -24, 31),
                poses: B.Focus(0.1f,
                    ("mod.identity", ModulePose.Extracted(2.05f, 1f, -30, 7, 0.15f)),
                    ("mod.inspection", ModulePose.Extracted(1.2f, 0.5f, -12, 4, 0f, 0.5f))),
                links: new[]
                {
                    B.Trust("mod.inspection", "mod.identity", "token issuance record", HealthState.Critical, 1f, 0.9f),
                    B.Bus("mod.identity", "bearer token", HealthState.Critical, 1f, 0.6f)
                },
                detail: new[]
                {
                    B.Row("Artefact", "Refresh token rt_9c41…e7b2, issued 3 days ago"),
                    B.Row("Client", "Finance Reporting Web"),
                    B.Row("Scope", "Reports.Read.All, offline_access"),
                    B.Row("Expected", "Token bound to the issuing device by proof of possession", HealthState.Healthy),
                    B.Row("Actual", "Bearer token, no binding claim present", HealthState.Critical),
                    B.Row("Consequence", "Anyone holding the token is indistinguishable from the user", HealthState.Critical),
                    B.Row("Root weakness", "A client authentication policy, not a compromised credential", HealthState.Critical),
                    B.Row("Evidence", "EV-1031 — token issuance record, platform attested")
                },
                focus: "mod.identity", verdict: HealthState.Critical, evidence: "EV-1031", service: "idp-fed"),

            B.Stage("s4", "What the session did", StageKind.Trace, 5.0,
                "Following the token into the workload ring shows intent. The mailbox rule is the tell: this is preparation for payment redirection.",
                B.Between("mod.identity", "mod.workload", 8.4f, 16, 38),
                poses: B.Focus(0.14f,
                    ("mod.identity", ModulePose.Extracted(1.5f, 0.8f, -22, 5, 0f, 0.85f)),
                    ("mod.workload", ModulePose.Extracted(1.6f, 1f, -18, 4, 0.15f))),
                links: new[]
                {
                    B.Trust("mod.identity", "mod.workload", "authorised session", HealthState.Critical, 1f, 1.2f),
                    B.Data("mod.workload", "mod.inspection", "audit trail", HealthState.Degraded, 0.8f, 1f)
                },
                detail: new[]
                {
                    B.Row("Action 1", "Inbox rule created with a blank display name", HealthState.Critical),
                    B.Row("Rule effect", "Moves messages matching invoice, payment or remittance to a folder the user does not read"),
                    B.Row("Action 2", "412 MB of reporting extracts downloaded in 10 minutes", HealthState.Critical),
                    B.Row("Baseline", "6 MB per day for this account over 30 days"),
                    B.Row("Assessment", "Preparation for invoice redirection fraud, not opportunistic browsing", HealthState.Critical),
                    B.Row("Time sensitivity", "Quarter-end payment run is in progress", HealthState.Critical),
                    B.Row("Evidence", "EV-1032 — mailbox audit with the rule definition preserved verbatim")
                },
                verdict: HealthState.Critical, evidence: "EV-1032", service: "app-erp"),

            B.Stage("s5", "Preserve before you destroy", StageKind.Act, 5.2,
                "Containment would delete the rule and revoke the token. Both are evidence. The actuator preserves them first, then arms.",
                B.Look("mod.automation", 1.85f, 6.0f, 13, -20, 33),
                poses: B.Focus(0.14f,
                    ("mod.automation", ModulePose.Extracted(1.85f, 1f, -22, 6)),
                    ("mod.evidence", ModulePose.Extracted(0.6f, 0.35f, 24, 0, 0.7f, 0.85f))),
                links: new[]
                {
                    B.Data("mod.automation", "mod.evidence", "preserve artefacts", HealthState.Healthy, 1f, 1.6f),
                    B.Trust("mod.automation", "mod.identity", "revoke token family", HealthState.Degraded, 0.9f, 0.9f)
                },
                detail: new[]
                {
                    B.Row("Runbook", "RB-021 — contain compromised OAuth session"),
                    B.Row("Step 1", "Copy the mailbox rule definition verbatim into the ledger", HealthState.Healthy),
                    B.Row("Step 2", "Capture the token identifier and redemption history", HealthState.Healthy),
                    B.Row("Pre-check", "Enumerate every session revocation would terminate, including legitimate ones", HealthState.Degraded),
                    B.Row("Pre-check", "Subject is inside a declared business-critical period — quarter-end close", HealthState.Degraded),
                    B.Row("Gate", "Peer review required. A second analyst must countersign containment of a privileged account.", HealthState.Degraded),
                    B.Row("Mutation state", "Held pending K. Ibrahim's review", HealthState.Degraded)
                },
                focus: "mod.automation", verdict: HealthState.Degraded, service: "ctl-automation"),

            B.Stage("s6", "Contain the path, not just the session", StageKind.Act, 4.8,
                "Revoking the token stops this actor. Removing the bearer-token path stops the next one.",
                B.Between("mod.automation", "mod.identity", 8.6f, 18, 40),
                poses: B.Focus(0.16f,
                    ("mod.automation", ModulePose.Extracted(1.5f, 0.9f, -20, 5)),
                    ("mod.identity", ModulePose.Extracted(1.5f, 0.9f, -24, 5, 0.1f))),
                links: new[]
                {
                    B.Trust("mod.automation", "mod.identity", "revoke and rebind", HealthState.Healthy, 1f, 1.4f),
                    B.Bus("mod.identity", "policy update", HealthState.Healthy, 0.9f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Immediate", "Refresh token family revoked; all sessions for the subject terminated", HealthState.Healthy),
                    B.Row("Immediate", "Hiding mailbox rule removed after preservation confirmed", HealthState.Healthy),
                    B.Row("Immediate", "Unmanaged device blocked from token redemption", HealthState.Healthy),
                    B.Row("Structural", "CHG-2307 — require proof-of-possession binding for the finance reporting client", HealthState.Degraded),
                    B.Row("Why structural matters", "Without it, the same theft succeeds again with a different token", HealthState.Critical),
                    B.Row("User impact", "One re-authentication with MFA on the enrolled device")
                },
                verdict: HealthState.Healthy, service: "idp-fed"),

            B.Stage("s7", "Prove containment", StageKind.Verify, 4.8,
                "Containment is asserted by replaying the attack, not by observing that alerts stopped.",
                B.Between("mod.control", "mod.identity", 8.6f, 20, 40),
                poses: B.Focus(0.2f,
                    ("mod.control", ModulePose.Extracted(1.5f, 1f, -16, 8)),
                    ("mod.identity", ModulePose.Extracted(1.0f, 0.5f, 0, 0, 0f, 0.85f)),
                    ("mod.inspection", ModulePose.Extracted(1.0f, 0.5f, 0, 0, 0f, 0.8f))),
                links: new[]
                {
                    B.Data("mod.control", "mod.identity", "replay captured token", HealthState.Healthy, 1f, 1.8f),
                    B.Data("mod.inspection", "mod.workload", "post-containment watch", HealthState.Healthy, 0.9f, 1.4f)
                },
                detail: new[]
                {
                    B.Row("Check 1", "Replayed token returns invalid_grant", HealthState.Healthy),
                    B.Row("Check 2", "Enrolled device completes sign-in with MFA", HealthState.Healthy),
                    B.Row("Check 3", "No mailbox rules matching the pattern remain", HealthState.Healthy),
                    B.Row("Check 4", "Payment instructions reconciled against the run — no alteration found", HealthState.Healthy),
                    B.Row("Check 5", "No further redemption attempts from the unmanaged device in 60 minutes", HealthState.Healthy),
                    B.Row("Rejected as proof", "Alert silence. The actor stopping and the actor being stopped look identical.", HealthState.Degraded)
                },
                focus: "mod.control", verdict: HealthState.Healthy),

            B.Stage("s8", "Seal the case", StageKind.Verify, 4.4,
                "The vault records what was claimed, what was checked and who is entitled to assert it.",
                B.Look("mod.evidence", 0.9f, 4.4f, 21, 0, 34, 0.02f),
                poses: B.Focus(0.18f, ("mod.evidence", ModulePose.Extracted(0.9f, 0.55f, 40, 0, 1f))),
                links: new[]
                {
                    B.Data("mod.inspection", "mod.evidence", "detection records", HealthState.Healthy, 1f, 1.4f),
                    B.Data("mod.automation", "mod.evidence", "containment record", HealthState.Healthy, 0.9f, 1.2f)
                },
                detail: new[]
                {
                    B.Row("Claim", "A valid refresh token was used from an unenrolled device to prepare payment redirection"),
                    B.Row("Sources", "Identity sign-in logs, token issuance record, mailbox audit, application audit"),
                    B.Row("Preserved", "Rule definition, token identifier, redemption history, export manifest", HealthState.Healthy),
                    B.Row("Verdict", "True positive — contained", HealthState.Critical),
                    B.Row("Authority", "Platform attested for all four records", HealthState.Healthy),
                    B.Row("Retained for", "Potential fraud referral — retention set accordingly")
                },
                focus: "mod.evidence", verdict: HealthState.Healthy, evidence: "EV-1030", service: "prf-ledger"),

            B.Stage("s9", "Close the aperture", StageKind.Reassemble, 4.8,
                "The inspection module folds shut and the machine seals with one authentication path permanently narrower than it was.",
                B.Wide(200, 20, 10.8f, 0.9f),
                links: new[]
                {
                    B.Bus("mod.inspection", "detections", HealthState.Healthy, 0.8f, 1.2f),
                    B.Bus("mod.identity", "trust", HealthState.Healthy, 0.9f, 1.4f)
                },
                detail: new[]
                {
                    B.Row("Case", "CASE-118 — contained, pending fraud reconciliation closure"),
                    B.Row("Dwell time", "63 minutes from first redemption to containment"),
                    B.Row("Detection tuning", "None required — all four detections were correct", HealthState.Healthy),
                    B.Row("Structural fix", "CHG-2307 in change assessment"),
                    B.Row("Residual", "Other clients still issue bearer refresh tokens; review raised as a problem record", HealthState.Degraded)
                },
                verdict: HealthState.Healthy)
        }
    };
}
