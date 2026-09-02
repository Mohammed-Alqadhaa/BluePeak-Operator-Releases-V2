namespace BluePeak.Domain.Seed;

/// <summary>
/// Builds the demonstration estate. The data is deterministic: the same estate is
/// produced on every launch so captures, tests and evidence digests are reproducible.
/// </summary>
public static partial class EstateSeed
{
    /// <summary>Fixed clock anchor. All relative times in the estate hang off this.</summary>
    public static DateTime Anchor { get; private set; }

    public static EstateModel Build(DateTime? now = null)
    {
        var clock = now ?? DateTime.Now;
        // Snap to the minute so repeated launches inside the same minute are identical.
        Anchor = new DateTime(clock.Year, clock.Month, clock.Day, clock.Hour, clock.Minute, 0, DateTimeKind.Local);

        var m = new EstateModel { Now = Anchor };
        BuildTopology(m);
        m.Index();
        BuildOperations(m);
        BuildSecurity(m);
        BuildChangeAndAutomation(m);
        BuildEvidenceAndDiagnostics(m);
        BuildActivityFeed(m);
        return m;
    }

    private static readonly Random Rng = new(20260902);

    private static MetricSeries Series(string key, string unit, double baseline, double jitter,
        double warn = double.NaN, double breach = double.NaN, int points = 96, double driftAfter = -1, double driftTo = 0)
    {
        var s = new MetricSeries { Key = key, Unit = unit, Warn = warn, Breach = breach };
        for (int i = 0; i < points; i++)
        {
            var t = Anchor.AddMinutes(-(points - 1 - i) * 2.5);
            double v = baseline + (Rng.NextDouble() - 0.5) * jitter;
            if (driftAfter >= 0 && i >= points * driftAfter)
            {
                double p = (i - points * driftAfter) / (points * (1 - driftAfter));
                v = baseline + (driftTo - baseline) * Math.Min(1, p * 1.6) + (Rng.NextDouble() - 0.5) * jitter;
            }
            s.Points.Add(new MetricPoint(t, Math.Max(0, Math.Round(v, 2))));
        }
        return s;
    }

    private static ServiceNode Node(string id, string name, EstateLayer layer, string kind, string owner,
        string location, int tier, HealthState health = HealthState.Healthy, params string[] tags)
    {
        var n = new ServiceNode
        {
            Id = id,
            Name = name,
            Layer = layer,
            Kind = kind,
            Owner = owner,
            Location = location,
            Tier = tier,
            Health = health
        };
        n.Tags.AddRange(tags);
        return n;
    }

