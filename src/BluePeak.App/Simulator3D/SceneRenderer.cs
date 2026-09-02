using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BluePeak.Domain;
using BluePeak.Simulation;

namespace BluePeak.App.Simulator3D;

/// <summary>
/// Applies a <see cref="SceneSnapshot"/> to the 3D scene. The renderer holds no playback
/// state of its own: give it the same snapshot twice and it produces the same image, which
/// is what makes scrubbing exact and lifecycle recovery trivial.
/// </summary>
public sealed class SceneRenderer
{
    private readonly CoreScene _scene = new();
    private readonly PerspectiveCamera _camera;
    private readonly Viewport3D _viewport;
    private readonly Dictionary<string, HealthState> _moduleStates = new(StringComparer.OrdinalIgnoreCase);

    private double _pulseClock;
    private double _flowClock;

    public SceneRenderer()
    {
        _camera = new PerspectiveCamera
        {
            Position = new Point3D(0, 2, 10),
            LookDirection = new Vector3D(0, 0, -1),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = 42,
            NearPlaneDistance = 0.12,
            FarPlaneDistance = 90
        };

        _viewport = new Viewport3D
        {
            Camera = _camera,
            // Hit testing 3D is done in software; the inspector selects modules by name instead.
            IsHitTestVisible = false,
            ClipToBounds = false
        };
        _viewport.Children.Add(new ModelVisual3D { Content = _scene.Root });
    }

    public Viewport3D Viewport => _viewport;

    public IReadOnlyDictionary<string, ModuleVisual> Modules => _scene.Modules;

    /// <summary>Per-module health, used to colour the state accents on each chassis.</summary>
    public void SetModuleStates(IReadOnlyDictionary<string, HealthState> states)
    {
        _moduleStates.Clear();
        foreach (var (key, value) in states) _moduleStates[key] = value;
    }

    /// <summary>Advances the ambient clocks. Only affects pulse and flow, never part positions.</summary>
    public void Tick(double deltaSeconds, bool moving)
    {
        _pulseClock += deltaSeconds;
        // Flow only advances while the journey is running, so a paused frame is genuinely still.
        if (moving) _flowClock += deltaSeconds;
    }

    public void Apply(SceneSnapshot snapshot)
    {
        ApplyCamera(snapshot.Camera);

        double pulse = 0.5 + 0.5 * Math.Sin(_pulseClock * 2.1);

        foreach (var (id, visual) in _scene.Modules)
        {
            var pose = snapshot.Poses.TryGetValue(id, out var p) ? p : ModulePose.Docked;
            var definition = visual.Definition;

            var direction = MeshFactory.Radial(definition.Azimuth);
            if (definition.Sweep >= 359) direction = new Vector3D(0, 0, 0);

            visual.Offset.OffsetX = direction.X * pose.Extract;
            visual.Offset.OffsetZ = direction.Z * pose.Extract;
            visual.Offset.OffsetY = pose.Lift + (definition.Sweep >= 359 ? pose.Extract * 0.42 : 0);

            visual.SpinRotation.Angle = pose.Spin;
            visual.TiltRotation.Angle = pose.Tilt;

            // Clamshell: the halves separate vertically and ease outward as they part.
            double open = Math.Clamp(pose.ShellOpen, 0, 1);
            double lift = open * 0.42;
            double flare = open * 0.10;
            visual.ShellUpperOffset.OffsetY = lift;
            visual.ShellUpperOffset.OffsetX = direction.X * flare;
            visual.ShellUpperOffset.OffsetZ = direction.Z * flare;
            visual.ShellLowerOffset.OffsetY = -lift * 0.55;
            visual.ShellLowerOffset.OffsetX = direction.X * flare * 0.5;
            visual.ShellLowerOffset.OffsetZ = direction.Z * flare * 0.5;

            bool focused = string.Equals(snapshot.FocusModuleId, id, StringComparison.OrdinalIgnoreCase);
            var state = _moduleStates.TryGetValue(id, out var s) ? s : HealthState.Healthy;
            visual.Shade(pose.Emphasis, state, focused, pulse);

            // Mechanisms respond to being opened: the drum indexes, the collar unlocks,
            // the aperture closes in, the sensor head sweeps.
            if (visual.MechanismRotation is not null)
            {
                double drive = definition.Mechanism switch
                {
                    MechanismKind.IndexDrum => open * 128 + (focused ? _flowClock * 26 : 0),
                    MechanismKind.TrustVault => open * 42,
                    MechanismKind.InspectionAperture => open * -34 + (focused ? Math.Sin(_flowClock * 0.9) * 6 : 0),
                    MechanismKind.SensorDome => open * 26 + _flowClock * 7,
                    _ => open * 30
                };
                visual.MechanismRotation.Angle = drive;
            }

            if (visual.MechanismExtend is not null)
            {
                double travel = open * 0.16 * (focused ? 0.55 + 0.45 * pulse : 1.0);
                visual.MechanismExtend.OffsetX = direction.X * travel;
                visual.MechanismExtend.OffsetZ = direction.Z * travel;
            }
        }

        ApplyLinks(snapshot);
    }

