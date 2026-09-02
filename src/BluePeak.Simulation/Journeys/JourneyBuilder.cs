using System.Numerics;
using BluePeak.Domain;

namespace BluePeak.Simulation.Journeys;

/// <summary>
/// Authoring helpers for journey definitions. Keeping camera framing derived from module
/// geometry rather than hand-written coordinates means a module can be repositioned in the
/// assembly without every journey needing new camera numbers.
/// </summary>
internal static class B
{
    public static CameraPose Wide(float azimuth = 38, float elevation = 17, float distance = 10.4f, float height = 0.55f, float fov = 42)
        => new(azimuth, elevation, distance, new Vector3(0, height, 0), fov);

    /// <summary>Frame a module that has travelled <paramref name="extract"/> out of the stack.</summary>
    public static CameraPose Look(string moduleId, float extract, float distance = 6.6f, float elevation = 11,
        float azimuthOffset = -20, float fov = 34, float heightBias = 0.06f)
    {
        var m = OperationsCore.Module(moduleId);
        if (m is null) return Wide();
        var dir = m.ExtractDirection;
        var centre = m.DockCentre + dir * (extract * 0.55f);
        return new CameraPose((float)m.Azimuth + azimuthOffset, elevation, distance,
            new Vector3(centre.X * 0.72f, centre.Y + heightBias, centre.Z * 0.72f), fov);
    }

    /// <summary>Frame the gap between two modules so a dependency reads as a relationship.</summary>
    public static CameraPose Between(string a, string b, float distance = 8.2f, float elevation = 14, float fov = 38)
    {
        var ma = OperationsCore.Module(a);
        var mb = OperationsCore.Module(b);
        if (ma is null || mb is null) return Wide();
        double aa = ma.Azimuth, ab = mb.Azimuth;
        double delta = ab - aa;
        while (delta > 180) delta -= 360;
        while (delta < -180) delta += 360;
        float mid = (float)(aa + delta / 2.0);
        float y = (float)((ma.Height + mb.Height) / 2.0);
        return new CameraPose(mid, elevation, distance, new Vector3(0, y + 0.1f, 0), fov);
    }

    public static Dictionary<string, ModulePose> Poses(params (string Id, ModulePose Pose)[] entries)
    {
        var d = new Dictionary<string, ModulePose>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, pose) in entries) d[id] = pose;
        return d;
    }

    /// <summary>Every module not named is pushed to the given emphasis so focus is unambiguous.</summary>
    public static Dictionary<string, ModulePose> Focus(float othersEmphasis, params (string Id, ModulePose Pose)[] entries)
    {
        var d = new Dictionary<string, ModulePose>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in OperationsCore.Modules) d[m.Id] = ModulePose.Secondary(othersEmphasis);
        foreach (var (id, pose) in entries) d[id] = pose;
        return d;
    }

    /// <summary>The whole machine standing off its seats — used for the disassembly beat.</summary>
    public static Dictionary<string, ModulePose> Bloom(float extract, float shellOpen, float emphasis = 0.82f,
        params (string Id, ModulePose Pose)[] overrides)
    {
        var d = new Dictionary<string, ModulePose>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in OperationsCore.Modules)
        {
            float e = m.Ring switch
            {
                0 => extract * 0.10f,
                4 => extract * 0.30f,
                _ => extract
            };
            float lift = m.Ring switch
            {
                0 => -extract * 0.34f,
                1 => -extract * 0.10f,
                3 => extract * 0.16f,
                4 => extract * 0.52f,
                _ => 0f
            };
            d[m.Id] = new ModulePose(e, lift, 0f, 0f, shellOpen * 0.55f, emphasis);
        }
        foreach (var (id, pose) in overrides) d[id] = pose;
        return d;
    }

    public static SceneLink Bus(string from, string label, HealthState state = HealthState.Healthy,
        float intensity = 1f, float flow = 1f)
        => new() { FromModuleId = from, ToModuleId = null, Label = label, Style = LinkStyle.Bus, State = state, Intensity = intensity, Flow = flow };

    public static SceneLink Dep(string from, string to, string label, HealthState state = HealthState.Healthy,
        float intensity = 1f, float flow = 1f)
        => new() { FromModuleId = from, ToModuleId = to, Label = label, Style = LinkStyle.Dependency, State = state, Intensity = intensity, Flow = flow };

    public static SceneLink Trust(string from, string to, string label, HealthState state = HealthState.Healthy,
        float intensity = 1f, float flow = 0.6f)
        => new() { FromModuleId = from, ToModuleId = to, Label = label, Style = LinkStyle.Trust, State = state, Intensity = intensity, Flow = flow };

    public static SceneLink Data(string from, string to, string label, HealthState state = HealthState.Healthy,
        float intensity = 1f, float flow = 0.4f)
        => new() { FromModuleId = from, ToModuleId = to, Label = label, Style = LinkStyle.Data, State = state, Intensity = intensity, Flow = flow };

    public static DetailRow Row(string label, string value, HealthState tone = HealthState.Unknown)
        => new(label, value, tone);

    public static JourneyStage Stage(string id, string title, StageKind kind, double duration, string caption,
        CameraPose camera, Dictionary<string, ModulePose>? poses = null, SceneLink[]? links = null,
        DetailRow[]? detail = null, string? focus = null, HealthState verdict = HealthState.Unknown,
        string? evidence = null, string? service = null)
        => new()
        {
            Id = id,
            Title = title,
            Kind = kind,
            Duration = duration,
            Caption = caption,
            Camera = camera,
            Poses = poses ?? new Dictionary<string, ModulePose>(),
            Links = links ?? Array.Empty<SceneLink>(),
            Detail = detail ?? Array.Empty<DetailRow>(),
            FocusModuleId = focus,
            Verdict = verdict,
            EvidenceId = evidence,
            ServiceId = service
        };
}