    private static void BuildTopology(EstateModel m)
    {
        // ---------- Foundation ----------
        m.Add(Node("fnd-dc-alpha", "DC Alpha Facility", EstateLayer.Foundation, "Facility", "Data Centre Ops", "Manchester", 1, HealthState.Healthy, "primary", "tier-iii"));
        m.Add(Node("fnd-dc-bravo", "DC Bravo Facility", EstateLayer.Foundation, "Facility", "Data Centre Ops", "Reading", 1, HealthState.Healthy, "secondary", "dr"));
        m.Add(Node("fnd-compute", "Virtualisation Cluster", EstateLayer.Foundation, "Hypervisor", "Platform Engineering", "DC-Alpha", 1, HealthState.Healthy, "vmware", "hci"));
        m.Add(Node("fnd-storage", "Primary Storage Array", EstateLayer.Foundation, "Storage", "Platform Engineering", "DC-Alpha", 1, HealthState.Healthy, "nvme", "replicated"));
        m.Add(Node("fnd-power", "Power & Environmental", EstateLayer.Foundation, "Facility Plant", "Data Centre Ops", "DC-Alpha", 1, HealthState.Maintenance, "ups", "planned-work"));

        // ---------- Network ----------
        m.Add(Node("net-edge", "Perimeter Firewall Pair", EstateLayer.Network, "Firewall", "Network Engineering", "DC-Alpha", 1, HealthState.Healthy, "ha-pair", "edge"));
        m.Add(Node("net-core", "Core Switching Fabric", EstateLayer.Network, "Switch Fabric", "Network Engineering", "DC-Alpha", 1, HealthState.Healthy, "spine-leaf"));
        m.Add(Node("net-dist-c", "Distribution Switch — Building C", EstateLayer.Network, "Switch Stack", "Network Engineering", "Campus C", 2, HealthState.Degraded, "campus", "port-channel"));
        m.Add(Node("net-dist-a", "Distribution Switch — Building A", EstateLayer.Network, "Switch Stack", "Network Engineering", "Campus A", 2, HealthState.Healthy, "campus"));
        m.Add(Node("net-wan", "SD-WAN Gateway", EstateLayer.Network, "WAN Edge", "Network Engineering", "DC-Alpha", 1, HealthState.Healthy, "dual-carrier"));
        m.Add(Node("net-lb", "Application Delivery Tier", EstateLayer.Network, "Load Balancer", "Network Engineering", "DC-Alpha", 1, HealthState.Healthy, "adc", "tls-offload"));
        m.Add(Node("net-vpn", "Remote Access Gateway", EstateLayer.Network, "VPN", "Network Engineering", "DC-Alpha", 2, HealthState.Healthy, "remote"));

        // ---------- Core Services ----------
        m.Add(Node("svc-dns", "Internal DNS Resolvers", EstateLayer.CoreServices, "Name Resolution", "Platform Engineering", "DC-Alpha / DC-Bravo", 1, HealthState.Critical, "anycast", "resolver-pair"));
        m.Add(Node("svc-dhcp", "DHCP Scopes", EstateLayer.CoreServices, "Address Assignment", "Platform Engineering", "DC-Alpha", 2, HealthState.Healthy, "failover"));
        m.Add(Node("svc-ntp", "Time Service", EstateLayer.CoreServices, "Time Sync", "Platform Engineering", "DC-Alpha", 2, HealthState.Healthy, "stratum-2"));
        m.Add(Node("svc-pki", "Internal Certificate Authority", EstateLayer.CoreServices, "PKI", "Security Engineering", "DC-Alpha", 1, HealthState.Healthy, "issuing-ca"));
        m.Add(Node("svc-smtp", "Mail Transport Relay", EstateLayer.CoreServices, "Message Transport", "Messaging Team", "DC-Alpha", 2, HealthState.Healthy, "relay"));

        // ---------- Identity / Trust ----------
        m.Add(Node("idp-ad", "Directory Services", EstateLayer.Identity, "Directory", "Identity Engineering", "DC-Alpha / DC-Bravo", 1, HealthState.Healthy, "kerberos", "ldap"));
        m.Add(Node("idp-fed", "Federation Service", EstateLayer.Identity, "Identity Provider", "Identity Engineering", "DC-Alpha", 1, HealthState.Degraded, "oidc", "saml"));
        m.Add(Node("idp-mfa", "Multifactor Service", EstateLayer.Identity, "Strong Auth", "Identity Engineering", "Cloud", 1, HealthState.Healthy, "push", "fido2"));
        m.Add(Node("idp-nac", "Network Access Control", EstateLayer.Identity, "RADIUS / 802.1X", "Identity Engineering", "DC-Alpha", 2, HealthState.Healthy, "posture"));

        // ---------- Control ----------
        m.Add(Node("ctl-telemetry", "Telemetry Pipeline", EstateLayer.Control, "Observability", "Observability Team", "DC-Alpha", 1, HealthState.Healthy, "otel", "metrics"));
        m.Add(Node("ctl-siem", "Security Analytics Pipeline", EstateLayer.Control, "SIEM Ingest", "Security Operations", "Cloud", 1, HealthState.Healthy, "detections"));
        m.Add(Node("ctl-config", "Configuration Management", EstateLayer.Control, "Desired State", "Platform Engineering", "DC-Alpha", 2, HealthState.Healthy, "idempotent"));
        m.Add(Node("ctl-backup", "Backup & Recovery Orchestrator", EstateLayer.Control, "Data Protection", "Platform Engineering", "DC-Bravo", 2, HealthState.Healthy, "immutable"));
        m.Add(Node("ctl-automation", "Automation Control Plane", EstateLayer.Control, "Runbook Engine", "Platform Operations", "DC-Alpha", 1, HealthState.Healthy, "gated"));

        // ---------- Applications ----------
        m.Add(Node("app-api", "Partner API Gateway", EstateLayer.Applications, "External API", "Integration Team", "DC-Alpha", 1, HealthState.Critical, "external", "revenue"));
        m.Add(Node("app-web", "Customer Web Portal", EstateLayer.Applications, "Web Application", "Digital Channels", "DC-Alpha", 1, HealthState.Degraded, "external", "sso"));
        m.Add(Node("app-erp", "Finance & ERP", EstateLayer.Applications, "Business Application", "Enterprise Applications", "DC-Alpha", 1, HealthState.Healthy, "sox"));
        m.Add(Node("app-crm", "Customer Relationship Platform", EstateLayer.Applications, "Business Application", "Enterprise Applications", "Cloud", 2, HealthState.Healthy, "saas"));
        m.Add(Node("app-collab", "Collaboration Suite", EstateLayer.Applications, "Productivity", "Messaging Team", "Cloud", 1, HealthState.Degraded, "chat", "meetings"));
        m.Add(Node("app-vdi", "Virtual Desktop Service", EstateLayer.Applications, "End User Compute", "End User Computing", "DC-Alpha", 2, HealthState.Degraded, "campus"));
        m.Add(Node("app-files", "File & Print Services", EstateLayer.Applications, "File Services", "End User Computing", "DC-Alpha", 3, HealthState.Healthy, "smb"));

        // ---------- Proof ----------
        m.Add(Node("prf-ledger", "Evidence Ledger", EstateLayer.Proof, "Attestation Store", "Assurance", "DC-Bravo", 1, HealthState.Healthy, "append-only"));
        m.Add(Node("prf-audit", "Audit Archive", EstateLayer.Proof, "Retention", "Assurance", "DC-Bravo", 2, HealthState.Healthy, "worm", "7-year"));

        Wire(m);
        Reasons(m);
        Metrics(m);
    }

