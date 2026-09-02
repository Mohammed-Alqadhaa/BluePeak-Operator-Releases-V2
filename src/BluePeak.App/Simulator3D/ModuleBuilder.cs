using System.Windows.Media;
using System.Windows.Media.Media3D;
using BluePeak.Simulation;

namespace BluePeak.App.Simulator3D;

/// <summary>
/// Builds one module: a clamshell chassis wedge, a structural frame, a docking interface,
/// and a mechanism whose form is specific to what the subsystem actually does. The mechanism
/// is the reason a resolver does not look like a trust vault when the machine opens.
/// </summary>
public static class ModuleBuilder
{
    private const int ArcSegments = 16;

    /// <summary>Local frame at a module's mechanism bay: x tangential, y up, z outward.</summary>
    private readonly struct Bay
    {
        private readonly Point3D _origin;
        private readonly Vector3D _tangent, _up, _out;

        public Bay(double azimuth, double radius, double height)
        {
            _origin = MeshFactory.Polar(azimuth, radius, height);
            _tangent = MeshFactory.Tangent(azimuth);
            _up = new Vector3D(0, 1, 0);
            _out = MeshFactory.Radial(azimuth);
        }

        public Point3D At(double x, double y, double z) =>
            _origin + _tangent * x + _up * y + _out * z;

        public Vector3D Out => _out;
        public Vector3D Tangent => _tangent;
        public Vector3D Up => _up;
    }

    private sealed class Surfaces
    {
        public readonly List<TintedSurface> All = new();

        public SolidColorBrush Add(Color colour, SurfaceRole role)
        {
            var brush = new SolidColorBrush(colour);
            All.Add(new TintedSurface(brush, colour, role));
            return brush;
        }
    }

    public static ModuleVisual Build(SceneModule module)
    {
        var surfaces = new Surfaces();
        var root = new Model3DGroup();

        double y0 = module.Height - module.Thickness / 2;
        double y1 = module.Height + module.Thickness / 2;
        double mid = module.Height;
        double start = module.Azimuth - module.Sweep / 2;

        // ---- Clamshell halves --------------------------------------------------------
        var upperGroup = new Model3DGroup();
        var lowerGroup = new Model3DGroup();
        var shellUpperOffset = new TranslateTransform3D();
        var shellLowerOffset = new TranslateTransform3D();
        upperGroup.Transform = shellUpperOffset;
        lowerGroup.Transform = shellLowerOffset;

        if (module.Sweep >= 359)
        {
            BuildRoundShell(upperGroup, lowerGroup, surfaces, module, y0, y1, mid);
        }
        else
        {
            BuildWedgeShell(upperGroup, lowerGroup, surfaces, module, y0, y1, mid, start);
        }

        root.Children.Add(upperGroup);
        root.Children.Add(lowerGroup);

        // ---- Structural frame, always visible once the shell opens --------------------
        var frame = new MeshFactory.Builder();
        double frameInner = module.InnerRadius + 0.03;
        double frameOuter = module.OuterRadius - 0.20;
        if (module.Sweep >= 359)
        {
            MeshFactory.Tube(frame, new Point3D(0, mid - 0.035, 0), new Vector3D(0, 1, 0),
                Math.Max(0.02, module.OuterRadius - 0.34), module.OuterRadius - 0.22, 0.07, 26);
        }
        else
        {
            // Two radial ribs and a floor rail: the cage a module is actually mounted in.
            MeshFactory.ArcWedge(frame, frameInner, frameOuter, mid - 0.30, mid - 0.245, start + 3, module.Sweep - 6, ArcSegments, 0.012);
            MeshFactory.ArcWedge(frame, frameInner, frameOuter, mid - 0.30, mid + 0.30, start + 3, 3.2, 3, 0.012);
            MeshFactory.ArcWedge(frame, frameInner, frameOuter, mid - 0.30, mid + 0.30, start + module.Sweep - 6.2, 3.2, 3, 0.012);
        }
        root.Children.Add(new GeometryModel3D(frame.ToMesh(),
            Palette3D.Structural(surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Structure), 0.10)));

        // ---- Docking interface --------------------------------------------------------
        var connectorAnchor = BuildInterface(root, surfaces, module, mid);

