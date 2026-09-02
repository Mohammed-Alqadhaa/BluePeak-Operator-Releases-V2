using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.App.Services;
using BluePeak.Domain;
using BluePeak.Simulation;

namespace BluePeak.App.Workspaces;

/// <summary>A journey as the chooser presents it.</summary>
public sealed class JourneyCard
{
    public required Journey Journey { get; init; }
    public string Name => Journey.Name;
    public string Discipline => Journey.Discipline.ToUpperInvariant();
    public string Question => Journey.Question;
    public Severity Weight => Journey.Weight;
    public string StageSummary => $"{Journey.Stages.Count} stages · {TimeSpan.FromSeconds(Journey.Duration):m\\:ss}";
    public string LinkSummary => Journey.ModulePath.Count + " modules on path";

    /// <summary>The scenario picker renders the selected item through a content presenter.</summary>
    public override string ToString() => Name;
}

/// <summary>One inspection row in the simulator's right panel.</summary>
public sealed class DetailLine
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public HealthState Tone { get; init; } = HealthState.Unknown;
    public bool HasTone => Tone != HealthState.Unknown;
    public Brush ValueBrush => Tone == HealthState.Unknown
        ? Theme.Brush("B.TextPrimary")
        : Theme.ForHealth(Tone);
}

public sealed class LinkLine
{
    public required string Route { get; init; }
    public required string Label { get; init; }
    public required HealthState Tone { get; init; }
}

/// <summary>A destination the operator can carry the current stage's subject to.</summary>
public sealed record Handoff(string Caption, string Workspace, FocusKind Kind, string Id, string Label);

public static class SimulatorContext
{
    /// <summary>Health of each module, resolved from the estate node the module represents.</summary>
    public static Dictionary<string, HealthState> ModuleHealth()
    {
        var model = EstateService.Current.Model;
        var map = new Dictionary<string, HealthState>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in OperationsCore.Modules)
        {
            var node = model.Node(module.ServiceId);
            map[module.Id] = node?.Health ?? HealthState.Healthy;
        }
        return map;
    }

    /// <summary>Cross-workspace destinations available from the current stage.</summary>
    public static List<Handoff> HandoffsFor(Journey journey, JourneyStage stage)
    {
        var model = EstateService.Current.Model;
        var list = new List<Handoff>();

        if (stage.ServiceId is not null && model.Node(stage.ServiceId) is { } node)
            list.Add(new Handoff($"NOC · {node.Name}", "noc", FocusKind.Service, node.Id, node.Name));

        if (stage.EvidenceId is not null && model.EvidenceRecord(stage.EvidenceId) is { } evidence)
            list.Add(new Handoff($"Evidence · {evidence.Id}", "evidence", FocusKind.Evidence, evidence.Id, evidence.Claim));

        if (journey.IncidentId is not null && model.Incident(journey.IncidentId) is { } incident)
            list.Add(new Handoff($"Incident · {incident.Id}", "incidents", FocusKind.Incident, incident.Id, incident.Title));

        if (journey.CaseId is not null && model.Case(journey.CaseId) is { } securityCase)
            list.Add(new Handoff($"Case · {securityCase.Id}", "soc", FocusKind.Case, securityCase.Id, securityCase.Title));

        if (journey.TicketId is not null && model.Ticket(journey.TicketId) is { } ticket)
            list.Add(new Handoff($"Ticket · {ticket.Id}", "tickets", FocusKind.Ticket, ticket.Id, ticket.Subject));

        if (journey.ChangeId is not null && model.Change(journey.ChangeId) is { } change)
            list.Add(new Handoff($"Change · {change.Id}", "changes", FocusKind.Change, change.Id, change.Title));

        if (journey.RunbookId is not null && model.Runbook(journey.RunbookId) is { } runbook)
            list.Add(new Handoff($"Runbook · {runbook.Id}", "automation", FocusKind.Runbook, runbook.Id, runbook.Name));

        if (journey.DiagnosticPathId is not null &&
            model.DiagnosticPaths.FirstOrDefault(p => p.Id == journey.DiagnosticPathId) is { } path)
            list.Add(new Handoff($"Diagnostics · {path.Id}", "diagnostics", FocusKind.Service, path.Id, path.Name));

        return list;
    }

    public static string KindLabel(StageKind kind) => kind switch
    {
        StageKind.Establish => "ESTABLISH",
        StageKind.Disassemble => "DISASSEMBLE",
        StageKind.Inspect => "INSPECT",
        StageKind.Trace => "TRACE",
        StageKind.Diagnose => "DIAGNOSE",
        StageKind.Act => "ACT",
        StageKind.Verify => "VERIFY",
        StageKind.Reassemble => "REASSEMBLE",
        _ => kind.ToString().ToUpperInvariant()
    };

    public static Color KindColour(StageKind kind) => kind switch
    {
        StageKind.Diagnose => Color.FromRgb(0xE5, 0x54, 0x4B),
        StageKind.Act => Color.FromRgb(0xE0, 0xA3, 0x3E),
        StageKind.Verify => Color.FromRgb(0x3F, 0xB9, 0x8A),
        StageKind.Inspect or StageKind.Trace => Color.FromRgb(0x4C, 0x9D, 0xF0),
        _ => Color.FromRgb(0x6B, 0x76, 0x86)
    };
}