    private static void Edge(EstateModel m, string from, string to, DependencyKind kind, string proto, string port = "",
        double latency = 1.0, bool critical = true, string note = "")
        => m.Connect(new DependencyEdge
        {
            FromId = from,
            ToId = to,
            Kind = kind,
            Protocol = proto,
            Port = port,
            LatencyMs = latency,
            IsCritical = critical,
            Note = note
        });

    private static void Wire(EstateModel m)
    {
        // Foundation hosting
        foreach (var id in new[] { "fnd-compute", "fnd-storage" })
            Edge(m, id, "fnd-dc-alpha", DependencyKind.Hosting, "Facility", latency: 0, note: "Rack power and cooling");
        Edge(m, "fnd-dc-alpha", "fnd-power", DependencyKind.Hosting, "Facility", latency: 0);
        Edge(m, "fnd-compute", "fnd-storage", DependencyKind.Data, "NVMe-oF", "4420", 0.4);

        // Network fabric
        Edge(m, "net-core", "fnd-dc-alpha", DependencyKind.Hosting, "Facility", latency: 0);
        Edge(m, "net-dist-c", "net-core", DependencyKind.Synchronous, "802.1Q / LACP", "Po1", 0.6, note: "2 x 10G port-channel to core");
        Edge(m, "net-dist-a", "net-core", DependencyKind.Synchronous, "802.1Q / LACP", "Po1", 0.5);
        Edge(m, "net-edge", "net-core", DependencyKind.Synchronous, "BGP", "179", 0.8);
        Edge(m, "net-wan", "net-edge", DependencyKind.Synchronous, "IPsec", "500", 6.4);
        Edge(m, "net-lb", "net-core", DependencyKind.Synchronous, "TCP", "443", 0.4);
        Edge(m, "net-vpn", "net-edge", DependencyKind.Synchronous, "TLS", "443", 2.1);
        Edge(m, "net-vpn", "idp-nac", DependencyKind.Trust, "RADIUS", "1812", 3.0);

        // Core services
        Edge(m, "svc-dns", "fnd-compute", DependencyKind.Hosting, "Virtual Machine", latency: 0);
        Edge(m, "svc-dns", "net-core", DependencyKind.Synchronous, "Anycast / OSPF", "53", 0.3);
        Edge(m, "svc-dhcp", "svc-dns", DependencyKind.Synchronous, "DDNS", "53", 1.1);
        Edge(m, "svc-pki", "idp-ad", DependencyKind.Trust, "LDAP", "636", 2.0, note: "Template and enrolment authorisation");
        Edge(m, "svc-smtp", "svc-dns", DependencyKind.Synchronous, "DNS MX", "53", 1.4);
        Edge(m, "svc-ntp", "net-core", DependencyKind.Synchronous, "NTP", "123", 0.2, critical: false);

        // Identity
        Edge(m, "idp-ad", "fnd-compute", DependencyKind.Hosting, "Virtual Machine", latency: 0);
        Edge(m, "idp-ad", "svc-dns", DependencyKind.Synchronous, "DNS SRV", "53", 1.2, note: "Domain controller SRV location");
        Edge(m, "idp-ad", "svc-ntp", DependencyKind.Synchronous, "NTP", "123", 0.3, note: "Kerberos 5 minute clock skew limit");
        Edge(m, "idp-fed", "idp-ad", DependencyKind.Trust, "LDAPS", "636", 3.8);
        Edge(m, "idp-fed", "svc-dns", DependencyKind.Synchronous, "DNS A / CNAME", "53", 1.6, note: "Resolves external IdP metadata host");
        Edge(m, "idp-fed", "svc-pki", DependencyKind.Trust, "PKI", "", 0.9, note: "Token signing certificate chain");
        Edge(m, "idp-mfa", "idp-fed", DependencyKind.Trust, "OIDC", "443", 42.0);
        Edge(m, "idp-nac", "idp-ad", DependencyKind.Trust, "Kerberos", "88", 2.2);

        // Control
        Edge(m, "ctl-telemetry", "fnd-compute", DependencyKind.Hosting, "Virtual Machine", latency: 0);
        Edge(m, "ctl-telemetry", "net-core", DependencyKind.Asynchronous, "OTLP", "4317", 1.0, critical: false);
        Edge(m, "ctl-siem", "ctl-telemetry", DependencyKind.Asynchronous, "HTTPS", "443", 120.0, critical: false);
        Edge(m, "ctl-siem", "idp-fed", DependencyKind.Data, "Sign-in logs", "443", 90.0, critical: false);
        Edge(m, "ctl-config", "idp-ad", DependencyKind.Trust, "WinRM", "5986", 4.0);
        Edge(m, "ctl-backup", "fnd-storage", DependencyKind.Data, "Snapshot API", "443", 12.0);
        Edge(m, "ctl-automation", "ctl-config", DependencyKind.Synchronous, "HTTPS", "443", 6.0);
        Edge(m, "ctl-automation", "idp-fed", DependencyKind.Trust, "OIDC", "443", 30.0, note: "Operator authorisation for gated actions");

        // Applications
        Edge(m, "app-api", "net-lb", DependencyKind.Synchronous, "HTTPS", "443", 1.2);
        Edge(m, "app-api", "idp-fed", DependencyKind.Trust, "OAuth 2.0", "443", 38.0, note: "Bearer token introspection");
        Edge(m, "app-api", "svc-dns", DependencyKind.Synchronous, "DNS A", "53", 1.9, note: "Resolves federation metadata endpoint");
        Edge(m, "app-api", "fnd-compute", DependencyKind.Hosting, "Container Host", latency: 0);
        Edge(m, "app-web", "net-lb", DependencyKind.Synchronous, "HTTPS", "443", 1.1);
        Edge(m, "app-web", "idp-fed", DependencyKind.Trust, "SAML 2.0", "443", 44.0);
        Edge(m, "app-web", "app-api", DependencyKind.Synchronous, "REST", "443", 26.0);
        Edge(m, "app-erp", "idp-ad", DependencyKind.Trust, "Kerberos", "88", 5.0);
        Edge(m, "app-erp", "fnd-storage", DependencyKind.Data, "iSCSI", "3260", 2.0);
        Edge(m, "app-crm", "idp-fed", DependencyKind.Trust, "SAML 2.0", "443", 61.0);
        Edge(m, "app-crm", "net-wan", DependencyKind.Synchronous, "HTTPS", "443", 24.0);
        Edge(m, "app-collab", "idp-fed", DependencyKind.Trust, "OIDC", "443", 55.0);
        Edge(m, "app-collab", "net-wan", DependencyKind.Synchronous, "HTTPS", "443", 28.0);
        Edge(m, "app-collab", "svc-smtp", DependencyKind.Asynchronous, "SMTP", "25", 40.0, critical: false);
        Edge(m, "app-vdi", "net-dist-c", DependencyKind.Synchronous, "PCoIP / BLAST", "4172", 3.4, note: "Campus C access layer");
        Edge(m, "app-vdi", "idp-ad", DependencyKind.Trust, "Kerberos", "88", 6.0);
        Edge(m, "app-vdi", "fnd-compute", DependencyKind.Hosting, "Virtual Machine", latency: 0);
        Edge(m, "app-files", "idp-ad", DependencyKind.Trust, "Kerberos", "88", 4.4);
        Edge(m, "app-files", "fnd-storage", DependencyKind.Data, "SMB3", "445", 1.8);
        Edge(m, "app-files", "net-dist-a", DependencyKind.Synchronous, "TCP", "445", 1.0, critical: false);

        // Proof
        Edge(m, "prf-ledger", "fnd-dc-bravo", DependencyKind.Hosting, "Facility", latency: 0);
        Edge(m, "prf-ledger", "svc-pki", DependencyKind.Trust, "PKI", "", 1.0, note: "Record countersigning");
        Edge(m, "prf-audit", "prf-ledger", DependencyKind.Data, "Append-only", "", 3.0);
        Edge(m, "prf-ledger", "ctl-automation", DependencyKind.Data, "HTTPS", "443", 8.0, critical: false, note: "Runbook execution attestations");
    }