        // ---- Mechanism -----------------------------------------------------------------
        var mechanismGroup = new Model3DGroup();
        AxisAngleRotation3D? mechanismRotation = null;
        TranslateTransform3D? mechanismExtend = null;

        double bayRadius = module.Sweep >= 359 ? 0 : (module.InnerRadius + module.OuterRadius) / 2 - 0.04;
        var bay = new Bay(module.Azimuth, bayRadius, mid);

        switch (module.Mechanism)
        {
            case MechanismKind.PortArray: BuildPortArray(mechanismGroup, surfaces, bay); break;
            case MechanismKind.SwitchLattice: BuildSwitchLattice(mechanismGroup, surfaces, bay); break;
            case MechanismKind.RoutingPrism: BuildRoutingPrism(mechanismGroup, surfaces, bay); break;
            case MechanismKind.IndexDrum: mechanismRotation = BuildIndexDrum(mechanismGroup, surfaces, bay); break;
            case MechanismKind.TrustVault: mechanismRotation = BuildTrustVault(mechanismGroup, surfaces, bay); break;
            case MechanismKind.ServiceStack: BuildServiceStack(mechanismGroup, surfaces, bay, module); break;
            case MechanismKind.SensorDome: mechanismRotation = BuildSensorDome(mechanismGroup, surfaces, bay); break;
            case MechanismKind.InspectionAperture: mechanismRotation = BuildInspectionAperture(mechanismGroup, surfaces, bay); break;
            case MechanismKind.Actuator: mechanismExtend = BuildActuator(mechanismGroup, surfaces, bay); break;
            case MechanismKind.ArchiveVault: BuildArchiveVault(mechanismGroup, surfaces, bay); break;
        }
        root.Children.Add(mechanismGroup);

        // ---- Module transform ----------------------------------------------------------
        var dock = module.DockCentre;
        var spin = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        var tilt = new AxisAngleRotation3D(MeshFactory.Tangent(module.Azimuth), 0);
        var offset = new TranslateTransform3D();

        var transform = new Transform3DGroup();
        transform.Children.Add(new RotateTransform3D(spin, dock.X, dock.Y, dock.Z));
        transform.Children.Add(new RotateTransform3D(tilt, dock.X, dock.Y, dock.Z));
        transform.Children.Add(offset);
        root.Transform = transform;

