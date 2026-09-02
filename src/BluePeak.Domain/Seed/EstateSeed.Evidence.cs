using System.Security.Cryptography;
using System.Text;

namespace BluePeak.Domain.Seed;

public static partial class EstateSeed
{
    private static string Digest(string material)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static void BuildEvidenceAndDiagnostics(EstateModel m)
    {
        void Rec(string id, string claim, string source, string check, EvidenceResult result, double minutesAgo,
            EvidenceAuthority authority, string expected, string observed, string scope, bool preserved, string? subject, string collector)
        {
            m.Evidence.Add(new EvidenceRecord
            {
                Id = id,
                Claim = claim,
                Source = source,
                Check = check,
                Result = result,
                CapturedAt = Anchor.AddMinutes(-minutesAgo),
                Authority = authority,
                Expected = expected,
                Observed = observed,
                Scope = scope,
                Preserved = preserved,
                SubjectId = subject,
                Collector = collector,
                Digest = Digest(id + claim + observed)
            });
        }

        Rec("EV-1001", "The DNS resolver pair stopped answering for the federation zone at 07:04",
            "Resolver query log — dns-r01, dns-r02", "Query response code distribution over a 5 minute window",
            EvidenceResult.Fail, 74, EvidenceAuthority.PlatformAttested,
            "NOERROR for federation metadata zone", "SERVFAIL on 100% of 412 queries from both resolvers",
            "Incident INC-4412", true, "svc-dns", "telemetry-pipeline");

        Rec("EV-1002", "The first failing component on the partner API request path is name resolution, not the API itself",
            "Diagnostics dependency walk DX-2201", "Ordered hop evaluation with expected and actual per hop",
            EvidenceResult.Fail, 52, EvidenceAuthority.LocalOperator,
            "Every hop returns its expected result", "Hops 1-3 pass, hop 4 (DNS resolution) returns SERVFAIL, hops 5-6 not reached",
            "Incident INC-4412", true, "svc-dns", "operator");

        Rec("EV-1003", "Rolling back the forwarder change is safe and reversible within the stated backout time",
            "Runbook RB-014 pre-check", "Configuration drift and rollback viability assertions",
            EvidenceResult.Pass, 31, EvidenceAuthority.PlatformAttested,
            "Previous revision present and restorable in under 5 minutes", "Revision r-2290 present on both resolvers; simulated reload completed in 41 s",
            "Incident INC-4412", true, "svc-dns", "automation-control-plane");

        Rec("EV-1010", "Interface Gi1/0/47 is the source of the Campus C instability",
            "Switch interface counters — net-dist-c", "Link state transition count over 30 minutes",
            EvidenceResult.Fail, 96, EvidenceAuthority.PlatformAttested,
            "0 transitions", "214 transitions on Gi1/0/47; 0 on all other members",
            "Incident INC-4415", true, "net-dist-c", "telemetry-pipeline");

        Rec("EV-1020", "The reported print queue fault was resolved and confirmed by the requester",
            "Service desk record TKT-88236", "Requester confirmation of a successful test page",
            EvidenceResult.Pass, 174, EvidenceAuthority.LocalOperator,
            "Requester confirms resolution", "Requester confirmed test page printed at 05:58",
            "Ticket TKT-88236", false, "TKT-88236", "operator");

        Rec("EV-1030", "A refresh token issued to the finance reporting client was redeemed from an unenrolled device",
            "Identity sign-in logs", "Token redemption correlation by token identifier and device identifier",
            EvidenceResult.Fail, 63, EvidenceAuthority.PlatformAttested,
            "Token redeemed only from enrolled devices", "Redemption from device with no enrolment record, source 203.0.113.47",
            "Case CASE-118", true, "ENT-T-551", "security-analytics");

        Rec("EV-1031", "The token was issued without proof-of-possession binding",
            "Token issuance record", "Client authentication policy assertion at time of issuance",
            EvidenceResult.Fail, 38, EvidenceAuthority.PlatformAttested,
            "Refresh tokens bound to the issuing device", "Bearer token, no binding claim present",
            "Case CASE-118", true, "ENT-T-551", "security-analytics");

        Rec("EV-1032", "A mailbox rule hiding financial correspondence was created from the suspect session",
            "Mailbox audit log", "Rule creation event with full rule definition preserved",
            EvidenceResult.Fail, 24, EvidenceAuthority.PlatformAttested,
            "No hiding rules present", "Rule with blank display name moving invoice/payment/remittance matches to RSS Feeds",
            "Case CASE-118", true, "ENT-M-014", "security-analytics");

        Rec("EV-1040", "Internal zones resolved correctly after CHG-2291",
            "Change verification record CHG-2291", "Synthetic query set against both resolvers",
            EvidenceResult.Pass, 71, EvidenceAuthority.ProjectAuthoritative,
            "NOERROR for all internal zones", "20 of 20 queries returned NOERROR",
            "Change CHG-2291", true, "CHG-2291", "change-implementer");

        Rec("EV-1041", "Conditional forwarder zones were not verified after CHG-2291",
            "Change verification record CHG-2291", "Post-change verification checklist completeness",
            EvidenceResult.Inconclusive, 70, EvidenceAuthority.ProjectAuthoritative,
            "All zone classes verified", "Conditional forwarder class not enumerated; check recorded as not executed",
            "Change CHG-2291", true, "CHG-2291", "change-implementer");

        Rec("EV-1050", "The operator workstation rendered the simulator at the configured quality without frame loss",
            "Local session telemetry", "Frame interval sampling during a full journey playback",
            EvidenceResult.Pass, 2, EvidenceAuthority.LocalOperator,
            "Median frame interval under 20 ms", "Median 14.8 ms across 1 842 frames",
            "Session", false, null, "operator");

        // ------------------------------------------------------------------ Diagnostic paths
        var dx2201 = new DiagnosticPath
        {
            Id = "DX-2201",
            Name = "Partner API authenticated request",
            Request = "POST https://partner-api.bluepeak.example/v2/orders  (bearer token)",
            Origin = "Synthetic probe — EMEA partner edge",
            RunAt = Anchor.AddMinutes(-52),
            FirstFailureServiceId = "svc-dns",
            LinkedIncidentId = "INC-4412",
            JourneyId = "journey.dns",
            Conclusion = "Name resolution is the first failing component. The API gateway and delivery tier are healthy and are "
                       + "returning 5xx because they cannot complete token introspection. Restoring the conditional forwarder "
                       + "zone should recover the whole path without touching the application."
        };
        dx2201.BlastRadiusServiceIds.AddRange(new[] { "app-api", "app-web", "app-collab" });
        dx2201.Hops.AddRange(new[]
        {
            new DiagnosticHop { Index = 1, ServiceId = "net-edge", Label = "Perimeter admission", Protocol = "TLS 1.3", Operation = "Handshake and policy match", Expected = "Session established, policy allow", Actual = "Session established in 34 ms, policy allow", Result = HealthState.Healthy, ElapsedMs = 34, Reasoning = "Edge is not implicated: the request is admitted normally." },
            new DiagnosticHop { Index = 2, ServiceId = "net-lb", Label = "Application delivery", Protocol = "HTTPS", Operation = "Virtual server selection and pool member health", Expected = "Pool member selected, member healthy", Actual = "Member api-02 selected, health monitor green", Result = HealthState.Healthy, ElapsedMs = 3, Reasoning = "Delivery tier has healthy members, so this is not a capacity or pool problem." },
            new DiagnosticHop { Index = 3, ServiceId = "app-api", Label = "Gateway request handling", Protocol = "HTTP/2", Operation = "Route match and token introspection start", Expected = "Route matched, introspection initiated", Actual = "Route matched, introspection initiated", Result = HealthState.Healthy, ElapsedMs = 2, Reasoning = "The gateway process is running and accepting work. Its failure is downstream of this point." },
            new DiagnosticHop { Index = 4, ServiceId = "svc-dns", Label = "Resolve federation metadata host", Protocol = "DNS", Operation = "A record lookup for the federation metadata endpoint", Expected = "NOERROR with an A record", Actual = "SERVFAIL after 2 800 ms and two retries", Result = HealthState.Critical, ElapsedMs = 2840, IsFirstFailure = true, EvidenceId = "EV-1001", Reasoning = "This is the first hop where expected and actual diverge. The conditional forwarder zone for the federation domain is absent from both resolvers following CHG-2291." },
            new DiagnosticHop { Index = 5, ServiceId = "idp-fed", Label = "Token introspection", Protocol = "OAuth 2.0", Operation = "POST /introspect", Expected = "HTTP 200 with active token assertion", Actual = "Not attempted — endpoint address unresolved", Result = HealthState.Critical, ElapsedMs = 0, IsDownstreamConsequence = true, Reasoning = "Consequence, not cause. The federation service is running; the caller simply cannot find it." },
            new DiagnosticHop { Index = 6, ServiceId = "app-api", Label = "Response to partner", Protocol = "HTTP/2", Operation = "Return result to caller", Expected = "HTTP 200 with order accepted", Actual = "HTTP 503 upstream authorisation unavailable", Result = HealthState.Critical, ElapsedMs = 2846, IsDownstreamConsequence = true, Reasoning = "The observed symptom. Acting here would mask the fault rather than fix it." }
        });
        m.DiagnosticPaths.Add(dx2201);

        var dx2205 = new DiagnosticPath
        {
            Id = "DX-2205",
            Name = "Campus C virtual desktop session",
            Request = "PCoIP session establishment — Building C, 3rd floor",
            Origin = "Endpoint agent — FIN-LT-2207",
            RunAt = Anchor.AddMinutes(-104),
            FirstFailureServiceId = "net-dist-c",
            LinkedIncidentId = "INC-4415",
            JourneyId = "journey.network",
            Conclusion = "A single port-channel member is flapping and taking a share of session traffic with it. The desktop "
                       + "platform itself is healthy. Draining the member stops the disconnects at the cost of redundancy."
        };
        dx2205.BlastRadiusServiceIds.Add("app-vdi");
        dx2205.Hops.AddRange(new[]
        {
            new DiagnosticHop { Index = 1, ServiceId = "net-dist-c", Label = "Access layer attachment", Protocol = "802.1Q", Operation = "Port up, VLAN assignment", Expected = "Interface up, VLAN 240", Actual = "Interface up, VLAN 240", Result = HealthState.Healthy, ElapsedMs = 1, Reasoning = "The endpoint attaches normally." },
            new DiagnosticHop { Index = 2, ServiceId = "net-dist-c", Label = "Uplink port-channel", Protocol = "LACP", Operation = "Member selection and forwarding", Expected = "All members forwarding, zero transitions", Actual = "Member Gi1/0/47 transitioned 214 times in 30 min", Result = HealthState.Degraded, ElapsedMs = 18, IsFirstFailure = true, EvidenceId = "EV-1010", Reasoning = "First divergence. Flows hashed onto the failing member are interrupted each time it drops." },
            new DiagnosticHop { Index = 3, ServiceId = "net-core", Label = "Core transit", Protocol = "802.1Q", Operation = "Forward to compute fabric", Expected = "Forwarded, no drops", Actual = "Forwarded, no drops", Result = HealthState.Healthy, ElapsedMs = 1, Reasoning = "Core is unaffected." },
            new DiagnosticHop { Index = 4, ServiceId = "idp-ad", Label = "Session authentication", Protocol = "Kerberos", Operation = "Service ticket for desktop broker", Expected = "Ticket issued", Actual = "Ticket issued in 6 ms", Result = HealthState.Healthy, ElapsedMs = 6, Reasoning = "Identity is not implicated." },
            new DiagnosticHop { Index = 5, ServiceId = "app-vdi", Label = "Desktop session", Protocol = "PCoIP", Operation = "Establish and hold session", Expected = "Session stable", Actual = "Session drops on member transition", Result = HealthState.Degraded, ElapsedMs = 88, IsDownstreamConsequence = true, Reasoning = "Consequence of the flapping uplink, not a desktop platform fault." }
        });
        m.DiagnosticPaths.Add(dx2205);

        var dx2210 = new DiagnosticPath
        {
            Id = "DX-2210",
            Name = "Interactive sign-in to the customer portal",
            Request = "GET https://portal.bluepeak.example/  → SAML redirect",
            Origin = "Synthetic probe — managed workstation",
            RunAt = Anchor.AddMinutes(-38),
            FirstFailureServiceId = "svc-dns",
            LinkedIncidentId = "INC-4412",
            JourneyId = "journey.auth",
            Conclusion = "Sign-in succeeds only while the federation service is serving from its metadata cache. The cache expires "
                       + "in approximately 41 minutes, after which this path fails completely. This is a deadline, not a stable state."
        };
        dx2210.BlastRadiusServiceIds.AddRange(new[] { "app-web", "app-crm", "app-collab" });
        dx2210.Hops.AddRange(new[]
        {
            new DiagnosticHop { Index = 1, ServiceId = "net-lb", Label = "Portal front end", Protocol = "HTTPS", Operation = "Serve portal shell", Expected = "HTTP 200", Actual = "HTTP 200 in 41 ms", Result = HealthState.Healthy, ElapsedMs = 41, Reasoning = "Anonymous content is served correctly, which is why some users report the site as 'up'." },
            new DiagnosticHop { Index = 2, ServiceId = "app-web", Label = "Authentication redirect", Protocol = "SAML 2.0", Operation = "Issue AuthnRequest", Expected = "302 to federation service", Actual = "302 to federation service", Result = HealthState.Healthy, ElapsedMs = 4, Reasoning = "The application behaves correctly." },
            new DiagnosticHop { Index = 3, ServiceId = "idp-fed", Label = "Federation processing", Protocol = "SAML 2.0", Operation = "Validate request, sign assertion", Expected = "Assertion signed with current key", Actual = "Assertion signed from cached key material, cache expires in 41 min", Result = HealthState.Degraded, ElapsedMs = 1180, IsFirstFailure = false, Reasoning = "Degraded but functioning. It is masking the underlying fault and creating a hidden deadline." },
            new DiagnosticHop { Index = 4, ServiceId = "svc-dns", Label = "Metadata refresh resolution", Protocol = "DNS", Operation = "Resolve metadata host for key rollover", Expected = "NOERROR with an A record", Actual = "SERVFAIL", Result = HealthState.Critical, ElapsedMs = 2800, IsFirstFailure = true, EvidenceId = "EV-1001", Reasoning = "Same first failure as the partner API path. One fault, two symptoms." },
            new DiagnosticHop { Index = 5, ServiceId = "app-web", Label = "Session establishment", Protocol = "SAML 2.0", Operation = "Consume assertion, set session", Expected = "Session established", Actual = "Intermittent — succeeds while cache is warm", Result = HealthState.Degraded, ElapsedMs = 1225, IsDownstreamConsequence = true, Reasoning = "Intermittent success is the most misleading symptom in this incident." }
        });
        m.DiagnosticPaths.Add(dx2210);
    }
}