    private static void Reasons(EstateModel m)
    {
        void Set(string id, string reason, double avail, double latency, double err, int signals, double degradedMinutes = -1)
        {
            var n = m.Node(id);
            if (n is null) return;
            n.StateReason = reason;
            n.Availability = avail;
            n.LatencyMs = latency;
            n.ErrorRate = err;
            n.OpenSignals = signals;
            if (degradedMinutes >= 0) n.DegradedSince = Anchor.AddMinutes(-degradedMinutes);
        }

        Set("svc-dns", "Secondary resolver returning SERVFAIL for conditional forwarder zone", 71.40, 2840, 28.4, 4, 74);
        Set("idp-fed", "Metadata refresh failing; cached signing keys still valid for 41 min", 96.20, 1180, 8.9, 3, 66);
        Set("app-api", "5xx to partner clients on token introspection path", 88.10, 4120, 34.7, 6, 62);
        Set("app-web", "Sign-in redirect intermittently failing; anonymous browse unaffected", 97.60, 940, 4.2, 2, 58);
        Set("app-collab", "Presence and meeting join delayed behind identity refresh", 98.80, 610, 1.9, 1, 54);
        Set("net-dist-c", "Port-channel member Gi1/0/47 flapping — 214 transitions in 30 min", 93.20, 18.4, 2.6, 3, 128);
        Set("app-vdi", "Session disconnects in Campus C; other buildings nominal", 95.10, 88, 3.1, 2, 121);
        Set("fnd-power", "Planned UPS battery string replacement, B-feed only", 100.0, 0, 0, 0);

        foreach (var n in m.Nodes.Where(x => x.Health == HealthState.Healthy))
        {
            if (n.Availability >= 99.99) n.Availability = 99.9 + Rng.NextDouble() * 0.09;
            if (n.LatencyMs == 0) n.LatencyMs = Math.Round(1 + Rng.NextDouble() * 40, 1);
            n.StateReason = string.IsNullOrEmpty(n.StateReason) ? "All indicators within objective" : n.StateReason;
        }
    }

    private static void Metrics(EstateModel m)
    {
        foreach (var n in m.Nodes)
        {
            bool bad = n.Health.IsBad();
            double drift = bad ? 0.62 : -1;
            n.Metrics["latency"] = Series("latency", "ms", Math.Max(1, n.LatencyMs * (bad ? 0.12 : 1)), Math.Max(1, n.LatencyMs * 0.16),
                warn: n.LatencyMs * 0.8, breach: n.LatencyMs, driftAfter: drift, driftTo: n.LatencyMs);
            n.Metrics["errors"] = Series("errors", "%", bad ? 0.2 : Math.Max(0.02, n.ErrorRate), 0.25,
                warn: 1, breach: 5, driftAfter: drift, driftTo: Math.Max(0.05, n.ErrorRate));
            n.Metrics["throughput"] = Series("throughput", "req/s", 400 + Rng.Next(0, 900), 90,
                driftAfter: bad ? 0.62 : -1, driftTo: bad ? 120 : 0);
        }
    }
}
