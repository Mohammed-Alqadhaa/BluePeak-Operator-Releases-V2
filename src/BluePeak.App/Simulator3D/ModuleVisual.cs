using System.Windows.Media;
using System.Windows.Media.Media3D;
using BluePeak.Domain;
using BluePeak.Simulation;

namespace BluePeak.App.Simulator3D;

/// <summary>
/// One assembled module in the scene, with the handful of mutable handles the renderer needs.
/// Geometry is built once and frozen; only transforms and brush colours change per frame,
/// which is what keeps a full-scene update inside a frame budget.
/// </summary>
public sealed class ModuleVisual
{
    public required SceneModule Definition { get; init; }
    public required Model3DGroup Root { get; init; }

    public required AxisAngleRotation3D SpinRotation { get; init; }
    public required AxisAngleRotation3D TiltRotation { get; init; }
    public required TranslateTransform3D Offset { get; init; }

    /// <summary>Clamshell halves. These separate to expose the mechanism inside.</summary>
    public required TranslateTransform3D ShellUpperOffset { get; init; }
    public required TranslateTransform3D ShellLowerOffset { get; init; }

    /// <summary>Mechanisms that rotate under inspection, such as the resolver drum.</summary>
    public AxisAngleRotation3D? MechanismRotation { get; init; }

    /// <summary>Parts that extend, such as the automation piston.</summary>
    public TranslateTransform3D? MechanismExtend { get; init; }

    public required IReadOnlyList<TintedSurface> Surfaces { get; init; }

    /// <summary>Where the module's connector bus terminates, in docked machine space.</summary>
    public required Point3D ConnectorAnchor { get; init; }

    public Point3D DockCentre => new(Definition.DockCentre.X, Definition.DockCentre.Y, Definition.DockCentre.Z);

    /// <summary>Current connector position, accounting for how far the module has travelled.</summary>
    public Point3D CurrentAnchor => new(
        ConnectorAnchor.X + Offset.OffsetX,
        ConnectorAnchor.Y + Offset.OffsetY,
        ConnectorAnchor.Z + Offset.OffsetZ);

    /// <summary>Applies emphasis and state tint. Dimming is by value, never by transparency.</summary>
    public void Shade(float emphasis, HealthState state, bool focused, double pulse)
    {
        foreach (var surface in Surfaces)
        {
            Color target = surface.Role switch
            {
                // Healthy is deliberately quiet. A machine where everything glows tells the
                // operator nothing, so only a fault is allowed to be bright.
                SurfaceRole.StateAccent => Palette3D.Scale(Palette3D.State(state), state switch
                {
                    HealthState.Healthy => 0.34,
                    HealthState.Maintenance => 0.50,
                    HealthState.Unknown => 0.38,
                    _ => 1.0
                }),
                SurfaceRole.FocusAccent => focused ? Palette3D.Accent : surface.BaseColor,
                _ => surface.BaseColor
            };

            if (surface.Role == SurfaceRole.StateAccent && focused)
                target = Palette3D.Scale(target, 1.0 + 0.16 * pulse);

            // A focused module is lifted in value and pulled very slightly toward the selection
            // colour, so the eye lands on it without the scene turning blue.
            if (focused && surface.Role is SurfaceRole.Structure or SurfaceRole.Mechanism)
                target = Palette3D.Lerp(Palette3D.Scale(target, 1.34), Palette3D.Accent, 0.10);

            // Recede toward the ambient value rather than fading out, so depth stays readable.
            var shaded = Palette3D.Lerp(Palette3D.Recede, target, 0.16 + 0.84 * emphasis);
            surface.Brush.Color = shaded;
        }
    }
}

public enum SurfaceRole
{
    /// <summary>Chassis and frame. Neutral, dimmable.</summary>
    Structure,
    /// <summary>Machined internals. Slightly brighter than the chassis.</summary>
    Mechanism,
    /// <summary>Takes the health colour of the subsystem.</summary>
    StateAccent,
    /// <summary>Takes the selection colour when the module is the inspection focus.</summary>
    FocusAccent,
    /// <summary>Connector metal. Stays bright so interfaces read when exposed.</summary>
    Interface
}

public sealed record TintedSurface(SolidColorBrush Brush, Color BaseColor, SurfaceRole Role);
