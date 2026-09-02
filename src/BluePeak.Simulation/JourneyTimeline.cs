namespace BluePeak.Simulation;

/// <summary>Motion curves. Mechanical parts settle; they do not bounce.</summary>
public static class Easing
{
    /// <summary>Symmetric ease used for most part travel.</summary>
    public static float Mechanical(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
    }

    /// <summary>Decelerating arrival, used when a part docks into its seat.</summary>
    public static float Seat(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - MathF.Pow(1f - t, 4f);
    }

    /// <summary>Gentle camera curve: no snap at either end.</summary>
    public static float Camera(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static float Linear(float t) => Math.Clamp(t, 0f, 1f);
}

/// <summary>
/// Turns a journey into a pure function of time. Every visual property of the machine at
/// instant <c>t</c> is computed from the journey definition alone, with no dependence on
/// how the playhead arrived there. Scrubbing backwards, scrubbing forwards and resuming
/// after a scrub therefore reconstruct exactly the same state as ordinary playback.
/// </summary>
public sealed class JourneyTimeline
{
    private readonly double[] _starts;
    private readonly IReadOnlyDictionary<string, ModulePose>[] _resolved;

    public Journey Journey { get; }
    public double Duration { get; }
    public IReadOnlyList<double> StageStarts => _starts;

    public JourneyTimeline(Journey journey)
    {
        Journey = journey;
        _starts = new double[journey.Stages.Count];
        double acc = 0;
        for (int i = 0; i < journey.Stages.Count; i++)
        {
            _starts[i] = acc;
            acc += journey.Stages[i].Duration;
        }
        Duration = acc;

        // Resolve every stage to a complete pose set once, so evaluation never allocates
        // a fallback path and every module is always accounted for.
        _resolved = new IReadOnlyDictionary<string, ModulePose>[journey.Stages.Count];
        for (int i = 0; i < journey.Stages.Count; i++)
        {
            var map = new Dictionary<string, ModulePose>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in OperationsCore.Modules)
                map[module.Id] = journey.Stages[i].Poses.TryGetValue(module.Id, out var p) ? p : ModulePose.Docked;
            _resolved[i] = map;
        }
    }

    public int StageAt(double time)
    {
        if (Journey.Stages.Count == 0) return 0;
        double t = Math.Clamp(time, 0, Duration);
        for (int i = Journey.Stages.Count - 1; i >= 0; i--)
            if (t >= _starts[i]) return i;
        return 0;
    }

    public double StageStart(int index) => _starts[Math.Clamp(index, 0, _starts.Length - 1)];

    /// <summary>Time of the first frame of the given stage, used by step controls.</summary>
    public double SeekToStage(int index) => StageStart(Math.Clamp(index, 0, Journey.Stages.Count - 1));

    public SceneSnapshot Evaluate(double time)
    {
        if (Journey.Stages.Count == 0)
        {
            return new SceneSnapshot
            {
                Time = 0,
                StageIndex = 0,
                StageProgress = 0,
                Camera = CameraPose.Establishing,
                Poses = new Dictionary<string, ModulePose>(),
                Links = Array.Empty<LinkSnapshot>(),
                Expansion = 0
            };
        }

        double t = Math.Clamp(time, 0, Duration);
        int index = StageAt(t);
        var stage = Journey.Stages[index];
        double local = stage.Duration <= 0 ? 1 : (t - _starts[index]) / stage.Duration;
        local = Math.Clamp(local, 0, 1);

        var target = _resolved[index];
        var previous = index == 0 ? _resolved[0] : _resolved[index - 1];

        // The first stage has nothing to come from, so it holds its own pose set.
        float partK = index == 0 ? 1f : Easing.Mechanical((float)local);
        float camK = index == 0 ? 1f : Easing.Camera((float)local);

        var poses = new Dictionary<string, ModulePose>(target.Count, StringComparer.OrdinalIgnoreCase);
        float expansion = 0;
        foreach (var module in OperationsCore.Modules)
        {
            var a = previous[module.Id];
            var b = target[module.Id];
            var pose = index == 0 ? b : ModulePose.Lerp(a, b, partK);
            poses[module.Id] = pose;
            expansion += Math.Abs(pose.Extract) + Math.Abs(pose.Lift);
        }
        expansion = Math.Clamp(expansion / (OperationsCore.Modules.Count * 1.4f), 0f, 1f);

        var previousStage = index == 0 ? stage : Journey.Stages[index - 1];
        var links = ResolveLinks(previousStage, stage, index == 0 ? 1f : Easing.Camera((float)local));

        var camA = previousStage.Camera;
        var camB = stage.Camera;
        var camera = index == 0 ? camB : CameraPose.Lerp(camA, camB, camK);

        return new SceneSnapshot
        {
            Time = t,
            StageIndex = index,
            StageProgress = local,
            Camera = camera,
            Poses = poses,
            Links = links,
            Expansion = expansion,
            FocusModuleId = stage.FocusModuleId
        };
    }

    /// <summary>
    /// Links fade across a stage boundary rather than popping, so a dependency that carries
    /// over between two stages stays continuous on screen.
    /// </summary>
    private static IReadOnlyList<LinkSnapshot> ResolveLinks(JourneyStage from, JourneyStage to, float k)
    {
        var result = new List<LinkSnapshot>(from.Links.Count + to.Links.Count);
        static string Key(SceneLink l) => l.FromModuleId + "->" + (l.ToModuleId ?? "spine") + "|" + l.Label;

        var incoming = to.Links.ToDictionary(Key, l => l);
        var outgoing = from.Links.ToDictionary(Key, l => l);

        foreach (var (key, link) in incoming)
        {
            float startIntensity = outgoing.TryGetValue(key, out var prev) ? prev.Intensity : 0f;
            float intensity = startIntensity + (link.Intensity - startIntensity) * k;
            result.Add(new LinkSnapshot(link.FromModuleId, link.ToModuleId, link.Label, link.Style,
                link.State, intensity, link.Flow));
        }

        foreach (var (key, link) in outgoing)
        {
            if (incoming.ContainsKey(key)) continue;
            float intensity = link.Intensity * (1f - k);
            if (intensity <= 0.01f) continue;
            result.Add(new LinkSnapshot(link.FromModuleId, link.ToModuleId, link.Label, link.Style,
                link.State, intensity, link.Flow));
        }

        return result;
    }
}