    /// <summary>
    /// WPF measures FieldOfView horizontally, so a wide viewport sees far less vertically than
    /// the number suggests and a machine framed on paper ends up cropped top and bottom.
    /// Journeys author distance against the vertical extent; this converts to the real viewport.
    /// </summary>
    private double FramingDistance(double authored)
    {
        double width = _viewport.ActualWidth;
        double height = _viewport.ActualHeight;
        double aspect = height > 1 && width > 1 ? width / height : 1.6;
        aspect = Math.Clamp(aspect, 1.0, 2.6);
        return authored * (0.78 + 0.52 * aspect);
    }

    private void ApplyCamera(CameraPose pose)
    {
        double azimuth = pose.Azimuth * Math.PI / 180;
        double elevation = pose.Elevation * Math.PI / 180;
        double distance = FramingDistance(pose.Distance);
        double horizontal = Math.Cos(elevation) * distance;

        var position = new Point3D(
            pose.Target.X + Math.Cos(azimuth) * horizontal,
            pose.Target.Y + Math.Sin(elevation) * distance,
            pose.Target.Z + Math.Sin(azimuth) * horizontal);

        _camera.Position = position;
        _camera.LookDirection = new Point3D(pose.Target.X, pose.Target.Y, pose.Target.Z) - position;
        _camera.FieldOfView = pose.FieldOfView;
    }

    private void ApplyLinks(SceneSnapshot snapshot)
    {
        int index = 0;
        foreach (var link in snapshot.Links)
        {
            if (index >= _scene.Links.Count) break;
            if (link.Intensity <= 0.02) continue;

            var from = _scene.Modules.TryGetValue(link.FromModuleId, out var f) ? f : null;
            if (from is null) continue;

            Point3D a = from.CurrentAnchor;
            Point3D b;

            if (link.ToModuleId is null)
            {
                b = CoreScene.SpineAnchor(from.Definition);
            }
            else
            {
                var to = _scene.Modules.TryGetValue(link.ToModuleId, out var t) ? t : null;
                if (to is null) continue;
                b = to.CurrentAnchor;
            }

            var visual = _scene.Links[index++];
            double radius = link.Style switch
            {
                LinkStyle.Bus => 0.011,
                LinkStyle.Trust => 0.009,
                LinkStyle.Data => 0.008,
                _ => 0.010
            };

            var colour = link.Style == LinkStyle.Data && link.State == HealthState.Healthy
                ? Palette3D.Accent
                : Palette3D.State(link.State);

            double phase = _flowClock * Math.Max(0.05, link.Flow) * 0.55;
            bool pulse = link.Flow > 0.05 && Services.AppSettings.Current.LinkFlow;
            visual.Span(a, b, radius, colour, 0.35 + 0.65 * link.Intensity, phase, pulse);
        }

        for (; index < _scene.Links.Count; index++) _scene.Links[index].Hide();
    }

    /// <summary>Frames the whole machine, used when the simulator is idle before a journey runs.</summary>
    public void ApplyIdle(double clock)
    {
        double drift = Services.AppSettings.Current.IdleDrift ? Math.Sin(clock * 0.12) * 9 : 0;
        var pose = new CameraPose(
            (float)(26 + drift),
            17f, 11.4f, new System.Numerics.Vector3(0, 0.5f, 0), 40f);
        ApplyCamera(pose);

        double pulse = 0.5 + 0.5 * Math.Sin(_pulseClock * 1.6);
        foreach (var (id, visual) in _scene.Modules)
        {
            visual.Offset.OffsetX = visual.Offset.OffsetY = visual.Offset.OffsetZ = 0;
            visual.SpinRotation.Angle = 0;
            visual.TiltRotation.Angle = 0;
            visual.ShellUpperOffset.OffsetY = visual.ShellLowerOffset.OffsetY = 0;
            visual.ShellUpperOffset.OffsetX = visual.ShellUpperOffset.OffsetZ = 0;
            visual.ShellLowerOffset.OffsetX = visual.ShellLowerOffset.OffsetZ = 0;
            if (visual.MechanismRotation is not null) visual.MechanismRotation.Angle = 0;
            if (visual.MechanismExtend is not null)
                visual.MechanismExtend.OffsetX = visual.MechanismExtend.OffsetZ = 0;

            var state = _moduleStates.TryGetValue(id, out var s) ? s : HealthState.Healthy;
            visual.Shade(0.92f, state, false, pulse);
        }

        foreach (var link in _scene.Links) link.Hide();
    }
}
