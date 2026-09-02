namespace BluePeak.Domain;

/// <summary>
/// The single in-memory model of the managed estate. Every workspace in the product
/// reads from this one graph, so a subject selected in one workspace is the same
/// object everywhere else.
/// </summary>
public sealed class EstateModel
{
    private readonly Dictionary<string, ServiceNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DependencyEdge> _edges = new();
    private Dictionary<string, List<DependencyEdge>> _outgoing = new();
    private Dictionary<string, List<DependencyEdge>> _incoming = new();

    public DateTime Now { get; set; } = DateTime.Now;

    public IReadOnlyCollection<ServiceNode> Nodes => _nodes.Values;
    public IReadOnlyList<DependencyEdge> Edges => _edges;

    public List<Ticket> Tickets { get; } = new();
    public List<Incident> Incidents { get; } = new();
    public List<SecurityAlert> Alerts { get; } = new();
    public List<SecurityCase> Cases { get; } = new();
    public List<SecurityEntity> Entities { get; } = new();
    public List<ChangeRequest> Changes { get; } = new();
    public List<Runbook> Runbooks { get; } = new();
    public List<EvidenceRecord> Evidence { get; } = new();
    public List<DiagnosticPath> DiagnosticPaths { get; } = new();
    public List<TimelineEvent> ActivityFeed { get; } = new();

    public void Add(ServiceNode node) => _nodes[node.Id] = node;

    public void Connect(DependencyEdge edge)
    {
        _edges.Add(edge);
        if (_nodes.TryGetValue(edge.FromId, out var from) && !from.DependsOn.Contains(edge.ToId))
            from.DependsOn.Add(edge.ToId);
    }

    /// <summary>Builds adjacency indexes. Call once after all nodes and edges are loaded.</summary>
    public void Index()
    {
        _outgoing = _edges.GroupBy(e => e.FromId, StringComparer.OrdinalIgnoreCase)
                          .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        _incoming = _edges.GroupBy(e => e.ToId, StringComparer.OrdinalIgnoreCase)
                          .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public ServiceNode? Node(string? id) =>
        id is not null && _nodes.TryGetValue(id, out var n) ? n : null;

    public string NameOf(string? id) => Node(id)?.Name ?? id ?? "—";

    /// <summary>Edges where this node is the consumer (things it needs).</summary>
    public IReadOnlyList<DependencyEdge> DependenciesOf(string id) =>
        _outgoing.TryGetValue(id, out var l) ? l : Array.Empty<DependencyEdge>();

    /// <summary>Edges where this node is the provider (things that need it).</summary>
    public IReadOnlyList<DependencyEdge> DependentsOf(string id) =>
        _incoming.TryGetValue(id, out var l) ? l : Array.Empty<DependencyEdge>();

    /// <summary>
    /// Everything that would be impaired if <paramref name="id"/> failed, walked
    /// transitively upward through consumers. This is the blast radius.
    /// </summary>
    public List<ServiceNode> BlastRadius(string id, bool criticalOnly = false)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        var queue = new Queue<string>();
        var result = new List<ServiceNode>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in DependentsOf(current))
            {
                if (criticalOnly && !edge.IsCritical) continue;
                if (!seen.Add(edge.FromId)) continue;
                var node = Node(edge.FromId);
                if (node is null) continue;
                result.Add(node);
                queue.Enqueue(edge.FromId);
            }
        }
        return result.OrderBy(n => n.Layer).ThenBy(n => n.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Transitive closure of what this node needs in order to work.</summary>
    public List<ServiceNode> DependencyClosure(string id)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        var queue = new Queue<string>();
        var result = new List<ServiceNode>();
        queue.Enqueue(id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in DependenciesOf(current))
            {
                if (!seen.Add(edge.ToId)) continue;
                var node = Node(edge.ToId);
                if (node is null) continue;
                result.Add(node);
                queue.Enqueue(edge.ToId);
            }
        }
        return result;
    }

    /// <summary>
    /// Walks down from a degraded node through its own dependencies to find the deepest
    /// unhealthy element. That element is the candidate first failure; everything above
    /// it is consequence rather than cause.
    /// </summary>
    public ServiceNode? FirstFailure(string id)
    {
        var start = Node(id);
        if (start is null || !start.Health.IsBad()) return null;

        ServiceNode best = start;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        var queue = new Queue<(string Id, int Depth)>();
        var depthOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [id] = 0 };
        queue.Enqueue((id, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            foreach (var edge in DependenciesOf(current))
            {
                if (!seen.Add(edge.ToId)) continue;
                var node = Node(edge.ToId);
                if (node is null) continue;
                depthOf[edge.ToId] = depth + 1;
                queue.Enqueue((edge.ToId, depth + 1));
                if (!node.Health.IsBad()) continue;
                var bestDepth = depthOf.TryGetValue(best.Id, out var d) ? d : 0;
                if (depth + 1 > bestDepth || node.Health.Weight() > best.Health.Weight() && depth + 1 >= bestDepth)
                    best = node;
            }
        }
        return best;
    }

    public IEnumerable<ServiceNode> Unhealthy() =>
        _nodes.Values.Where(n => n.Health.IsBad())
                     .OrderByDescending(n => n.Health.Weight())
                     .ThenBy(n => n.Layer);

    public IEnumerable<ServiceNode> ByLayer(EstateLayer layer) =>
        _nodes.Values.Where(n => n.Layer == layer).OrderBy(n => n.Name, StringComparer.Ordinal);

    public Ticket? Ticket(string? id) => Tickets.FirstOrDefault(t => t.Id == id);
    public Incident? Incident(string? id) => Incidents.FirstOrDefault(i => i.Id == id);
    public SecurityAlert? Alert(string? id) => Alerts.FirstOrDefault(a => a.Id == id);
    public SecurityCase? Case(string? id) => Cases.FirstOrDefault(c => c.Id == id);
    public SecurityEntity? Entity(string? id) => Entities.FirstOrDefault(e => e.Id == id);
    public ChangeRequest? Change(string? id) => Changes.FirstOrDefault(c => c.Id == id);
    public Runbook? Runbook(string? id) => Runbooks.FirstOrDefault(r => r.Id == id);
    public EvidenceRecord? EvidenceRecord(string? id) => Evidence.FirstOrDefault(e => e.Id == id);

    /// <summary>Health rolled up per architectural layer, used by Infrastructure and Overview.</summary>
    public LayerRollup Rollup(EstateLayer layer)
    {
        var nodes = ByLayer(layer).ToList();
        return new LayerRollup
        {
            Layer = layer,
            Total = nodes.Count,
            Critical = nodes.Count(n => n.Health is HealthState.Critical or HealthState.Offline),
            Degraded = nodes.Count(n => n.Health == HealthState.Degraded),
            Healthy = nodes.Count(n => n.Health == HealthState.Healthy),
            Maintenance = nodes.Count(n => n.Health == HealthState.Maintenance),
            Worst = nodes.Count == 0 ? HealthState.Unknown : nodes.MaxBy(n => n.Health.Weight())!.Health
        };
    }
}

public sealed class LayerRollup
{
    public EstateLayer Layer { get; init; }
    public int Total { get; init; }
    public int Critical { get; init; }
    public int Degraded { get; init; }
    public int Healthy { get; init; }
    public int Maintenance { get; init; }
    public HealthState Worst { get; init; }
    public double HealthyFraction => Total == 0 ? 1 : (double)Healthy / Total;
}
