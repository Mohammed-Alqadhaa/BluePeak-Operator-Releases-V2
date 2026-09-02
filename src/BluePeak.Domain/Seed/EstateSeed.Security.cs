namespace BluePeak.Domain.Seed;

public static partial class EstateSeed
{
    private static void BuildSecurity(EstateModel m)
    {
        // ------------------------------------------------------------------ Entities
        void Ent(string id, EntityKind kind, string name, int risk, string context, double firstSeenMin, double lastSeenMin,
            bool managed, (string, string)[] attrs, string[] related)
        {
            var e = new SecurityEntity
            {
                Id = id,
                Kind = kind,
                Name = name,
                RiskScore = risk,
                Context = context,
                FirstSeen = Anchor.AddMinutes(-firstSeenMin),
                LastSeen = Anchor.AddMinutes(-lastSeenMin),
                IsManaged = managed
            };
            foreach (var (k, v) in attrs) e.Attributes[k] = v;
            e.RelatedEntityIds.AddRange(related);
            m.Entities.Add(e);
        }

        Ent("ENT-U-014", EntityKind.User, "j.harding@bluepeak.example", 84,
            "Finance systems accountant. Privileged group membership added 6 days ago for quarter-end close.", 4320, 7, true,
            new[] { ("Department", "Finance"), ("Manager", "C. Vasquez"), ("Privileged", "Yes — Finance Reporting Admin"),
                    ("Last password change", "38 days ago"), ("Registered MFA", "Push + FIDO2 key") },
            new[] { "ENT-H-207", "ENT-H-902", "ENT-T-551", "ENT-M-014" });

        Ent("ENT-H-207", EntityKind.Host, "FIN-LT-2207", 22,
            "Corporate laptop, compliant, EDR healthy. Normal working location Building A.", 20160, 32, true,
            new[] { ("Platform", "Windows 11 24H2"), ("Compliance", "Compliant"), ("EDR", "Healthy — last check-in 4 min"),
                    ("Owner", "j.harding") },
            new[] { "ENT-U-014" });

        Ent("ENT-H-902", EntityKind.Host, "UNMANAGED-9f2c", 91,
            "Device not present in the endpoint inventory. First observed authenticating 63 minutes ago.", 63, 7, false,
            new[] { ("Platform", "Reported: Windows 10"), ("Compliance", "Not enrolled"), ("EDR", "Not present"),
                    ("Join type", "None — token-based access only") },
            new[] { "ENT-U-014", "ENT-IP-311" });

        Ent("ENT-IP-311", EntityKind.IpAddress, "203.0.113.47", 78,
            "Hosting-provider address space. No prior authentication history for this tenant.", 63, 7, false,
            new[] { ("ASN", "AS64511 — Example Hosting"), ("Geolocation", "Frankfurt, DE"), ("Reputation", "Anonymising infrastructure"),
                    ("Prior sightings", "None in 90 days") },
            new[] { "ENT-H-902" });

        Ent("ENT-T-551", EntityKind.Token, "rt_9c41…e7b2", 88,
            "OAuth refresh token issued to the finance reporting client 3 days ago; presented from a new device.", 4320, 7, false,
            new[] { ("Client", "Finance Reporting Web"), ("Scope", "Reports.Read.All offline_access"),
                    ("Issued", "3 days ago"), ("Binding", "None — bearer") },
            new[] { "ENT-U-014", "ENT-H-902" });

        Ent("ENT-M-014", EntityKind.Mailbox, "j.harding — Mailbox", 74,
            "Inbox rule created 24 minutes ago moving messages matching finance keywords to a rarely used folder.", 4320, 24, true,
            new[] { ("Rule name", "..."), ("Action", "Move to RSS Feeds, mark read"),
                    ("Match", "invoice OR payment OR remittance"), ("Created by", "Exchange Web Services session") },
            new[] { "ENT-U-014" });

        Ent("ENT-U-032", EntityKind.User, "svc-reporting@bluepeak.example", 41,
            "Service account for scheduled report extraction. Interactive sign-in is not expected.", 43200, 88, true,
            new[] { ("Type", "Service account"), ("Interactive sign-in", "Not expected"), ("Owner", "Enterprise Applications") },
            Array.Empty<string>());

        Ent("ENT-H-118", EntityKind.Host, "SRV-FILE-01", 35,
            "File services host. Elevated SMB enumeration observed from a finance workstation.", 43200, 51, true,
            new[] { ("Role", "File services"), ("Shares", "142"), ("EDR", "Healthy") },
            new[] { "ENT-U-032" });

        // ------------------------------------------------------------------ Alerts
        void Alert(string id, string rule, Severity sev, double firstMin, double lastMin, AlertStatus status, string assignee,
            int confidence, string tactic, string technique, string source, string? caseId, string[] entities, string detail, int signals)
        {
            var a = new SecurityAlert
            {
                Id = id,
                Rule = rule,
                Severity = sev,
                FirstSeen = Anchor.AddMinutes(-firstMin),
                LastSeen = Anchor.AddMinutes(-lastMin),
                Status = status,
                Assignee = assignee,
                Confidence = confidence,
                Tactic = tactic,
                Technique = technique,
                DataSource = source,
                CaseId = caseId,
                Detail = detail,
                SignalCount = signals
            };
            a.EntityIds.AddRange(entities);
            m.Alerts.Add(a);
        }

        Alert("ANL-0038", "Refresh token presented from previously unseen device", Severity.Critical, 63, 7,
            AlertStatus.Investigating, "N. Petrova", 88, "Credential Access", "T1550.001 — Application Access Token",
            "Identity sign-in logs", "CASE-118",
            new[] { "ENT-U-014", "ENT-H-902", "ENT-T-551", "ENT-IP-311" },
            "A refresh token issued to the finance reporting client three days ago was redeemed from a device with no "
          + "enrolment record and an address with no prior sign-in history for this tenant. The original device continues "
          + "to sign in normally, so this is an addition rather than a relocation.", 6);

        Alert("ANL-0041", "Impossible travel between sign-ins", Severity.High, 58, 11,
            AlertStatus.Investigating, "N. Petrova", 71, "Initial Access", "T1078 — Valid Accounts",
            "Identity sign-in logs", "CASE-118",
            new[] { "ENT-U-014", "ENT-IP-311" },
            "Successful sign-ins from Manchester and Frankfurt separated by 9 minutes. Travel is not physically possible. "
          + "The Manchester session is from the enrolled laptop and appears legitimate.", 2);

        Alert("ANL-0042", "Mailbox rule created that hides financial correspondence", Severity.High, 24, 24,
            AlertStatus.Investigating, "N. Petrova", 92, "Collection", "T1564.008 — Email Hiding Rules",
            "Mailbox audit", "CASE-118",
            new[] { "ENT-U-014", "ENT-M-014" },
            "An inbox rule with a blank display name was created that moves messages matching invoice, payment and "
          + "remittance keywords into a folder the user does not normally read. This pattern is strongly associated with "
          + "payment redirection fraud.", 1);

        Alert("ANL-0044", "Anomalous report export volume", Severity.Medium, 19, 9,
            AlertStatus.Triaged, "N. Petrova", 64, "Exfiltration", "T1567 — Exfiltration Over Web Service",
            "Application audit", "CASE-118",
            new[] { "ENT-U-014", "ENT-H-902" },
            "412 MB of reporting extracts downloaded in 10 minutes against a 30-day baseline of 6 MB per day for this account.", 3);

        Alert("ANL-0051", "Service account interactive sign-in", Severity.Medium, 88, 88,
            AlertStatus.Triaged, "K. Ibrahim", 55, "Persistence", "T1078.004 — Cloud Accounts",
            "Identity sign-in logs", null,
            new[] { "ENT-U-032" },
            "Interactive sign-in observed for an account designated non-interactive. Source is an internal jump host "
          + "used by Enterprise Applications during a scheduled maintenance window.", 1);

        Alert("ANL-0053", "Broad SMB share enumeration", Severity.Low, 51, 51,
            AlertStatus.Closed, "K. Ibrahim", 38, "Discovery", "T1135 — Network Share Discovery",
            "Endpoint telemetry", null,
            new[] { "ENT-H-118", "ENT-U-032" },
            "Enumeration matched the signature of the quarterly storage reporting job. Confirmed against the job schedule.", 1);

        Alert("ANL-0056", "Sign-in failures spike against federation endpoint", Severity.Medium, 70, 8,
            AlertStatus.Triaged, "K. Ibrahim", 47, "Credential Access", "T1110 — Brute Force",
            "Identity sign-in logs", null,
            new[] { "ENT-U-032" },
            "Failure volume rose sharply 70 minutes ago. Failure codes indicate a service-side error rather than credential "
          + "guessing, and the rise coincides exactly with INC-4412. Likely operational, not adversarial.", 4);

        Alert("ANL-0059", "Certificate about to expire on public endpoint", Severity.Low, 300, 300,
            AlertStatus.New, "Unassigned", 99, "Hygiene", "N/A", "Certificate inventory", null,
            Array.Empty<string>(),
            "TLS certificate for the partner API front end expires in 11 days.", 1);

        // ------------------------------------------------------------------ Case
        var case118 = new SecurityCase
        {
            Id = "CASE-118",
            Title = "Suspected token theft and payment redirection preparation — finance account",
            Severity = Severity.Critical,
            Owner = "N. Petrova",
            Status = AlertStatus.Investigating,
            OpenedAt = Anchor.AddMinutes(-57),
            Hypothesis = "A valid refresh token for the finance reporting client was obtained outside the managed estate and is "
                       + "being used from an unenrolled device. The mailbox rule and export volume indicate preparation for "
                       + "invoice redirection rather than opportunistic access.",
            Verdict = "True positive — likely; containment recommended"
        };
        case118.AlertIds.AddRange(new[] { "ANL-0038", "ANL-0041", "ANL-0042", "ANL-0044" });
        case118.EntityIds.AddRange(new[] { "ENT-U-014", "ENT-H-902", "ENT-IP-311", "ENT-T-551", "ENT-M-014", "ENT-H-207" });
        case118.EvidenceIds.AddRange(new[] { "EV-1030", "EV-1031", "EV-1032" });
        case118.Timeline.AddRange(new[]
        {
            Ev(63, "Detection", "ANL-0038 fired: refresh token redeemed from unseen device", "detection", Severity.Critical, "EV-1030"),
            Ev(58, "Detection", "ANL-0041 fired: impossible travel", "detection", Severity.High),
            Ev(57, "N. Petrova", "Case opened, alerts correlated on the shared user entity", "human", Severity.High),
            Ev(51, "N. Petrova", "Confirmed the enrolled laptop is still in normal use — this is an additional session", "human"),
            Ev(38, "N. Petrova", "Retrieved token issuance record; no device binding was applied by the client", "human", Severity.Medium, "EV-1031"),
            Ev(24, "Detection", "ANL-0042 fired: hiding mailbox rule created from the suspect session", "detection", Severity.High, "EV-1032"),
            Ev(19, "Detection", "ANL-0044 fired: report export volume 68x baseline", "detection", Severity.Medium),
            Ev(9, "N. Petrova", "Containment package prepared: revoke sessions, block token family, disable rule", "human", Severity.High),
            Ev(4, "K. Ibrahim", "Peer review requested before revocation — account is inside quarter-end close", "human", Severity.Medium)
        });
        case118.Tasks.AddRange(new[]
        {
            new ResponseTask { Name = "Correlate alerts to a single subject", Phase = "Triage", State = GateState.Passed, Owner = "N. Petrova", Detail = "Four alerts share the finance user entity." },
            new ResponseTask { Name = "Establish whether the enrolled device is compromised", Phase = "Scope", State = GateState.Passed, Owner = "N. Petrova", Detail = "EDR clean; no suspicious process lineage on FIN-LT-2207." },
            new ResponseTask { Name = "Determine token issuance path", Phase = "Scope", State = GateState.Passed, Owner = "N. Petrova", Detail = "Bearer refresh token, no proof-of-possession binding." },
            new ResponseTask { Name = "Identify data touched from the suspect session", Phase = "Scope", State = GateState.Running, Owner = "N. Petrova", Detail = "412 MB of extracts enumerated; classification in progress." },
            new ResponseTask { Name = "Peer review of containment package", Phase = "Contain", State = GateState.WaitingApproval, Owner = "K. Ibrahim", Detail = "Revocation affects an active quarter-end user." },
            new ResponseTask { Name = "Revoke refresh token family and active sessions", Phase = "Contain", State = GateState.Blocked, Owner = "N. Petrova", Detail = "Blocked on peer review." },
            new ResponseTask { Name = "Remove hiding mailbox rule and preserve a copy", Phase = "Contain", State = GateState.Blocked, Owner = "N. Petrova", Detail = "Rule definition must be preserved before deletion." },
            new ResponseTask { Name = "Confirm no payment instruction was altered", Phase = "Verify", State = GateState.Pending, Owner = "Finance Control", Detail = "Reconcile against the payments run." },
            new ResponseTask { Name = "Attest containment outcome to the evidence ledger", Phase = "Verify", State = GateState.Pending, Owner = "Assurance", Detail = "" }
        });
        m.Cases.Add(case118);

        var case115 = new SecurityCase
        {
            Id = "CASE-115",
            Title = "Scheduled reporting job flagged as discovery activity",
            Severity = Severity.Low,
            Owner = "K. Ibrahim",
            Status = AlertStatus.Closed,
            OpenedAt = Anchor.AddMinutes(-51),
            Hypothesis = "Enumeration originated from the quarterly storage reporting job.",
            Verdict = "Benign — detection tuned"
        };
        case115.AlertIds.Add("ANL-0053");
        case115.EntityIds.AddRange(new[] { "ENT-H-118", "ENT-U-032" });
        case115.Timeline.AddRange(new[]
        {
            Ev(51, "Detection", "ANL-0053 fired: broad share enumeration", "detection"),
            Ev(48, "K. Ibrahim", "Matched to storage reporting schedule; closed as benign", "human"),
            Ev(46, "K. Ibrahim", "Suppression scoped to the job identity only, expires in 90 days", "human")
        });
        m.Cases.Add(case115);
    }
}
