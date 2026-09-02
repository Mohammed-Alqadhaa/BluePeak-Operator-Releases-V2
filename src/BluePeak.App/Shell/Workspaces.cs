using System.Windows.Controls;

namespace BluePeak.App.Shell;

/// <summary>
/// Navigation is grouped by the operator loop rather than by product module. An operator
/// asks "what is happening", then "what is it", then "what do I do", then "did it work" —
/// so the rail is ordered Observe, Respond, Diagnose, Act, Verify.
/// </summary>
public sealed class WorkspaceDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Group { get; init; }
    public required string Icon { get; init; }
    public required string Purpose { get; init; }
    public required Func<UserControl> Factory { get; init; }
    public string Shortcut { get; init; } = "";

    private UserControl? _instance;

    /// <summary>
    /// Views are created once and kept. Navigating away and back must not reset a running
    /// investigation, a scroll position, or the simulator's playhead.
    /// </summary>
    public UserControl View => _instance ??= Factory();

    public bool IsRealised => _instance is not null;
}

public static class WorkspaceCatalog
{
    public static IReadOnlyList<WorkspaceDefinition> All { get; } = new List<WorkspaceDefinition>
    {
        new()
        {
            Id = "overview", Title = "Overview", Group = "Observe", Icon = "I.Overview", Shortcut = "Ctrl+1",
            Purpose = "What is happening right now, what changed, and what to look at next",
            Factory = () => new Workspaces.OverviewView()
        },
        new()
        {
            Id = "noc", Title = "NOC", Group = "Observe", Icon = "I.Noc", Shortcut = "Ctrl+2",
            Purpose = "Service health, dependency propagation, first failure and blast radius",
            Factory = () => new Workspaces.NocView()
        },
        new()
        {
            Id = "soc", Title = "SOC", Group = "Observe", Icon = "I.Soc", Shortcut = "Ctrl+3",
            Purpose = "Detections, entities, correlation and case investigation",
            Factory = () => new Workspaces.SocView()
        },
        new()
        {
            Id = "servicedesk", Title = "Service Desk", Group = "Respond", Icon = "I.Desk", Shortcut = "Ctrl+4",
            Purpose = "Live intake queue, triage decisions and contact context",
            Factory = () => new Workspaces.ServiceDeskView()
        },
        new()
        {
            Id = "tickets", Title = "Tickets", Group = "Respond", Icon = "I.Ticket", Shortcut = "Ctrl+5",
            Purpose = "The full ticket estate with SLA position and linkage",
            Factory = () => new Workspaces.TicketsView()
        },
        new()
        {
            Id = "incidents", Title = "Incidents", Group = "Respond", Icon = "I.Incident", Shortcut = "Ctrl+6",
            Purpose = "Major incident command, impact and timeline",
            Factory = () => new Workspaces.IncidentsView()
        },
        new()
        {
            Id = "diagnostics", Title = "Diagnostics", Group = "Diagnose", Icon = "I.Diagnostics", Shortcut = "Ctrl+7",
            Purpose = "Ordered dependency walks with expected against actual at every hop",
            Factory = () => new Workspaces.DiagnosticsView()
        },
        new()
        {
            Id = "infrastructure", Title = "Infrastructure", Group = "Diagnose", Icon = "I.Infrastructure", Shortcut = "Ctrl+8",
            Purpose = "The estate by layer, with dependents and blast radius",
            Factory = () => new Workspaces.InfrastructureView()
        },
        new()
        {
            Id = "simulator", Title = "Simulator", Group = "Diagnose", Icon = "I.Simulator", Shortcut = "Ctrl+9",
            Purpose = "The estate as one machine: disassemble, inspect, diagnose, reassemble",
            Factory = () => new Workspaces.SimulatorView()
        },
        new()
        {
            Id = "automation", Title = "Automation", Group = "Act", Icon = "I.Automation", Shortcut = "Ctrl+0",
            Purpose = "Gated runbooks: request, policy, pre-check, simulate, approve, verify, evidence",
            Factory = () => new Workspaces.AutomationView()
        },
        new()
        {
            Id = "changes", Title = "Changes", Group = "Act", Icon = "I.Changes",
            Purpose = "Proposals with dependencies, blast radius, risk, rollback and verification",
            Factory = () => new Workspaces.ChangesView()
        },
        new()
        {
            Id = "evidence", Title = "Evidence", Group = "Verify", Icon = "I.Evidence",
            Purpose = "Claim, source, check, result, authority and preservation",
            Factory = () => new Workspaces.EvidenceView()
        },
        new()
        {
            Id = "settings", Title = "Settings", Group = "System", Icon = "I.Settings",
            Purpose = "Boundaries, capabilities, rendering and safety contracts",
            Factory = () => new Workspaces.SettingsView()
        }
    };

    public static WorkspaceDefinition? ById(string id) =>
        All.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<IGrouping<string, WorkspaceDefinition>> Grouped =>
        All.GroupBy(w => w.Group);
}
