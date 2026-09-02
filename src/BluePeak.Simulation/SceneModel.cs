using System.Numerics;
using BluePeak.Domain;

namespace BluePeak.Simulation;

/// <summary>
/// The internal mechanism a module carries. The shell of every module is a chassis wedge so
/// the assembled machine reads as one object; the mechanism is what distinguishes a resolver
/// from a trust vault when the shell opens.
/// </summary>
public enum MechanismKind
{
    /// <summary>Fan of connector blades — request ingress.</summary>
    PortArray,
    /// <summary>Cross-bar lattice plate — switching.</summary>
    SwitchLattice,
    /// <summary>Beveled prism with directional vanes — routing decisions.</summary>
    RoutingPrism,
    /// <summary>Rotating indexed drum with radial fins — name resolution.</summary>
    IndexDrum,
    /// <summary>Sealed cylinder behind a keyed collar — identity and trust.</summary>
    TrustVault,
    /// <summary>Stack of thin service plates — application workload.</summary>
    ServiceStack,
    /// <summary>Sensor dome over a gimbal ring — monitoring and control.</summary>
    SensorDome,
    /// <summary>Concentric aperture rings — security inspection.</summary>
    InspectionAperture,
    /// <summary>Piston and cylinder — gated automation action.</summary>
    Actuator,
    /// <summary>Sealed archival block with a ledger stack — evidence.</summary>
    ArchiveVault
}

/// <summary>One functional module of the BluePeak Operations Core.</summary>
public sealed class SceneModule
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required MechanismKind Mechanism { get; init; }
    public required EstateLayer Layer { get; init; }

    /// <summary>Ring index: 0 = plinth, 1..3 = stacked rings, 4 = crown.</summary>
    public required int Ring { get; init; }

    /// <summary>Angle of the wedge centre, degrees, measured about the vertical axis.</summary>
    public required double Azimuth { get; init; }

    /// <summary>Height of the ring the module belongs to.</summary>
    public required double Height { get; init; }

    /// <summary>Angular width of the chassis wedge, degrees.</summary>
    public double Sweep { get; init; } = 112;

    public double InnerRadius { get; init; } = 0.62;
    public double OuterRadius { get; init; } = 1.62;
    public double Thickness { get; init; } = 0.94;

    /// <summary>Estate node this module represents, so inspection can reach real data.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Outward unit direction the module extracts along.</summary>
    public Vector3 ExtractDirection
    {
        get
        {
            double r = Azimuth * Math.PI / 180.0;
            return new Vector3((float)Math.Cos(r), 0, (float)Math.Sin(r));
        }
    }

    /// <summary>Centre of the docked module in machine space.</summary>
    public Vector3 DockCentre
    {
        get
        {
            // A full-sweep module is a body of revolution on the machine axis, so its centre is
            // the axis itself. Treating it like a wedge would place it off-centre and every
            // camera framed on it would be wrong.
            if (Sweep >= 359) return new Vector3(0, (float)Height, 0);

            double r = Azimuth * Math.PI / 180.0;
            double mid = (InnerRadius + OuterRadius) * 0.5;
            return new Vector3((float)(Math.Cos(r) * mid), (float)Height, (float)(Math.Sin(r) * mid));
        }
    }
}

/// <summary>
/// A module's deviation from its docked state. Everything about the visual state of the
/// machine at any instant is expressible as one pose per module, which is what makes
/// scrubbing exact rather than approximate.
/// </summary>
public readonly record struct ModulePose(
    float Extract,
    float Lift,
    float Spin,
    float Tilt,
    float ShellOpen,
    float Emphasis)
{
    /// <summary>Fully seated, sealed, at normal emphasis.</summary>
    public static readonly ModulePose Docked = new(0f, 0f, 0f, 0f, 0f, 1f);

    /// <summary>Seated but visually recessive while another module holds attention.</summary>
    public static ModulePose Secondary(float emphasis = 0.28f) => new(0f, 0f, 0f, 0f, 0f, emphasis);

    public static ModulePose Extracted(float extract, float shellOpen = 1f, float spin = 0f,
        float tilt = 0f, float lift = 0f, float emphasis = 1f)
        => new(extract, lift, spin, tilt, shellOpen, emphasis);

    public static ModulePose Lerp(ModulePose a, ModulePose b, float k) => new(
        a.Extract + (b.Extract - a.Extract) * k,
        a.Lift + (b.Lift - a.Lift) * k,
        a.Spin + (b.Spin - a.Spin) * k,
        a.Tilt + (b.Tilt - a.Tilt) * k,
        a.ShellOpen + (b.ShellOpen - a.ShellOpen) * k,
        a.Emphasis + (b.Emphasis - a.Emphasis) * k);
}

/// <summary>Camera state expressed in orbit coordinates so interpolation never tumbles.</summary>
public readonly record struct CameraPose(
    float Azimuth,
    float Elevation,
    float Distance,
    Vector3 Target,
    float FieldOfView)
{
    public static readonly CameraPose Establishing = new(38f, 17f, 10.4f, new Vector3(0, 0.55f, 0), 42f);

    public static CameraPose Lerp(CameraPose a, CameraPose b, float k)
    {
        // Take the short way round so a stage never spins the machine the long way.
        float da = b.Azimuth - a.Azimuth;
        while (da > 180f) da -= 360f;
        while (da < -180f) da += 360f;
        return new CameraPose(
            a.Azimuth + da * k,
            a.Elevation + (b.Elevation - a.Elevation) * k,
            a.Distance + (b.Distance - a.Distance) * k,
            Vector3.Lerp(a.Target, b.Target, k),
            a.FieldOfView + (b.FieldOfView - a.FieldOfView) * k);
    }
}

public enum LinkStyle
{
    /// <summary>A dependency the request actually traverses.</summary>
    Dependency,
    /// <summary>A trust or authorisation relationship.</summary>
    Trust,
    /// <summary>Data or evidence movement.</summary>
    Data,
    /// <summary>The bus attaching a module to the spine.</summary>
    Bus
}

/// <summary>A visible relationship between two modules, or between a module and the spine.</summary>
public sealed class SceneLink
{
    public required string FromModuleId { get; init; }
    /// <summary>Null means the link terminates on the central spine.</summary>
    public string? ToModuleId { get; init; }
    public required string Label { get; init; }
    public LinkStyle Style { get; init; } = LinkStyle.Dependency;
    public HealthState State { get; init; } = HealthState.Healthy;
    public float Intensity { get; init; } = 1f;
    /// <summary>Animated flow along the link, 0 = static.</summary>
    public float Flow { get; init; } = 1f;
}

/// <summary>Resolved link with its interpolated intensity for a given instant.</summary>
public readonly record struct LinkSnapshot(
    string FromModuleId,
    string? ToModuleId,
    string Label,
    LinkStyle Style,
    HealthState State,
    float Intensity,
    float Flow);

/// <summary>The complete visual state of the machine at one instant. Pure output of Evaluate.</summary>
public sealed class SceneSnapshot
{
    public required double Time { get; init; }
    public required int StageIndex { get; init; }
    public required double StageProgress { get; init; }
    public required CameraPose Camera { get; init; }
    public required IReadOnlyDictionary<string, ModulePose> Poses { get; init; }
    public required IReadOnlyList<LinkSnapshot> Links { get; init; }
    /// <summary>0 while sealed, 1 when the machine is fully open. Drives ambient response.</summary>
    public required float Expansion { get; init; }
    public string? FocusModuleId { get; init; }
}