        return new ModuleVisual
        {
            Definition = module,
            Root = root,
            SpinRotation = spin,
            TiltRotation = tilt,
            Offset = offset,
            ShellUpperOffset = shellUpperOffset,
            ShellLowerOffset = shellLowerOffset,
            MechanismRotation = mechanismRotation,
            MechanismExtend = mechanismExtend,
            Surfaces = surfaces.All,
            ConnectorAnchor = connectorAnchor
        };
    }

    // ------------------------------------------------------------------ shells

    private static void BuildWedgeShell(Model3DGroup upper, Model3DGroup lower, Surfaces surfaces,
        SceneModule module, double y0, double y1, double mid, double start)
    {
        double skinInner = module.OuterRadius - 0.17;
        double deckThickness = 0.055;
        double gap = 0.012;

        var faceUpper = surfaces.Add(Palette3D.ShellFace, SurfaceRole.Structure);
        var faceLower = surfaces.Add(Palette3D.ShellFace, SurfaceRole.Structure);
        var edgeUpper = surfaces.Add(Palette3D.ShellEdge, SurfaceRole.Structure);
        var edgeLower = surfaces.Add(Palette3D.ShellEdge, SurfaceRole.Structure);

        // Upper half: outer skin band plus the top deck.
        var us = new MeshFactory.Builder();
        MeshFactory.ArcWedge(us, skinInner, module.OuterRadius, mid + gap, y1 - deckThickness, start, module.Sweep, ArcSegments, 0.05);
        upper.Children.Add(new GeometryModel3D(us.ToMesh(), Palette3D.Structural(faceUpper, 0.20)));

        var ud = new MeshFactory.Builder();
        MeshFactory.ArcWedge(ud, module.InnerRadius, module.OuterRadius, y1 - deckThickness, y1, start, module.Sweep, ArcSegments, 0.02);
        upper.Children.Add(new GeometryModel3D(ud.ToMesh(), Palette3D.Structural(edgeUpper, 0.26)));

        // Lower half: outer skin band, bottom deck, and a backing plate that closes the bay.
        var ls = new MeshFactory.Builder();
        MeshFactory.ArcWedge(ls, skinInner, module.OuterRadius, y0 + deckThickness, mid - gap, start, module.Sweep, ArcSegments, 0.05);
        lower.Children.Add(new GeometryModel3D(ls.ToMesh(), Palette3D.Structural(faceLower, 0.20)));

        var ld = new MeshFactory.Builder();
        MeshFactory.ArcWedge(ld, module.InnerRadius, module.OuterRadius, y0, y0 + deckThickness, start, module.Sweep, ArcSegments, 0.02);
        MeshFactory.ArcWedge(ld, module.InnerRadius, module.InnerRadius + 0.045, y0, mid - gap, start, module.Sweep, ArcSegments, 0.014);
        lower.Children.Add(new GeometryModel3D(ld.ToMesh(), Palette3D.Structural(edgeLower, 0.26)));

        // A hairline indicator strip on the outer skin: where the module reports its own state.
        var strip = new MeshFactory.Builder();
        MeshFactory.ArcWedge(strip, module.OuterRadius - 0.005, module.OuterRadius + 0.012,
            mid + gap + 0.05, mid + gap + 0.105, start + module.Sweep * 0.30, module.Sweep * 0.40, ArcSegments, 0.004);
        upper.Children.Add(new GeometryModel3D(strip.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
    }

    private static void BuildRoundShell(Model3DGroup upper, Model3DGroup lower, Surfaces surfaces,
        SceneModule module, double y0, double y1, double mid)
    {
        var face = surfaces.Add(Palette3D.Plinth, SurfaceRole.Structure);
        var edge = surfaces.Add(Palette3D.ShellEdge, SurfaceRole.Structure);

        if (module.Ring == 0)
        {
            // Foundation plinth: a broad hexagonal base with a chamfered cap and six feet.
            var b = new MeshFactory.Builder();
            MeshFactory.Prism(b, new Point3D(0, y0, 0), module.OuterRadius, module.Thickness * 0.72, 6, 0, module.OuterRadius * 0.965);
            lower.Children.Add(new GeometryModel3D(b.ToMesh(), Palette3D.Structural(face, 0.14)));

            var cap = new MeshFactory.Builder();
            MeshFactory.Prism(cap, new Point3D(0, y0 + module.Thickness * 0.72, 0), module.OuterRadius * 0.965,
                module.Thickness * 0.28, 6, 0, module.OuterRadius * 0.80);
            MeshFactory.Tube(cap, new Point3D(0, y1 - 0.02, 0), new Vector3D(0, 1, 0), 0.52, 0.78, 0.05, 28);
            upper.Children.Add(new GeometryModel3D(cap.ToMesh(), Palette3D.Structural(edge, 0.22)));

            var feet = new MeshFactory.Builder();
            for (int i = 0; i < 6; i++)
            {
                double a = i * 60 + 30;
                MeshFactory.Box(feet, MeshFactory.Polar(a, module.OuterRadius * 0.80, y0 - 0.055), 0.30, 0.11, 0.30, 0.03);
            }
            lower.Children.Add(new GeometryModel3D(feet.ToMesh(),
                Palette3D.Structural(surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Structure), 0.10)));

            var ring = new MeshFactory.Builder();
            MeshFactory.Torus(ring, new Point3D(0, y0 + module.Thickness * 0.72, 0), new Vector3D(0, 1, 0),
                module.OuterRadius * 0.90, 0.016, 40, 8);
            upper.Children.Add(new GeometryModel3D(ring.ToMesh(),
                Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
            return;
        }

        // Evidence crown: a sealed vault that lifts off its seat.
        var body = new MeshFactory.Builder();
        MeshFactory.Prism(body, new Point3D(0, y0, 0), module.OuterRadius, module.Thickness * 0.58, 6, 30);
        lower.Children.Add(new GeometryModel3D(body.ToMesh(), Palette3D.Structural(face, 0.20)));

        var lid = new MeshFactory.Builder();
        MeshFactory.Prism(lid, new Point3D(0, y0 + module.Thickness * 0.58, 0), module.OuterRadius,
            module.Thickness * 0.30, 6, 30, module.OuterRadius * 0.62);
        MeshFactory.Prism(lid, new Point3D(0, y0 + module.Thickness * 0.88, 0), module.OuterRadius * 0.62,
            module.Thickness * 0.12, 6, 30, module.OuterRadius * 0.44);
        upper.Children.Add(new GeometryModel3D(lid.ToMesh(), Palette3D.Structural(edge, 0.30)));

        var seal = new MeshFactory.Builder();
        MeshFactory.Torus(seal, new Point3D(0, y0 + module.Thickness * 0.58, 0), new Vector3D(0, 1, 0),
            module.OuterRadius * 0.86, 0.02, 36, 8);
        upper.Children.Add(new GeometryModel3D(seal.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
    }

    // ------------------------------------------------------------------ interface

    private static Point3D BuildInterface(Model3DGroup root, Surfaces surfaces, SceneModule module, double mid)
    {
        var metal = surfaces.Add(Palette3D.Connector, SurfaceRole.Interface);
        var block = surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Structure);

        if (module.Sweep >= 359)
        {
            // Round modules dock through a central spigot rather than a side connector.
            double y = module.Ring == 0 ? mid + module.Thickness / 2 : mid - module.Thickness / 2;
            var b = new MeshFactory.Builder();
            MeshFactory.Cylinder(b, new Point3D(0, y - 0.10, 0), new Vector3D(0, 1, 0), 0.13, 0.20, 16);
            root.Children.Add(new GeometryModel3D(b.ToMesh(), Palette3D.Structural(block, 0.12)));

            var pins = new MeshFactory.Builder();
            for (int i = 0; i < 6; i++)
            {
                double a = i * 60;
                MeshFactory.Cylinder(pins, MeshFactory.Polar(a, 0.075, y - 0.19), new Vector3D(0, 1, 0), 0.014, 0.10, 8);
            }
            root.Children.Add(new GeometryModel3D(pins.ToMesh(), Palette3D.Structural(metal, 0.45, 46)));
            return new Point3D(0, y - 0.14, 0);
        }

        // Wedge modules dock inward to the spine through a pin block.
        var bay = new Bay(module.Azimuth, module.InnerRadius + 0.075, mid);
        var body = new MeshFactory.Builder();
        MeshFactory.Box(body, bay.At(0, 0, 0), 0.34, 0.22, 0.10, 0.022);
        root.Children.Add(new GeometryModel3D(body.ToMesh(), Palette3D.Structural(block, 0.12)));

        var pinMesh = new MeshFactory.Builder();
        for (int i = 0; i < 5; i++)
        {
            double x = -0.12 + i * 0.06;
            MeshFactory.Cylinder(pinMesh, bay.At(x, 0, -0.05), -bay.Out, 0.013, 0.085, 8);
        }
        MeshFactory.Box(pinMesh, bay.At(0, 0.085, -0.02), 0.30, 0.018, 0.05, 0.006);
        root.Children.Add(new GeometryModel3D(pinMesh.ToMesh(), Palette3D.Structural(metal, 0.45, 46)));

        return bay.At(0, 0, -0.12);
    }

    // ------------------------------------------------------------------ mechanisms

    private static GeometryModel3D Model(MeshFactory.Builder b, SolidColorBrush brush, double specular = 0.24, double power = 32)
        => new(b.ToMesh(), Palette3D.Structural(brush, specular, power));

    /// <summary>Request ingress: a fan of connector blades behind a slotted faceplate.</summary>
    private static void BuildPortArray(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var blades = new MeshFactory.Builder();
        for (int i = 0; i < 9; i++)
        {
            double x = -0.34 + i * 0.085;
            double h = 0.30 + (i % 3) * 0.045;
            MeshFactory.Box(blades, bay.At(x, 0.02, -0.02), 0.030, h, 0.26, 0.008);
        }
        group.Children.Add(Model(blades, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.30));

        var spine = new MeshFactory.Builder();
        MeshFactory.Box(spine, bay.At(0, -0.20, 0), 0.80, 0.055, 0.30, 0.014);
        MeshFactory.Box(spine, bay.At(0, 0.02, 0.16), 0.80, 0.34, 0.035, 0.010);
        group.Children.Add(Model(spine, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.16));

        var ports = new MeshFactory.Builder();
        for (int i = 0; i < 9; i++)
            MeshFactory.Box(ports, bay.At(-0.34 + i * 0.085, 0.02, 0.18), 0.022, 0.16, 0.012, 0);
        group.Children.Add(new GeometryModel3D(ports.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
    }

    /// <summary>Switching fabric: a crossbar lattice with visible bundle members.</summary>
    private static void BuildSwitchLattice(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var lattice = new MeshFactory.Builder();
        for (int i = 0; i < 6; i++)
            MeshFactory.Box(lattice, bay.At(-0.32 + i * 0.128, 0, 0), 0.026, 0.44, 0.20, 0.006);
        for (int j = 0; j < 4; j++)
            MeshFactory.Box(lattice, bay.At(0, -0.18 + j * 0.12, 0), 0.78, 0.024, 0.20, 0.006);
        group.Children.Add(Model(lattice, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.28));

        var backplane = new MeshFactory.Builder();
        MeshFactory.Box(backplane, bay.At(0, 0, -0.14), 0.84, 0.50, 0.045, 0.012);
        group.Children.Add(Model(backplane, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.14));

        // Two bundle members, deliberately separate so a single failing member is visible.
        var members = new MeshFactory.Builder();
        MeshFactory.Cylinder(members, bay.At(-0.16, 0.26, -0.10), bay.Out, 0.020, 0.28, 10);
        MeshFactory.Cylinder(members, bay.At(0.16, 0.26, -0.10), bay.Out, 0.020, 0.28, 10);
        group.Children.Add(new GeometryModel3D(members.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
    }

    /// <summary>Routing and delivery: a faceted prism with directional vanes.</summary>
    private static void BuildRoutingPrism(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var prism = new MeshFactory.Builder();
        MeshFactory.Cylinder(prism, bay.At(0, -0.24, 0), bay.Up, 0.26, 0.48, 6, topRadius: 0.17);
        group.Children.Add(Model(prism, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.34, 40));

        var vanes = new MeshFactory.Builder();
        for (int i = 0; i < 3; i++)
        {
            double x = -0.30 + i * 0.30;
            MeshFactory.Box(vanes, bay.At(x, 0.02, 0.10), 0.055, 0.34, 0.18, 0.014);
        }
        MeshFactory.Box(vanes, bay.At(0, -0.27, 0), 0.78, 0.045, 0.34, 0.012);
        group.Children.Add(Model(vanes, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.18));

        var paths = new MeshFactory.Builder();
        for (int i = 0; i < 3; i++)
            MeshFactory.Box(paths, bay.At(-0.30 + i * 0.30, 0.21, 0.10), 0.040, 0.020, 0.19, 0);
        group.Children.Add(new GeometryModel3D(paths.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
    }

    /// <summary>Name resolution: an indexed drum whose fins are the zones it can answer for.</summary>
    private static AxisAngleRotation3D BuildIndexDrum(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var spin = new AxisAngleRotation3D(bay.Tangent, 0);
        var spun = new Model3DGroup
        {
            Transform = new RotateTransform3D(spin, bay.At(0, 0, 0))
        };

        var drum = new MeshFactory.Builder();
        MeshFactory.Cylinder(drum, bay.At(-0.26, 0, 0), bay.Tangent, 0.215, 0.52, 22);
        spun.Children.Add(Model(drum, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.30, 40));

        var fins = new MeshFactory.Builder();
        for (int i = 0; i < 14; i++)
        {
            double a = i * (360.0 / 14) * Math.PI / 180;
            var radial = bay.Up * Math.Cos(a) + bay.Out * Math.Sin(a);
            var at = bay.At(0, 0, 0) + radial * 0.245;
            MeshFactory.Cylinder(fins, at - bay.Tangent * 0.24, bay.Tangent, 0.016, 0.48, 6);
        }
        spun.Children.Add(Model(fins, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.20));

        var index = new MeshFactory.Builder();
        MeshFactory.Torus(index, bay.At(0, 0, 0), bay.Tangent, 0.235, 0.017, 26, 7);
        spun.Children.Add(new GeometryModel3D(index.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));

        group.Children.Add(spun);

        // Static bearing housings, so the drum reads as mounted rather than floating.
        var mounts = new MeshFactory.Builder();
        MeshFactory.Cylinder(mounts, bay.At(-0.32, 0, 0), bay.Tangent, 0.10, 0.07, 14);
        MeshFactory.Cylinder(mounts, bay.At(0.25, 0, 0), bay.Tangent, 0.10, 0.07, 14);
        MeshFactory.Box(mounts, bay.At(0, -0.30, 0), 0.78, 0.05, 0.30, 0.012);
        group.Children.Add(Model(mounts, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Structure), 0.14));

        return spin;
    }

    /// <summary>Identity and trust: a sealed cylinder behind a keyed collar that unlocks under inspection.</summary>
    private static AxisAngleRotation3D BuildTrustVault(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var housing = new MeshFactory.Builder();
        MeshFactory.Cylinder(housing, bay.At(0, 0, -0.22), bay.Out, 0.245, 0.42, 20, capStart: true, capEnd: false);
        group.Children.Add(Model(housing, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.30, 40));

        // The collar rotates when the vault is inspected.
        var collarRotation = new AxisAngleRotation3D(bay.Out, 0);
        var collarGroup = new Model3DGroup
        {
            Transform = new RotateTransform3D(collarRotation, bay.At(0, 0, 0.20))
        };
        var collar = new MeshFactory.Builder();
        MeshFactory.Tube(collar, bay.At(0, 0, 0.18), bay.Out, 0.185, 0.275, 0.06, 24);
        for (int i = 0; i < 4; i++)
        {
            double a = i * 90 * Math.PI / 180;
            var radial = bay.Up * Math.Cos(a) + bay.Tangent * Math.Sin(a);
            MeshFactory.Box(collar, bay.At(0, 0, 0.21) + radial * 0.275, 0.075, 0.075, 0.075, 0.018);
        }
        collarGroup.Children.Add(Model(collar, surfaces.Add(Palette3D.Connector, SurfaceRole.Interface), 0.42, 48));
        group.Children.Add(collarGroup);

        var core = new MeshFactory.Builder();
        MeshFactory.Cylinder(core, bay.At(0, 0, -0.10), bay.Out, 0.115, 0.26, 16);
        group.Children.Add(new GeometryModel3D(core.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));

        var cradle = new MeshFactory.Builder();
        MeshFactory.Box(cradle, bay.At(0, -0.29, -0.02), 0.62, 0.05, 0.40, 0.012);
        MeshFactory.Box(cradle, bay.At(-0.30, 0, -0.02), 0.05, 0.40, 0.36, 0.012);
        MeshFactory.Box(cradle, bay.At(0.30, 0, -0.02), 0.05, 0.40, 0.36, 0.012);
        group.Children.Add(Model(cradle, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Structure), 0.14));

        return collarRotation;
    }

    /// <summary>Application workload: stacked service plates on a spacer post.</summary>
    private static void BuildServiceStack(Model3DGroup group, Surfaces surfaces, Bay bay, SceneModule module)
    {
        bool round = module.Sweep >= 359;
        double width = round ? 0.90 : 0.74;
        var plates = new MeshFactory.Builder();
        int count = round ? 4 : 6;
        for (int i = 0; i < count; i++)
        {
            double y = -0.24 + i * (0.48 / (count - 1));
            MeshFactory.Box(plates, bay.At(0, y, 0), width, 0.040, round ? 0.90 : 0.34, 0.010);
        }
        group.Children.Add(Model(plates, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.26));

        var posts = new MeshFactory.Builder();
        double px = width / 2 - 0.06;
        double pz = (round ? 0.90 : 0.34) / 2 - 0.06;
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            MeshFactory.Cylinder(posts, bay.At(sx * px, -0.28, sz * pz), bay.Up, 0.022, 0.56, 8);
        group.Children.Add(Model(posts, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.18));

        var live = new MeshFactory.Builder();
        for (int i = 0; i < count; i++)
        {
            double y = -0.24 + i * (0.48 / (count - 1));
            MeshFactory.Box(live, bay.At(width / 2 - 0.10, y, round ? 0.40 : 0.15), 0.10, 0.014, 0.014, 0);
        }
        group.Children.Add(new GeometryModel3D(live.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
    }

    /// <summary>Observation and control: a sensor head on a gimbal that sweeps the rings below.</summary>
    private static AxisAngleRotation3D BuildSensorDome(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var pedestal = new MeshFactory.Builder();
        MeshFactory.Cylinder(pedestal, bay.At(0, -0.28, 0), bay.Up, 0.20, 0.16, 16, topRadius: 0.15);
        MeshFactory.Box(pedestal, bay.At(0, -0.31, 0), 0.70, 0.05, 0.32, 0.012);
        group.Children.Add(Model(pedestal, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.16));

        var rotation = new AxisAngleRotation3D(bay.Up, 0);
        var head = new Model3DGroup { Transform = new RotateTransform3D(rotation, bay.At(0, -0.04, 0)) };

        var dome = new MeshFactory.Builder();
        MeshFactory.Dome(dome, bay.At(0, -0.06, 0), 0.235, 22, 9);
        MeshFactory.Cylinder(dome, bay.At(0, -0.12, 0), bay.Up, 0.235, 0.06, 22);
        head.Children.Add(Model(dome, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.40, 52));

        var gimbal = new MeshFactory.Builder();
        MeshFactory.Torus(gimbal, bay.At(0, -0.06, 0), bay.Tangent, 0.265, 0.020, 26, 7);
        head.Children.Add(Model(gimbal, surfaces.Add(Palette3D.Connector, SurfaceRole.Interface), 0.40, 46));

        var lens = new MeshFactory.Builder();
        MeshFactory.Cylinder(lens, bay.At(0, -0.04, 0.20), bay.Out, 0.055, 0.05, 14);
        head.Children.Add(new GeometryModel3D(lens.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));

        group.Children.Add(head);

        var antennae = new MeshFactory.Builder();
        for (int i = 0; i < 3; i++)
            MeshFactory.Cylinder(antennae, bay.At(-0.28 + i * 0.28, 0.16, -0.06), bay.Up, 0.010, 0.16, 6);
        group.Children.Add(Model(antennae, surfaces.Add(Palette3D.Connector, SurfaceRole.Interface), 0.34, 40));

        return rotation;
    }

    /// <summary>Security inspection: concentric aperture rings that close around what is being examined.</summary>
    private static AxisAngleRotation3D BuildInspectionAperture(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var frame = new MeshFactory.Builder();
        MeshFactory.Tube(frame, bay.At(0, 0, -0.14), bay.Out, 0.245, 0.315, 0.06, 26);
        MeshFactory.Box(frame, bay.At(0, -0.30, -0.02), 0.70, 0.05, 0.34, 0.012);
        group.Children.Add(Model(frame, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.18));

        var rotation = new AxisAngleRotation3D(bay.Out, 0);
        var iris = new Model3DGroup { Transform = new RotateTransform3D(rotation, bay.At(0, 0, 0)) };

        var blades = new MeshFactory.Builder();
        for (int i = 0; i < 6; i++)
        {
            double a = i * 60 * Math.PI / 180;
            var radial = bay.Up * Math.Cos(a) + bay.Tangent * Math.Sin(a);
            var tangential = bay.Up * -Math.Sin(a) + bay.Tangent * Math.Cos(a);
            var at = bay.At(0, 0, 0.02) + radial * 0.155 + tangential * 0.05;
            AddOrientedPlate(blades, at, tangential, radial, bay.Out, 0.19, 0.055, 0.022);
        }
        iris.Children.Add(Model(blades, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.36, 44));
        group.Children.Add(iris);

        var rings = new MeshFactory.Builder();
        MeshFactory.Torus(rings, bay.At(0, 0, 0.10), bay.Out, 0.215, 0.014, 26, 7);
        MeshFactory.Torus(rings, bay.At(0, 0, 0.16), bay.Out, 0.155, 0.012, 22, 7);
        group.Children.Add(Model(rings, surfaces.Add(Palette3D.Connector, SurfaceRole.Interface), 0.40, 46));

        var core = new MeshFactory.Builder();
        MeshFactory.Cylinder(core, bay.At(0, 0, -0.06), bay.Out, 0.085, 0.14, 14);
        group.Children.Add(new GeometryModel3D(core.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));

        return rotation;
    }

    /// <summary>Gated automation: a cylinder and piston that only travels once every gate has cleared.</summary>
    private static TranslateTransform3D BuildActuator(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var barrel = new MeshFactory.Builder();
        MeshFactory.Cylinder(barrel, bay.At(0, 0, -0.26), bay.Out, 0.175, 0.34, 20);
        MeshFactory.Tube(barrel, bay.At(0, 0, 0.06), bay.Out, 0.175, 0.215, 0.045, 20);
        group.Children.Add(Model(barrel, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.32, 42));

        var rails = new MeshFactory.Builder();
        MeshFactory.Cylinder(rails, bay.At(-0.26, 0, -0.24), bay.Out, 0.020, 0.50, 10);
        MeshFactory.Cylinder(rails, bay.At(0.26, 0, -0.24), bay.Out, 0.020, 0.50, 10);
        MeshFactory.Box(rails, bay.At(0, -0.27, -0.04), 0.70, 0.05, 0.40, 0.012);
        MeshFactory.Box(rails, bay.At(0, 0.25, -0.16), 0.44, 0.10, 0.18, 0.02);
        group.Children.Add(Model(rails, surfaces.Add(Palette3D.MechanismDark, SurfaceRole.Mechanism), 0.16));

        var extend = new TranslateTransform3D();
        var piston = new Model3DGroup { Transform = extend };
        var rod = new MeshFactory.Builder();
        MeshFactory.Cylinder(rod, bay.At(0, 0, 0.06), bay.Out, 0.058, 0.20, 14);
        MeshFactory.Cylinder(rod, bay.At(0, 0, 0.24), bay.Out, 0.105, 0.05, 16);
        piston.Children.Add(Model(rod, surfaces.Add(Palette3D.Connector, SurfaceRole.Interface), 0.46, 52));
        group.Children.Add(piston);

        var gates = new MeshFactory.Builder();
        for (int i = 0; i < 4; i++)
            MeshFactory.Box(gates, bay.At(-0.15 + i * 0.10, 0.25, -0.06), 0.055, 0.055, 0.014, 0);
        group.Children.Add(new GeometryModel3D(gates.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));

        return extend;
    }

    /// <summary>Evidence: a sealed block holding a stack of ledger plates.</summary>
    private static void BuildArchiveVault(Model3DGroup group, Surfaces surfaces, Bay bay)
    {
        var block = new MeshFactory.Builder();
        MeshFactory.Box(block, bay.At(0, -0.04, 0), 0.62, 0.34, 0.62, 0.06);
        group.Children.Add(Model(block, surfaces.Add(Palette3D.Mechanism, SurfaceRole.Mechanism), 0.30, 40));

        var ledger = new MeshFactory.Builder();
        for (int i = 0; i < 6; i++)
            MeshFactory.Box(ledger, bay.At(0, 0.14 + i * 0.030, 0), 0.46 - i * 0.026, 0.018, 0.46 - i * 0.026, 0.005);
        group.Children.Add(Model(ledger, surfaces.Add(Palette3D.Connector, SurfaceRole.Interface), 0.40, 46));

        var seal = new MeshFactory.Builder();
        MeshFactory.Torus(seal, bay.At(0, 0.135, 0), bay.Up, 0.27, 0.014, 28, 7);
        group.Children.Add(new GeometryModel3D(seal.ToMesh(),
            Palette3D.Emissive(surfaces.Add(Palette3D.State(Domain.HealthState.Healthy), SurfaceRole.StateAccent))));
    }

    /// <summary>A thin plate oriented by an arbitrary basis, used for iris blades.</summary>
    private static void AddOrientedPlate(MeshFactory.Builder b, Point3D centre, Vector3D along, Vector3D across,
        Vector3D normal, double length, double width, double thickness)
    {
        along.Normalize(); across.Normalize(); normal.Normalize();
        var a = along * (length / 2);
        var c = across * (width / 2);
        var n = normal * (thickness / 2);

        Point3D V(int sa, int sc, int sn) => centre + a * sa + c * sc + n * sn;
        b.Quad(V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1));
        b.Quad(V(1, -1, -1), V(-1, -1, -1), V(-1, 1, -1), V(1, 1, -1));
        b.Quad(V(1, -1, 1), V(1, -1, -1), V(1, 1, -1), V(1, 1, 1));
        b.Quad(V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1));
        b.Quad(V(-1, 1, 1), V(1, 1, 1), V(1, 1, -1), V(-1, 1, -1));
        b.Quad(V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1));
    }
}
