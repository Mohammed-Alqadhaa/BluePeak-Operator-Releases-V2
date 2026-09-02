using BluePeak.Domain;

namespace BluePeak.Simulation;

public enum StageKind
{
    /// <summary>Whole machine sealed, establishing shot.</summary>
    Establish,
    /// <summary>Chassis disengages; the operator sees the machine open.</summary>
    Disassemble,
    /// <summary>One module presented for close reading.</summary>
    Inspect,
    /// <summary>Following a dependency from one module to the next.</summary>
    Trace,
    /// <summary>The point at which expected and actual diverge.</summary>
    Diagnose,
    /// <summary>A gated action is staged or taken.</summary>
    Act,
    /// <summary>Proof that the intended state was reached.</summary>
    Verify,
    /// <summary>The machine closes and locks.</summary>
    Reassemble
}

/// <summary>One row in the inspection panel while a stage is on screen.</summary>
public readonly record struct DetailRow(string Label, string Value, HealthState Tone = HealthState.Unknown);

/// <summary>A single beat of a journey: a camera framing, a machine pose set, and what it means.</summary>
public sealed class JourneyStage
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required StageKind Kind { get; init; }
    public required double Duration { get; init; }

    /// <summary>One sentence of operator-facing narration.</summary>
    public required string Caption { get; init; }

    public string? FocusModuleId { get; init; }
    public CameraPose Camera { get; init; } = CameraPose.Establishing;
    public HealthState Verdict { get; init; } = HealthState.Unknown;

    /// <summary>Poses that differ from docked. Anything unlisted is treated as docked.</summary>
    public IReadOnlyDictionary<string, ModulePose> Poses { get; init; } =
        new Dictionary<string, ModulePose>();

    /// <summary>Relationships visible during this stage.</summary>
    public IReadOnlyList<SceneLink> Links { get; init; } = Array.Empty<SceneLink>();

    /// <summary>Protocol, expected, actual, evidence, impact — the inspection payload.</summary>
    public IReadOnlyList<DetailRow> Detail { get; init; } = Array.Empty<DetailRow>();

    public string? EvidenceId { get; init; }
    public string? ServiceId { get; init; }
}

public sealed class Journey
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Discipline { get; init; }
    public required string Question { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<JourneyStage> Stages { get; init; }

    public string? IncidentId { get; init; }
    public string? CaseId { get; init; }
    public string? TicketId { get; init; }
    public string? ChangeId { get; init; }
    public string? RunbookId { get; init; }
    public string? DiagnosticPathId { get; init; }
    public Severity Weight { get; init; } = Severity.High;

    /// <summary>Modules this journey actually exercises, in the order it reaches them.</summary>
    public IReadOnlyList<string> ModulePath { get; init; } = Array.Empty<string>();

    public double Duration => Stages.Sum(s => s.Duration);

    public double StartOf(int stageIndex)
    {
        double t = 0;
        for (int i = 0; i < stageIndex && i < Stages.Count; i++) t += Stages[i].Duration;
        return t;
    }
}
