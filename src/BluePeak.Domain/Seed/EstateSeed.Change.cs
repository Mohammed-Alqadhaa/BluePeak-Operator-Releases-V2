namespace BluePeak.Domain.Seed;

public static partial class EstateSeed
{
    private static void BuildChangeAndAutomation(EstateModel m)
    {
        // ------------------------------------------------------------------ Changes
        var chg2291 = new ChangeRequest
        {
            Id = "CHG-2291",
            Title = "Consolidate internal DNS conditional forwarders onto new resolver pair",
            Type = "Normal",
            Risk = ChangeRisk.Moderate,
            State = ChangeState.Completed,
            Requester = "Platform Engineering",
            Implementer = "H. Nowak",
            WindowStart = Anchor.AddMinutes(-90),
            WindowEnd = Anchor.AddMinutes(-70),
            TargetServiceId = "svc-dns",
            Description = "Retire the legacy forwarder configuration and move all conditional forwarder zones onto the "
                        + "new resolver pair. Intended to remove a single point of failure ahead of the datacentre refresh.",
            RollbackPlan = "Restore the previous named.conf from configuration management and reload both resolvers. "
                         + "Reload is non-disruptive and takes under 60 seconds.",
            BackoutTime = "5 min",
            LinkedIncidentId = "INC-4412"
        };
        chg2291.BlastRadiusServiceIds.AddRange(new[] { "idp-fed", "app-api", "app-web", "app-collab", "svc-smtp" });
        chg2291.Approvals.AddRange(new[]
        {
            new Approval { Board = "Technical Review", Approver = "S. Okonkwo", State = GateState.Passed, DecidedAt = Anchor.AddHours(-26), Comment = "Configuration diff reviewed." },
            new Approval { Board = "Change Advisory Board", Approver = "R. Achebe", State = GateState.Passed, DecidedAt = Anchor.AddHours(-20), Comment = "Approved for the standard Tuesday window." }
        });
        chg2291.Verification.AddRange(new[]
        {
            new VerificationCheck { Name = "Resolver answers for internal zones", Method = "Synthetic query x20", Expected = "NOERROR", Actual = "NOERROR", Result = EvidenceResult.Pass, EvidenceId = "EV-1040" },
            new VerificationCheck { Name = "Resolver answers for conditional forwarder zones", Method = "Synthetic query x20", Expected = "NOERROR", Actual = "Not executed — zone list not enumerated post-change", Result = EvidenceResult.Inconclusive, EvidenceId = "EV-1041" },
            new VerificationCheck { Name = "Downstream federation metadata reachable", Method = "HTTPS probe", Expected = "200 OK", Actual = "Not executed", Result = EvidenceResult.NotRun }
        });
        chg2291.EvidenceIds.AddRange(new[] { "EV-1040", "EV-1041" });
        m.Changes.Add(chg2291);

        var chg2304 = new ChangeRequest
        {
            Id = "CHG-2304",
            Title = "Emergency: restore federation conditional forwarder on resolver pair",
            Type = "Emergency",
            Risk = ChangeRisk.Moderate,
            State = ChangeState.AwaitingApproval,
            Requester = "R. Achebe",
            Implementer = "H. Nowak",
            WindowStart = Anchor.AddMinutes(6),
            WindowEnd = Anchor.AddMinutes(26),
            TargetServiceId = "svc-dns",
            Description = "Restore the conditional forwarder zone removed by CHG-2291 so that federation metadata resolution "
                        + "recovers. Applied via configuration management so the running state matches source of truth.",
            RollbackPlan = "Re-apply the CHG-2291 configuration. The forwarder change is a single stanza; reload is non-disruptive.",
            BackoutTime = "5 min",
            LinkedIncidentId = "INC-4412"
        };
        chg2304.BlastRadiusServiceIds.AddRange(new[] { "svc-dns", "idp-fed", "app-api", "app-web", "app-collab" });
        chg2304.Approvals.AddRange(new[]
        {
            new Approval { Board = "Incident Commander", Approver = "R. Achebe", State = GateState.Passed, DecidedAt = Anchor.AddMinutes(-10), Comment = "Required to restore service." },
            new Approval { Board = "Emergency CAB", Approver = "S. Okonkwo", State = GateState.WaitingApproval, Comment = "" }
        });
        chg2304.Verification.AddRange(new[]
        {
            new VerificationCheck { Name = "Conditional forwarder zone present on both resolvers", Method = "Config assertion", Expected = "Zone present, forwarders 2", Result = EvidenceResult.NotRun },
            new VerificationCheck { Name = "Federation metadata resolves", Method = "DNS query x10 from both resolvers", Expected = "NOERROR with A record", Result = EvidenceResult.NotRun },
            new VerificationCheck { Name = "Partner API token introspection succeeds", Method = "Synthetic transaction", Expected = "HTTP 200 with valid token", Result = EvidenceResult.NotRun },
            new VerificationCheck { Name = "Error rate returns below objective", Method = "5 minute rolling window", Expected = "< 0.5%", Result = EvidenceResult.NotRun }
        });
        m.Changes.Add(chg2304);

        var chg2298 = new ChangeRequest
        {
            Id = "CHG-2298",
            Title = "Replace flapping optic in Campus C distribution stack",
            Type = "Normal",
            Risk = ChangeRisk.Low,
            State = ChangeState.Approved,
            Requester = "Network Engineering",
            Implementer = "T. Lindqvist",
            WindowStart = Anchor.AddHours(9),
            WindowEnd = Anchor.AddHours(11),
            TargetServiceId = "net-dist-c",
            Description = "Replace the SFP+ transceiver on Gi1/0/47 and return the port-channel to two active members.",
            RollbackPlan = "Reinstate the original optic and leave the member administratively down as it is today.",
            BackoutTime = "10 min",
            LinkedIncidentId = "INC-4415"
        };
        chg2298.BlastRadiusServiceIds.AddRange(new[] { "app-vdi" });
        chg2298.Approvals.Add(new Approval { Board = "Change Advisory Board", Approver = "S. Okonkwo", State = GateState.Passed, DecidedAt = Anchor.AddMinutes(-95), Comment = "Out-of-hours window, single member already isolated." });
        chg2298.Verification.AddRange(new[]
        {
            new VerificationCheck { Name = "Both port-channel members up", Method = "Interface assertion", Expected = "2 active members", Result = EvidenceResult.NotRun },
            new VerificationCheck { Name = "Zero flaps over 60 minutes", Method = "Counter watch", Expected = "0 transitions", Result = EvidenceResult.NotRun }
        });
        m.Changes.Add(chg2298);

        var chg2307 = new ChangeRequest
        {
            Id = "CHG-2307",
            Title = "Enforce device-bound tokens for finance reporting client",
            Type = "Normal",
            Risk = ChangeRisk.High,
            State = ChangeState.Assessment,
            Requester = "Security Engineering",
            Implementer = "N. Petrova",
            WindowStart = Anchor.AddDays(2),
            WindowEnd = Anchor.AddDays(2).AddHours(2),
            TargetServiceId = "idp-fed",
            Description = "Require proof-of-possession binding on refresh tokens issued to the finance reporting client, "
                        + "removing the bearer-token replay path exercised in CASE-118.",
            RollbackPlan = "Revert the client authentication policy assignment. Existing sessions are unaffected by the revert.",
            BackoutTime = "5 min"
        };
        chg2307.BlastRadiusServiceIds.AddRange(new[] { "idp-fed", "app-erp", "app-web" });
        chg2307.Approvals.AddRange(new[]
        {
            new Approval { Board = "Security Design Authority", Approver = "N. Petrova", State = GateState.Passed, DecidedAt = Anchor.AddMinutes(-40), Comment = "Directly closes the CASE-118 attack path." },
            new Approval { Board = "Change Advisory Board", Approver = "S. Okonkwo", State = GateState.Pending },
            new Approval { Board = "Business Owner — Finance", Approver = "C. Vasquez", State = GateState.Pending, Comment = "Quarter-end close in progress." }
        });
        chg2307.Verification.AddRange(new[]
        {
            new VerificationCheck { Name = "Bearer refresh token rejected", Method = "Negative test with replayed token", Expected = "invalid_grant", Result = EvidenceResult.NotRun },
            new VerificationCheck { Name = "Bound token accepted from enrolled device", Method = "Positive test", Expected = "HTTP 200", Result = EvidenceResult.NotRun },
            new VerificationCheck { Name = "Scheduled report extraction unaffected", Method = "Job run observation", Expected = "Job completes", Result = EvidenceResult.NotRun }
        });
        m.Changes.Add(chg2307);

        var chg2285 = new ChangeRequest
        {
            Id = "CHG-2285",
            Title = "UPS battery string replacement — DC Alpha B feed",
            Type = "Standard",
            Risk = ChangeRisk.Low,
            State = ChangeState.Implementing,
            Requester = "Data Centre Ops",
            Implementer = "Facilities Vendor",
            WindowStart = Anchor.AddMinutes(-160),
            WindowEnd = Anchor.AddMinutes(120),
            TargetServiceId = "fnd-power",
            Description = "Scheduled battery replacement on the B feed. A feed carries full load throughout.",
            RollbackPlan = "Reinstate the existing string. Load remains on the A feed in all cases.",
            BackoutTime = "30 min"
        };
        chg2285.Approvals.Add(new Approval { Board = "Standard Change Catalogue", Approver = "Pre-authorised", State = GateState.Passed, DecidedAt = Anchor.AddDays(-6) });
        chg2285.Verification.Add(new VerificationCheck { Name = "Both feeds carrying load", Method = "Facility telemetry", Expected = "A and B nominal", Result = EvidenceResult.NotRun });
        m.Changes.Add(chg2285);

        // ------------------------------------------------------------------ Runbooks
        Runbook Rb(string id, string name, string category, string purpose, ChangeRisk risk, bool requiresChange,
            string[] targets, string? forIncident, double? lastRunMinutes, string lastResult, int runCount, RunbookStep[] steps)
        {
            var r = new Runbook
            {
                Id = id,
                Name = name,
                Category = category,
                Purpose = purpose,
                Risk = risk,
                RequiresChange = requiresChange,
                LastRunAt = lastRunMinutes is null ? null : Anchor.AddMinutes(-lastRunMinutes.Value),
                LastRunResult = lastResult,
                RunCount = runCount,
                SuggestedForIncidentId = forIncident
            };
            r.TargetServiceIds.AddRange(targets);
            r.Steps.AddRange(steps);
            m.Runbooks.Add(r);
            return r;
        }

        RunbookStep Step(string name, string gate, string detail, double seconds, bool approval = false, bool mutating = false)
            => new() { Name = name, Gate = gate, Detail = detail, EstimatedSeconds = seconds, RequiresApproval = approval, Mutating = mutating };

        Rb("RB-014", "Restore DNS conditional forwarder", "Name Resolution",
            "Restores a conditional forwarder zone from configuration management to both resolvers and proves resolution recovered.",
            ChangeRisk.Moderate, true, new[] { "svc-dns", "idp-fed" }, "INC-4412", 31, "Pre-check passed — awaiting change approval", 7,
            new[]
            {
                Step("Capture request parameters", "Request", "Target zone, resolver pair, requesting incident and operator identity are recorded before anything else runs.", 1.2),
                Step("Evaluate execution policy", "Policy", "Confirms the operator holds the Name Resolution change role, the target is in scope, and the estate is not inside a change freeze.", 1.8),
                Step("Pre-check: resolver reachability", "Pre-check", "Both resolvers answer a control query on port 53 within 200 ms.", 2.4),
                Step("Pre-check: configuration drift", "Pre-check", "Running configuration is compared against the configuration management source of truth. Drift outside the target stanza aborts the run.", 2.6),
                Step("Pre-check: rollback viability", "Pre-check", "The previous configuration revision is present and restorable within the stated backout time.", 2.0),
                Step("Simulate: configuration diff", "Simulate", "Renders exactly which lines would change on each resolver. No write is performed.", 2.8),
                Step("Simulate: predicted resolution outcome", "Simulate", "Replays the failing queries against the proposed configuration in an isolated resolver instance.", 3.2),
                Step("Await change authorisation", "Approve", "Execution is held until an approved change record authorises the mutation.", 2.0, approval: true),
                Step("Apply configuration to resolver 1", "Execute", "Writes the forwarder stanza and reloads. Resolver 2 continues to serve during the reload.", 3.4, mutating: true),
                Step("Apply configuration to resolver 2", "Execute", "Repeats on the second resolver once the first has answered a control query successfully.", 3.4, mutating: true),
                Step("Verify: zone answers correctly", "Verify", "Twenty queries against each resolver must return NOERROR with the expected A record.", 3.0),
                Step("Verify: downstream federation metadata", "Verify", "HTTPS probe of the federation metadata endpoint from the application tier.", 2.6),
                Step("Verify: partner API synthetic transaction", "Verify", "End-to-end authenticated call must return HTTP 200.", 3.0),
                Step("Seal evidence record", "Evidence", "Inputs, diff, outputs and verification results are hashed and written to the evidence ledger.", 1.6)
            });

        Rb("RB-021", "Contain compromised OAuth session", "Identity",
            "Revokes a refresh token family, terminates active sessions, and preserves the artefacts before removing them.",
            ChangeRisk.High, true, new[] { "idp-fed", "app-erp" }, null, null, "Never run", 0,
            new[]
            {
                Step("Capture subject and scope", "Request", "Records the user, token family, client and initiating case.", 1.2),
                Step("Evaluate execution policy", "Policy", "Requires the Identity Containment role and a peer reviewer distinct from the requester.", 1.8),
                Step("Pre-check: blast radius of revocation", "Pre-check", "Enumerates every active session that would be terminated, including legitimate ones.", 2.8),
                Step("Pre-check: business impact window", "Pre-check", "Warns when the subject is inside a declared business-critical period such as financial close.", 2.2),
                Step("Preserve mailbox rule definition", "Pre-check", "Copies the rule verbatim into the evidence ledger before any deletion is proposed.", 2.4),
                Step("Simulate: revocation outcome", "Simulate", "Shows which tokens are invalidated and what the user experience will be at next sign-in.", 3.0),
                Step("Await peer review", "Approve", "A second analyst must countersign containment of an active privileged account.", 2.0, approval: true),
                Step("Revoke refresh token family", "Execute", "Invalidates the token family so replay from the unmanaged device fails.", 3.0, mutating: true),
                Step("Terminate active sessions", "Execute", "Signs out all sessions for the subject across clients.", 2.6, mutating: true),
                Step("Remove hiding mailbox rule", "Execute", "Deletes the rule after the preserved copy is confirmed in the ledger.", 2.4, mutating: true),
                Step("Verify: replay attempt fails", "Verify", "Replays the captured token and requires invalid_grant.", 2.8),
                Step("Verify: legitimate device can re-authenticate", "Verify", "Confirms the enrolled laptop completes sign-in with MFA.", 2.6),
                Step("Seal evidence record", "Evidence", "Containment inputs, outputs and verification are sealed to the ledger.", 1.6)
            });

        Rb("RB-007", "Drain and restore access switch member", "Network",
            "Safely removes a flapping port-channel member from service and returns it after replacement.",
            ChangeRisk.Moderate, true, new[] { "net-dist-c", "app-vdi" }, "INC-4415", 38, "Completed — member drained", 23,
            new[]
            {
                Step("Capture target interface", "Request", "Device, interface, channel group and initiating incident.", 1.2),
                Step("Evaluate execution policy", "Policy", "Requires the Network Change role and confirms the device is not the last path to any tier-1 service.", 1.8),
                Step("Pre-check: remaining capacity", "Pre-check", "Confirms the surviving members carry current load with headroom.", 2.6),
                Step("Pre-check: redundancy assertion", "Pre-check", "Refuses to proceed if draining would isolate any downstream node.", 2.4),
                Step("Simulate: traffic redistribution", "Simulate", "Models per-member utilisation after the drain.", 2.8),
                Step("Await change authorisation", "Approve", "Held for an approved change record.", 2.0, approval: true),
                Step("Administratively shut member", "Execute", "Takes the flapping member out of the bundle.", 2.6, mutating: true),
                Step("Verify: channel stable", "Verify", "No transitions observed over the watch window.", 3.4),
                Step("Verify: downstream sessions stable", "Verify", "Desktop session disconnect rate returns to baseline.", 3.0),
                Step("Seal evidence record", "Evidence", "Interface counters before and after are sealed to the ledger.", 1.6)
            });

        Rb("RB-032", "Rotate service account credential", "Identity",
            "Rotates a service account secret with coordinated consumer reload.",
            ChangeRisk.High, true, new[] { "idp-ad", "app-erp" }, null, 4320, "Completed successfully", 12,
            new[]
            {
                Step("Capture account and consumers", "Request", "Account, secret store path and every registered consumer.", 1.2),
                Step("Evaluate execution policy", "Policy", "Requires the Identity change role and a maintenance window.", 1.8),
                Step("Pre-check: consumer inventory complete", "Pre-check", "Every consumer must be registered; an unregistered consumer aborts the run.", 2.8),
                Step("Pre-check: secret store writable", "Pre-check", "Confirms the vault path accepts a new version.", 2.0),
                Step("Simulate: rotation sequence", "Simulate", "Shows the ordering of secret write and consumer reload.", 2.6),
                Step("Await change authorisation", "Approve", "Held for an approved change record.", 2.0, approval: true),
                Step("Write new secret version", "Execute", "Adds a new version while the previous remains valid.", 2.6, mutating: true),
                Step("Reload consumers", "Execute", "Rolls consumers onto the new version one at a time.", 3.4, mutating: true),
                Step("Retire previous version", "Execute", "Marks the old secret version invalid.", 2.0, mutating: true),
                Step("Verify: all consumers authenticated", "Verify", "Each consumer must show a successful authentication after reload.", 3.0),
                Step("Seal evidence record", "Evidence", "Rotation record sealed to the ledger.", 1.6)
            });

        Rb("RB-045", "Collect diagnostic bundle for a service", "Diagnostics",
            "Read-only collection of configuration, telemetry and dependency state for a named service.",
            ChangeRisk.Low, false, new[] { "svc-dns", "idp-fed", "app-api" }, "INC-4412", 12, "Completed — bundle sealed", 148,
            new[]
            {
                Step("Capture target service", "Request", "Service identifier and collection window.", 1.0),
                Step("Evaluate execution policy", "Policy", "Read-only collection is permitted for any operator role.", 1.4),
                Step("Pre-check: collector reachability", "Pre-check", "Confirms telemetry and configuration endpoints answer.", 2.0),
                Step("Simulate: collection scope", "Simulate", "Lists exactly which artefacts will be gathered and their approximate size.", 2.2),
                Step("Collect configuration state", "Execute", "Reads running configuration. No write occurs at any point.", 2.8),
                Step("Collect telemetry window", "Execute", "Extracts the metric and log window around the incident start.", 3.0),
                Step("Collect dependency state", "Execute", "Captures the resolved dependency graph and per-edge health.", 2.4),
                Step("Verify: bundle integrity", "Verify", "Bundle manifest hashes match the collected artefacts.", 2.0),
                Step("Seal evidence record", "Evidence", "Bundle digest written to the evidence ledger.", 1.6)
            });

        Rb("RB-058", "Fail over application delivery tier", "Network",
            "Moves the active application delivery node to its partner and proves service continuity.",
            ChangeRisk.Critical, true, new[] { "net-lb", "app-api", "app-web" }, null, null, "Never run", 0,
            new[]
            {
                Step("Capture failover target", "Request", "Cluster, active node and intended standby.", 1.2),
                Step("Evaluate execution policy", "Policy", "Requires two approvers and refuses to run during a declared major incident on the same path.", 2.0),
                Step("Pre-check: standby health", "Pre-check", "Standby must be fully synchronised and healthy.", 2.6),
                Step("Pre-check: session table capacity", "Pre-check", "Standby must have headroom for the current connection count.", 2.4),
                Step("Pre-check: no conflicting incident", "Pre-check", "Blocks when the target path is already inside an open major incident.", 2.2),
                Step("Simulate: connection drain profile", "Simulate", "Models how many connections reset versus drain gracefully.", 3.0),
                Step("Await dual authorisation", "Approve", "Two named approvers required.", 2.4, approval: true),
                Step("Drain active node", "Execute", "Stops new connections and allows in-flight requests to complete.", 3.4, mutating: true),
                Step("Promote standby", "Execute", "Standby takes the service address.", 2.8, mutating: true),
                Step("Verify: synthetic transactions pass", "Verify", "Both external services must return HTTP 200.", 3.0),
                Step("Verify: no elevated error rate", "Verify", "Five minute rolling error rate stays below objective.", 3.0),
                Step("Seal evidence record", "Evidence", "Failover record sealed to the ledger.", 1.6)
            });
    }
}
