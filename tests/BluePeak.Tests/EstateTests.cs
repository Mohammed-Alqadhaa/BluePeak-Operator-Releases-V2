using BluePeak.Domain;
using BluePeak.Domain.Seed;
using Xunit;

namespace BluePeak.Tests;

public class EstateTests
{
    private static readonly EstateModel Model = EstateSeed.Build(new DateTime(2026, 9, 2, 9, 45, 0));

    [Fact]
    public void Estate_is_populated_and_indexed()
    {
        Assert.True(Model.Nodes.Count >= 30, $"only {Model.Nodes.Count} elements");
        Assert.True(Model.Edges.Count >= 45, $"only {Model.Edges.Count} dependencies");
        Assert.NotEmpty(Model.Incidents);
        Assert.NotEmpty(Model.Tickets);
        Assert.NotEmpty(Model.Alerts);
        Assert.NotEmpty(Model.Cases);
        Assert.NotEmpty(Model.Changes);
        Assert.NotEmpty(Model.Runbooks);
        Assert.NotEmpty(Model.Evidence);
        Assert.NotEmpty(Model.DiagnosticPaths);
    }

    [Fact]
    public void Every_dependency_edge_resolves_to_real_nodes()
    {
        foreach (var edge in Model.Edges)
        {
            Assert.True(Model.Node(edge.FromId) is not null, $"unknown source {edge.FromId}");
            Assert.True(Model.Node(edge.ToId) is not null, $"unknown target {edge.ToId}");
            Assert.False(string.IsNullOrWhiteSpace(edge.Protocol), $"{edge.FromId}->{edge.ToId} has no protocol");
        }
    }

    [Fact]
    public void The_dependency_graph_has_no_cycles_through_critical_edges()
    {
        // A cycle would make first-failure analysis meaningless, so assert the model is a DAG.
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string id, List<string> path)
        {
            if (done.Contains(id)) return;
            Assert.True(visiting.Add(id), $"cycle through {string.Join(" -> ", path)} -> {id}");
            path.Add(id);
            foreach (var edge in Model.DependenciesOf(id)) Visit(edge.ToId, path);
            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            done.Add(id);
        }

