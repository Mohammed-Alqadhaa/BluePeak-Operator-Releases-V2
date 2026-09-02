namespace BluePeak.Domain.Seed;

public static partial class EstateSeed
{
    private static TimelineEvent Ev(double minutesAgo, string actor, string text, string channel = "system",
        Severity weight = Severity.Info, string? evidenceId = null)
        => new()
        {
            At = Anchor.AddMinutes(-minutesAgo),
            Actor = actor,
            Text = text,
            Channel = channel,
            Weight = weight,
            EvidenceId = evidenceId
        };

    private static void BuildOperations(EstateModel m)
    {
        // ------------------------------------------------------------------ Incidents
        var inc4412 = new Incident
        {
            Id = "INC-4412",
            Title = "Partner API returning 5xx on authenticated endpoints",
            Severity = Severity.Critical,
            State = IncidentState.Identified,
            Commander = "R. Achebe",
            StartedAt = Anchor.AddMinutes(-74),
            DetectedAt = Anchor.AddMinutes(-68),
            Impact = "All partner integrations failing token introspection. 14 partner tenants affected, "
                   + "including two contractual availability commitments. Customer portal sign-in degraded.",
            RootCauseServiceId = "svc-dns",
            SuspectedChangeId = "CHG-2291",
            UsersAffected = 2140,
            Workstream = "Infrastructure"
        };
        inc4412.AffectedServiceIds.AddRange(new[] { "app-api", "app-web", "app-collab", "idp-fed", "svc-dns" });
        inc4412.LinkedTicketIds.AddRange(new[] { "TKT-88214", "TKT-88219", "TKT-88223", "TKT-88231" });
        inc4412.EvidenceIds.AddRange(new[] { "EV-1001", "EV-1002", "EV-1003" });
        inc4412.Timeline.AddRange(new[]
        {
            Ev(76, "Change: CHG-2291", "Conditional forwarder consolidation applied to resolver pair", "change", Severity.Info),
            Ev(74, "svc-dns", "Resolver dns-r02 begins answering SERVFAIL for federation zone", "telemetry", Severity.High, "EV-1001"),
            Ev(68, "Telemetry Pipeline", "Synthetic check partner-api/token failed 3 consecutive intervals", "detection", Severity.High),
            Ev(66, "Service Desk", "First user contact: partner portal sign-in failure", "intake", Severity.Medium),
            Ev(61, "R. Achebe", "Declared major incident, assumed incident command", "human", Severity.High),
            Ev(52, "Diagnostics", "Dependency walk isolated first failure to svc-dns (SERVFAIL)", "analysis", Severity.High, "EV-1002"),
            Ev(44, "R. Achebe", "Suspected causal change CHG-2291 linked; change owner engaged", "human", Severity.Medium),
            Ev(31, "Automation", "Runbook RB-014 pre-check completed: rollback is safe and reversible", "automation", Severity.Info, "EV-1003"),
            Ev(12, "Change Board", "Emergency change CHG-2304 raised for forwarder restore, awaiting approval", "change", Severity.Medium)
        });
        m.Incidents.Add(inc4412);

        var inc4415 = new Incident
        {
            Id = "INC-4415",
            Title = "Virtual desktop sessions dropping in Campus C",
            Severity = Severity.High,
            State = IncidentState.Mitigating,
            Commander = "T. Lindqvist",
            StartedAt = Anchor.AddMinutes(-128),
            DetectedAt = Anchor.AddMinutes(-124),
            Impact = "Approximately 180 desktop users in Building C experiencing session disconnects every 4-9 minutes.",
            RootCauseServiceId = "net-dist-c",
            UsersAffected = 180,
            Workstream = "Network"
        };
        inc4415.AffectedServiceIds.AddRange(new[] { "app-vdi", "net-dist-c" });
        inc4415.LinkedTicketIds.AddRange(new[] { "TKT-88198", "TKT-88205" });
        inc4415.EvidenceIds.Add("EV-1010");
        inc4415.Timeline.AddRange(new[]
        {
            Ev(128, "net-dist-c", "Interface Gi1/0/47 link state transitions detected", "telemetry", Severity.Medium),
            Ev(124, "Telemetry Pipeline", "Port-channel member flap threshold exceeded (>20 in 5 min)", "detection", Severity.High),
            Ev(120, "Service Desk", "Cluster of 11 desktop disconnect contacts from Building C", "intake", Severity.Medium),
            Ev(96, "T. Lindqvist", "Optic transceiver suspected; spare staged on site", "human", Severity.Medium, "EV-1010"),
            Ev(38, "T. Lindqvist", "Member interface administratively shut, channel running degraded on single member", "human", Severity.Medium),
            Ev(35, "Telemetry Pipeline", "Flap rate returned to zero; residual capacity risk remains", "telemetry", Severity.Info)
        });
        m.Incidents.Add(inc4415);

        var inc4409 = new Incident
        {
            Id = "INC-4409",
            Title = "Backup window overrun on finance volumes",
            Severity = Severity.Medium,
            State = IncidentState.Monitoring,
            Commander = "S. Okonkwo",
            StartedAt = Anchor.AddHours(-19),
            DetectedAt = Anchor.AddHours(-19),
            Impact = "Nightly protection job for ERP volumes exceeded window by 2h 40m. No data loss; RPO still met.",
            RootCauseServiceId = "ctl-backup",
            UsersAffected = 0,
            Workstream = "Platform"
        };
        inc4409.AffectedServiceIds.AddRange(new[] { "ctl-backup", "app-erp" });
        inc4409.Timeline.AddRange(new[]
        {
            Ev(1140, "ctl-backup", "Protection job finance-nightly exceeded 8h window", "telemetry", Severity.Medium),
            Ev(1080, "S. Okonkwo", "Throttle attributed to concurrent array rebuild; job completed successfully", "human", Severity.Info),
            Ev(240, "S. Okonkwo", "Monitoring next window before closure", "human", Severity.Info)
        });
        m.Incidents.Add(inc4409);

        // Recently closed incidents. A queue with nothing behind it reads as a demo; these give
        // the workspace a realistic recent history and something to compare against.
        void Resolved(string id, string title, Severity severity, double startedHoursAgo, double durationMinutes,
            string commander, string workstream, string cause, string impact, int users, string[] services)
        {
            var incident = new Incident
            {
                Id = id,
                Title = title,
                Severity = severity,
                State = IncidentState.Resolved,
                Commander = commander,
                StartedAt = Anchor.AddHours(-startedHoursAgo),
                DetectedAt = Anchor.AddHours(-startedHoursAgo).AddMinutes(3),
                MitigatedAt = Anchor.AddHours(-startedHoursAgo).AddMinutes(durationMinutes),
                Impact = impact,
                RootCauseServiceId = cause,
                UsersAffected = users,
                Workstream = workstream
            };
            incident.AffectedServiceIds.AddRange(services);
            incident.Timeline.Add(Ev(startedHoursAgo * 60, "Telemetry Pipeline", "Detected", "telemetry", severity));
            incident.Timeline.Add(Ev(startedHoursAgo * 60 - durationMinutes, commander, "Mitigated and verified", "human"));
            incident.Timeline.Add(Ev(startedHoursAgo * 60 - durationMinutes - 20, commander, "Resolved after verification window", "human"));
            m.Incidents.Add(incident);
        }

        Resolved("INC-4404", "Certificate renewal failed on the partner API front end", Severity.High, 31, 48,
            "H. Nowak", "Security", "svc-pki",
            "Automated renewal failed silently; the certificate was reissued manually before expiry.", 0,
            new[] { "svc-pki", "app-api" });

        Resolved("INC-4398", "Mail relay queue backlog after upstream throttling", Severity.Medium, 52, 96,
            "Messaging Team", "Platform", "svc-smtp",
            "Outbound mail delayed by up to 40 minutes. No messages were lost.", 310,
            new[] { "svc-smtp", "app-collab" });

        Resolved("INC-4391", "Storage array controller failover during firmware update", Severity.High, 76, 22,
            "S. Okonkwo", "Platform", "fnd-storage",
            "Controller failover took 22 minutes rather than the expected 90 seconds. No data loss.", 0,
            new[] { "fnd-storage", "app-erp", "app-files" });

        Resolved("INC-4386", "Campus A wireless authentication failures", Severity.Medium, 98, 64,
            "T. Lindqvist", "Network", "idp-nac",
            "RADIUS certificate chain incomplete after a policy edit. 220 devices could not associate.", 220,
            new[] { "idp-nac", "net-dist-a" });

        // ------------------------------------------------------------------ Tickets
        void Tkt(string id, string subject, string requester, string dept, string channel, Severity pri, TicketState state,
            string assignee, string queue, double openedMinutes, double slaMinutes, string? svc, string? inc, string summary,
            string[]? similar = null, TimelineEvent[]? timeline = null, string[]? evidence = null)
        {
            var t = new Ticket
            {
                Id = id,
                Subject = subject,
                Requester = requester,
                Department = dept,
                Channel = channel,
                Priority = pri,
                State = state,
                Assignee = assignee,
                Queue = queue,
                OpenedAt = Anchor.AddMinutes(-openedMinutes),
                SlaDueAt = Anchor.AddMinutes(-openedMinutes + slaMinutes),
                LinkedServiceId = svc,
                LinkedIncidentId = inc,
                Summary = summary
            };
            if (similar is not null) t.SimilarTicketIds.AddRange(similar);
            if (timeline is not null) t.Timeline.AddRange(timeline);
            if (evidence is not null) t.EvidenceIds.AddRange(evidence);
            m.Tickets.Add(t);
        }

        Tkt("TKT-88231", "Partner portal returns 'service unavailable' on login", "M. Osei", "Partner Operations", "Phone",
            Severity.Critical, TicketState.Escalated, "R. Achebe", "Incident Response", 22, 60, "app-api", "INC-4412",
            "Caller is the integration lead for a tier-1 partner. Every authenticated call has returned HTTP 503 since "
          + "approximately 07:10. Unauthenticated status endpoint responds normally.",
            new[] { "TKT-88223", "TKT-88219" },
            new[]
            {
                Ev(22, "M. Osei", "Reported total loss of partner API access", "intake", Severity.Critical),
                Ev(21, "Service Desk L1", "Matched to open major incident INC-4412; escalated without re-triage", "human", Severity.High),
                Ev(19, "R. Achebe", "Confirmed the caller is inside the known blast radius; no separate diagnosis needed", "human")
            },
            new[] { "EV-1002" });

        Tkt("TKT-88223", "Cannot sign in to customer portal — redirect loop", "J. Whitfield", "Customer Service", "Portal",
            Severity.High, TicketState.InProgress, "D. Marchetti", "Service Desk L2", 41, 120, "app-web", "INC-4412",
            "Sign-in redirects to the identity provider and returns to the portal without a session. Reproduced on two "
          + "browsers and one managed device. Anonymous pages load normally.",
            new[] { "TKT-88231" },
            new[]
            {
                Ev(41, "J. Whitfield", "Submitted via self-service portal with screen recording", "intake"),
                Ev(38, "Service Desk L1", "Reproduced; symptom matches federation metadata failure", "human", Severity.Medium),
                Ev(35, "D. Marchetti", "Linked to INC-4412; holding for infrastructure resolution", "human")
            });

        Tkt("TKT-88219", "Teams meeting join delayed by 30-60 seconds", "A. Ferreira", "Legal", "Chat",
            Severity.Medium, TicketState.Pending, "D. Marchetti", "Service Desk L2", 49, 480, "app-collab", "INC-4412",
            "Meeting join eventually succeeds but takes far longer than normal. Presence indicators are stale.",
            null,
            new[]
            {
                Ev(49, "A. Ferreira", "Reported via collaboration chat bot", "intake"),
                Ev(46, "Service Desk L1", "Second contact with same symptom in 20 minutes; suspected common cause", "human"),
                Ev(44, "D. Marchetti", "Attached to INC-4412 as downstream consequence", "human")
            });

        Tkt("TKT-88214", "API integration failing for EMEA partners", "Integration Monitoring", "Integration Team", "Email",
            Severity.Critical, TicketState.Escalated, "R. Achebe", "Incident Response", 64, 60, "app-api", "INC-4412",
            "Automated intake from partner integration monitoring. 41 consecutive token introspection failures.",
            new[] { "TKT-88231" },
            new[]
            {
                Ev(64, "Integration Monitoring", "Automated ticket raised from synthetic monitor failure", "intake", Severity.Critical),
                Ev(63, "Service Desk L1", "Auto-classified as Availability / External Integration", "system"),
                Ev(61, "R. Achebe", "Promoted to major incident INC-4412", "human", Severity.Critical)
            },
            new[] { "EV-1001" });

        Tkt("TKT-88205", "Desktop session keeps disconnecting", "P. Nakamura", "Claims Processing", "Phone",
            Severity.High, TicketState.InProgress, "T. Lindqvist", "Network Engineering", 112, 240, "app-vdi", "INC-4415",
            "User is disconnected from the virtual desktop every few minutes and must reconnect. Loses unsaved work in the claims tool.",
            new[] { "TKT-88198" },
            new[]
            {
                Ev(112, "P. Nakamura", "Third call today about the same problem", "intake", Severity.High),
                Ev(110, "Service Desk L1", "Location Building C, 3rd floor — matched to cluster", "human"),
                Ev(108, "T. Lindqvist", "Correlated to distribution switch flapping; linked to INC-4415", "human", Severity.High)
            });

        Tkt("TKT-88198", "Several people on our floor keep losing their desktops", "L. Brennan", "Claims Processing", "Walk-up",
            Severity.High, TicketState.Triage, "Unassigned", "Service Desk L1", 126, 240, "app-vdi", "INC-4415",
            "Team lead reporting on behalf of approximately 12 staff on the same floor.",
            new[] { "TKT-88205" },
            new[]
            {
                Ev(126, "L. Brennan", "Walk-up report at the service desk", "intake", Severity.High),
                Ev(124, "Service Desk L1", "Recognised as a cluster rather than individual faults", "human", Severity.High)
            });

        Tkt("TKT-88240", "Request: additional storage quota for design share", "K. Doyle", "Marketing", "Portal",
            Severity.Low, TicketState.New, "Unassigned", "Service Desk L1", 8, 2880, "app-files", null,
            "Standard quota increase request for the shared design library. 500 GB requested.",
            null,
            new[] { Ev(8, "K. Doyle", "Submitted standard request", "intake") });

        Tkt("TKT-88238", "New starter account and equipment — 14 Sept", "HR Onboarding", "People Team", "Automation",
            Severity.Low, TicketState.InProgress, "Provisioning Bot", "Provisioning", 14, 4320, "idp-ad", null,
            "Automated onboarding request. Account, mailbox, laptop and building access for a new analyst.",
            null,
            new[]
            {
                Ev(14, "HR Onboarding", "Onboarding record created downstream of HR system", "intake"),
                Ev(12, "Provisioning Bot", "Directory account staged, awaiting manager confirmation", "automation")
            });

        Tkt("TKT-88236", "Printer on 2nd floor shows offline", "N. Adeyemi", "Facilities", "Phone",
            Severity.Low, TicketState.Resolved, "D. Marchetti", "Service Desk L1", 190, 480, "app-files", null,
            "Print queue stalled; spooler restarted and test page confirmed.",
            null,
            new[]
            {
                Ev(190, "N. Adeyemi", "Reported printer offline", "intake"),
                Ev(176, "D. Marchetti", "Restarted print spooler on the queue server", "human"),
                Ev(174, "D. Marchetti", "Test page confirmed by requester; resolved", "human"),
                Ev(172, "Service Desk", "Resolution verified with requester, awaiting closure window", "system")
            },
            new[] { "EV-1020" });

        Tkt("TKT-88229", "Laptop very slow after morning update", "C. Vasquez", "Finance", "Portal",
            Severity.Medium, TicketState.Triage, "Unassigned", "Service Desk L1", 34, 480, null, null,
            "Device performance degraded after a scheduled update ring. No dependency on any open incident.",
            null,
            new[]
            {
                Ev(34, "C. Vasquez", "Reported general slowness", "intake"),
                Ev(30, "Service Desk L1", "Deferred pending major incident load", "human")
            });

        Tkt("TKT-88226", "MFA prompt not arriving on phone", "S. Kaur", "Procurement", "Phone",
            Severity.Medium, TicketState.Pending, "D. Marchetti", "Service Desk L2", 44, 240, "idp-mfa", null,
            "Push notification not received. User can complete sign-in with a one-time code, so impact is inconvenience rather than lockout.",
            null,
            new[]
            {
                Ev(44, "S. Kaur", "Reported missing push prompts", "intake"),
                Ev(40, "D. Marchetti", "Re-registered device token; awaiting user confirmation", "human")
            });
    }