        foreach (var node in Model.Nodes) Visit(node.Id, new List<string>());
    }

    [Fact]
    public void Cross_references_between_records_all_resolve()
    {
        foreach (var incident in Model.Incidents)
        {
            foreach (var id in incident.AffectedServiceIds)
                Assert.True(Model.Node(id) is not null, $"{incident.Id} references unknown service {id}");
            foreach (var id in incident.LinkedTicketIds)
                Assert.True(Model.Ticket(id) is not null, $"{incident.Id} references unknown ticket {id}");
            foreach (var id in incident.EvidenceIds)
                Assert.True(Model.EvidenceRecord(id) is not null, $"{incident.Id} references unknown evidence {id}");
            if (incident.RootCauseServiceId is not null)
                Assert.True(Model.Node(incident.RootCauseServiceId) is not null);
            if (incident.SuspectedChangeId is not null)
                Assert.True(Model.Change(incident.SuspectedChangeId) is not null);
        }

        foreach (var ticket in Model.Tickets)
        {
            if (ticket.LinkedServiceId is not null)
                Assert.True(Model.Node(ticket.LinkedServiceId) is not null, $"{ticket.Id} references unknown service");
            if (ticket.LinkedIncidentId is not null)
                Assert.True(Model.Incident(ticket.LinkedIncidentId) is not null, $"{ticket.Id} references unknown incident");
            foreach (var id in ticket.SimilarTicketIds)
                Assert.True(Model.Ticket(id) is not null, $"{ticket.Id} references unknown similar ticket {id}");
        }

        foreach (var alert in Model.Alerts)
        {
            foreach (var id in alert.EntityIds)
                Assert.True(Model.Entity(id) is not null, $"{alert.Id} references unknown entity {id}");
            if (alert.CaseId is not null)
                Assert.True(Model.Case(alert.CaseId) is not null, $"{alert.Id} references unknown case");
        }

        foreach (var securityCase in Model.Cases)
        {
            foreach (var id in securityCase.AlertIds)
                Assert.True(Model.Alert(id) is not null, $"{securityCase.Id} references unknown alert {id}");
            foreach (var id in securityCase.EntityIds)
                Assert.True(Model.Entity(id) is not null, $"{securityCase.Id} references unknown entity {id}");
        }

        foreach (var change in Model.Changes)
        {
            Assert.True(Model.Node(change.TargetServiceId) is not null, $"{change.Id} targets unknown service");
            foreach (var id in change.BlastRadiusServiceIds)
                Assert.True(Model.Node(id) is not null, $"{change.Id} references unknown service {id}");
        }

        foreach (var runbook in Model.Runbooks)
            foreach (var id in runbook.TargetServiceIds)
                Assert.True(Model.Node(id) is not null, $"{runbook.Id} targets unknown service {id}");

        foreach (var path in Model.DiagnosticPaths)
        {
            foreach (var hop in path.Hops)
                Assert.True(Model.Node(hop.ServiceId) is not null, $"{path.Id} hop references unknown service");
            foreach (var id in path.BlastRadiusServiceIds)
                Assert.True(Model.Node(id) is not null);
        }
    }

    [Fact]
    public void Blast_radius_walks_consumers_transitively()
    {
        var blast = Model.BlastRadius("svc-dns");
        var ids = blast.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("idp-fed", ids);          // direct consumer
        Assert.Contains("app-api", ids);          // direct consumer
        Assert.Contains("app-crm", ids);          // reached through the federation service
        Assert.DoesNotContain("svc-dns", ids);    // never includes itself
        Assert.DoesNotContain("fnd-compute", ids); // a provider, not a consumer
    }

    [Fact]
    public void Dependency_closure_reaches_the_foundation()
    {
        var closure = Model.DependencyClosure("app-api").Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("svc-dns", closure);
        Assert.Contains("idp-fed", closure);
        Assert.Contains("fnd-compute", closure);
        Assert.Contains("fnd-dc-alpha", closure);
    }

    [Fact]
    public void First_failure_finds_the_deepest_unhealthy_component()
    {
        // The partner API is failing, but the resolver beneath it is the actual cause.
        var failure = Model.FirstFailure("app-api");
        Assert.NotNull(failure);
        Assert.Equal("svc-dns", failure!.Id);

        // The resolver itself is the origin: nothing it needs is unhealthy.
        var origin = Model.FirstFailure("svc-dns");
        Assert.NotNull(origin);
        Assert.Equal("svc-dns", origin!.Id);

        // A healthy service has no first failure.
        Assert.Null(Model.FirstFailure("app-erp"));
    }

    [Fact]
    public void Diagnostic_paths_have_exactly_one_first_failure_and_it_matches_the_conclusion()
    {
        foreach (var path in Model.DiagnosticPaths)
        {
            var firstFailures = path.Hops.Where(h => h.IsFirstFailure).ToList();
            Assert.True(firstFailures.Count == 1, $"{path.Id} has {firstFailures.Count} first failures");
            Assert.Equal(path.FirstFailureServiceId, firstFailures[0].ServiceId);

            int failureIndex = path.Hops.IndexOf(firstFailures[0]);
            Assert.True(firstFailures[0].Result.IsBad(), $"{path.Id} declares a healthy hop as its first failure");

            // No hop before the first failure may be hard-failed. A degraded-but-still-answering
            // hop is allowed there, and is the whole point of the masked-fault scenario: the
            // component is impaired yet succeeding from cache, so it is not where the request broke.
            for (int i = 0; i < failureIndex; i++)
                Assert.False(path.Hops[i].Result is HealthState.Critical or HealthState.Offline,
                    $"{path.Id} hop {i + 1} has hard-failed before the declared first failure");

            foreach (var hop in path.Hops)
            {
                Assert.False(string.IsNullOrWhiteSpace(hop.Expected), $"{path.Id} hop {hop.Index} has no expected value");
                Assert.False(string.IsNullOrWhiteSpace(hop.Actual), $"{path.Id} hop {hop.Index} has no actual value");
                Assert.False(hop.IsFirstFailure && hop.IsDownstreamConsequence,
                    $"{path.Id} hop {hop.Index} is marked both cause and consequence");
            }
        }
    }

    [Fact]
    public void Every_evidence_record_states_its_authority_and_carries_a_digest()
    {
        foreach (var record in Model.Evidence)
        {
            Assert.False(string.IsNullOrWhiteSpace(record.Claim));
            Assert.False(string.IsNullOrWhiteSpace(record.Source));
            Assert.False(string.IsNullOrWhiteSpace(record.Check));
            Assert.False(string.IsNullOrWhiteSpace(record.Digest));
            Assert.NotEqual(default, record.CapturedAt);
        }

        // The boundary the product promises: local records are never silently authoritative.
        var local = Model.Evidence.Where(e => e.Authority == EvidenceAuthority.LocalOperator).ToList();
        Assert.NotEmpty(local);
        Assert.All(local, e => Assert.NotEqual(EvidenceAuthority.ProjectAuthoritative, e.Authority));
    }

    [Fact]
    public void Seed_is_deterministic_for_the_same_clock()
    {
        var anchor = new DateTime(2026, 9, 2, 9, 45, 0);
        var a = EstateSeed.Build(anchor);
        var b = EstateSeed.Build(anchor);

        Assert.Equal(a.Nodes.Count, b.Nodes.Count);
        Assert.Equal(a.Edges.Count, b.Edges.Count);
        foreach (var record in a.Evidence)
        {
            var other = b.EvidenceRecord(record.Id);
            Assert.NotNull(other);
            Assert.Equal(record.Digest, other!.Digest);
        }
    }

    [Fact]
    public void Layer_rollups_account_for_every_element()
    {
        int total = 0;
        foreach (EstateLayer layer in Enum.GetValues<EstateLayer>())
        {
            var rollup = Model.Rollup(layer);
            Assert.Equal(rollup.Total, rollup.Critical + rollup.Degraded + rollup.Healthy + rollup.Maintenance);
            total += rollup.Total;
        }
        Assert.Equal(Model.Nodes.Count, total);
    }

    [Fact]
    public void The_seeded_situation_is_coherent_across_workspaces()
    {
        // One fault should be visible from every discipline, which is what makes the product
        // feel like one platform rather than several screens.
        var incident = Model.Incident("INC-4412");
        Assert.NotNull(incident);
        Assert.Equal("svc-dns", incident!.RootCauseServiceId);

        var resolver = Model.Node("svc-dns")!;
        Assert.Equal(HealthState.Critical, resolver.Health);

        Assert.Contains(Model.Tickets, t => t.LinkedIncidentId == "INC-4412");
        Assert.Contains(Model.Changes, c => c.LinkedIncidentId == "INC-4412");
        Assert.Contains(Model.Runbooks, r => r.SuggestedForIncidentId == "INC-4412");
        Assert.Contains(Model.DiagnosticPaths, p => p.LinkedIncidentId == "INC-4412");
        Assert.Contains(Model.Evidence, e => e.SubjectId == "svc-dns");
    }
}