    private static void BuildActivityFeed(EstateModel m)
    {
        m.ActivityFeed.AddRange(new[]
        {
            Ev(3, "Automation", "RB-014 dry run completed — 4 of 4 pre-checks passed", "automation"),
            Ev(6, "Change Board", "CHG-2304 emergency approval requested by R. Achebe", "change", Severity.Medium),
            Ev(9, "Security Operations", "CASE-118 escalated to containment review", "security", Severity.High),
            Ev(12, "Service Desk", "TKT-88231 escalated to Incident Response", "intake", Severity.High),
            Ev(18, "Telemetry Pipeline", "app-api error budget for the month is 92% consumed", "telemetry", Severity.High),
            Ev(24, "Detection", "Detection ANL-0042 fired for mailbox rule creation", "security", Severity.High),
            Ev(31, "Automation", "RB-014 pre-check produced evidence EV-1003", "automation"),
            Ev(35, "Network Engineering", "Campus C port-channel member shut to stop flapping", "human", Severity.Medium),
            Ev(44, "Diagnostics", "Dependency walk DX-2201 completed for partner API request path", "analysis", Severity.High),
            Ev(52, "Identity Engineering", "Federation metadata cache extended to survive resolver outage", "human"),
            Ev(61, "R. Achebe", "INC-4412 declared major incident", "human", Severity.Critical),
            Ev(74, "svc-dns", "Resolver dns-r02 answering SERVFAIL for federation zone", "telemetry", Severity.Critical),
            Ev(76, "Change: CHG-2291", "Conditional forwarder consolidation applied", "change", Severity.Info)
        });
    }
}
