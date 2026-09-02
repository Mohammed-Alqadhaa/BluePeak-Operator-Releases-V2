using System.Windows.Media;
using System.Windows.Media.Media3D;
using BluePeak.Domain;
using BluePeak.Simulation;

namespace BluePeak.App.Simulator3D;

/// <summary>
/// A dependency drawn between two points in the machine. Links are pooled and reshaped with
/// transforms rather than rebuilt, so nothing allocates geometry during playback.
/// </summary>
public sealed class LinkVisual
{
    public required Model3DGroup Root { get; init; }
    public required ScaleTransform3D Scale { get; init; }
    public required AxisAngleRotation3D Rotation { get; init; }
    public required TranslateTransform3D Translation { get; init; }
    public required SolidColorBrush Brush { get; init; }

    public required Model3DGroup PulseRoot { get; init; }
    public required TranslateTransform3D PulseTranslation { get; init; }
    public required ScaleTransform3D PulseScale { get; init; }
    public required SolidColorBrush PulseBrush { get; init; }

    public bool Visible { get; private set; } = true;

    public void Hide()
    {
        if (!Visible) return;
        Visible = false;
        Scale.ScaleX = Scale.ScaleY = Scale.ScaleZ = 0;
        PulseScale.ScaleX = PulseScale.ScaleY = PulseScale.ScaleZ = 0;
    }

    /// <summary>Aligns the unit cylinder to span from a to b at the requested radius.</summary>
    public void Span(Point3D a, Point3D b, double radius, Color colour, double intensity, double flowPhase, bool showPulse)
    {
        Visible = true;
        var delta = b - a;
        double length = delta.Length;
        if (length < 1e-4)
        {
            Hide();
            return;
        }

        var direction = delta / length;
        var up = new Vector3D(0, 1, 0);
        var axis = Vector3D.CrossProduct(up, direction);
        double angle;
        if (axis.LengthSquared < 1e-9)
        {
            axis = new Vector3D(1, 0, 0);
            angle = direction.Y > 0 ? 0 : 180;
        }
        else
        {
            axis.Normalize();
            angle = Vector3D.AngleBetween(up, direction);
        }

        Scale.ScaleX = Scale.ScaleZ = radius;
        Scale.ScaleY = length;
        Rotation.Axis = axis;
        Rotation.Angle = angle;
        Translation.OffsetX = a.X;
        Translation.OffsetY = a.Y;
        Translation.OffsetZ = a.Z;

        Brush.Color = Color.FromRgb(
            (byte)Math.Clamp(colour.R * intensity, 0, 255),
            (byte)Math.Clamp(colour.G * intensity, 0, 255),
            (byte)Math.Clamp(colour.B * intensity, 0, 255));

        if (showPulse)
        {
            double t = flowPhase - Math.Floor(flowPhase);
            var at = a + direction * (length * t);
            PulseScale.ScaleX = PulseScale.ScaleY = PulseScale.ScaleZ = radius * 2.2;
            PulseTranslation.OffsetX = at.X;
            PulseTranslation.OffsetY = at.Y;
            PulseTranslation.OffsetZ = at.Z;
            double fade = Math.Sin(Math.PI * t);
            PulseBrush.Color = Color.FromRgb(
                (byte)Math.Clamp(colour.R * (0.55 + 0.45 * fade) * intensity, 0, 255),
                (byte)Math.Clamp(colour.G * (0.55 + 0.45 * fade) * intensity, 0, 255),
                (byte)Math.Clamp(colour.B * (0.55 + 0.45 * fade) * intensity, 0, 255));
        }
        else
        {
            PulseScale.ScaleX = PulseScale.ScaleY = PulseScale.ScaleZ = 0;
        }
    }
}

/// <summary>
/// The assembled BluePeak Operations Core: a central spine carrying four stacked decks of
/// chassis wedges, plus the pooled visuals used to draw dependencies between them.
/// </summary>
public sealed class CoreScene
{
    public Model3DGroup Root { get; } = new();
    public IReadOnlyDictionary<string, ModuleVisual> Modules { get; }
    public IReadOnlyList<LinkVisual> Links { get; }

    private const int LinkPoolSize = 12;

    public CoreScene()
    {
        Root.Children.Add(BuildLighting());
        Root.Children.Add(BuildGround());
        Root.Children.Add(BuildSpine());
        Root.Children.Add(BuildDecks());

        var modules = new Dictionary<string, ModuleVisual>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in OperationsCore.Modules)
        {
            var visual = ModuleBuilder.Build(definition);
            modules[definition.Id] = visual;
            Root.Children.Add(visual.Root);
        }
        Modules = modules;

        var cylinder = MeshFactory.UnitCylinder(10);
        var sphere = MeshFactory.UnitSphere(10, 6);
        var links = new List<LinkVisual>(LinkPoolSize);
        for (int i = 0; i < LinkPoolSize; i++)
        {
            var link = BuildLink(cylinder, sphere);
            links.Add(link);
            Root.Children.Add(link.Root);
            Root.Children.Add(link.PulseRoot);
            link.Hide();
        }
        Links = links;
    }

    /// <summary>Anchor on the spine at the height of the given module's deck.</summary>
    public static Point3D SpineAnchor(SceneModule module)
    {
        double radius = module.Sweep >= 359 ? 0.0 : 0.44;
        var direction = MeshFactory.Radial(module.Azimuth);
        return new Point3D(direction.X * radius, module.Height, direction.Z * radius);
    }

    private static Model3DGroup BuildLighting()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(0x2C, 0x33, 0x3D)));

        // Key from the upper front left, which is where an operator expects a machine to be lit from.
        group.Children.Add(new DirectionalLight(Color.FromRgb(0xDA, 0xE4, 0xF2), new Vector3D(-0.48, -0.72, -0.50)));
        // Cool fill from the opposite side keeps the far shoulder from going black.
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x46, 0x55, 0x6A), new Vector3D(0.74, -0.18, 0.46)));
        // Low rim to separate the silhouette from the background.
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x35, 0x4A, 0x64), new Vector3D(0.12, 0.62, -0.88)));
        return group;
    }

    /// <summary>
    /// A shallow platform under the machine. Without it the assembly reads as floating, and an
    /// operator loses the sense of scale that makes an exploded view legible.
    /// </summary>
    private static Model3DGroup BuildGround()
    {
        var group = new Model3DGroup();
        double y = OperationsCore.RingPlinth - 0.30;

        var pad = new MeshFactory.Builder();
        MeshFactory.Prism(pad, new Point3D(0, y - 0.08, 0), 4.55, 0.08, 12, 15, 4.75);
        var padBrush = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x1F));
        group.Children.Add(new GeometryModel3D(pad.ToMesh(), Palette3D.Structural(padBrush, 0.06)));

        // Concentric datum rings give the eye a scale reference as parts travel outward.
        var rings = new MeshFactory.Builder();
        foreach (var (radius, thickness) in new[] { (2.95, 0.010), (3.70, 0.007), (4.42, 0.007) })
            MeshFactory.Torus(rings, new Point3D(0, y + 0.005, 0), new Vector3D(0, 1, 0), radius, thickness, 56, 6);
        var ringBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x2E, 0x3A));
        group.Children.Add(new GeometryModel3D(rings.ToMesh(), Palette3D.Structural(ringBrush, 0.20)));

        // Radial datum marks at each module azimuth, so extraction directions are readable.
        var marks = new MeshFactory.Builder();
        foreach (var module in OperationsCore.Modules.Where(m => m.Sweep < 359))
        {
            var direction = MeshFactory.Radial(module.Azimuth);
            for (double r = 2.35; r < 4.3; r += 0.42)
            {
                var at = new Point3D(direction.X * r, y + 0.006, direction.Z * r);
                MeshFactory.Box(marks, at, 0.16, 0.008, 0.03);
            }
        }
        var markBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x39, 0x47));
        group.Children.Add(new GeometryModel3D(marks.ToMesh(), Palette3D.Structural(markBrush, 0.18)));

        return group;
    }

    private static Model3DGroup BuildSpine()
    {
        var group = new Model3DGroup();

        var core = new MeshFactory.Builder();
        MeshFactory.Prism(core, new Point3D(0, OperationsCore.RingPlinth - 0.10, 0), 0.40,
            OperationsCore.RingCrown - OperationsCore.RingPlinth + 0.42, 6, 0);
        var coreBrush = new SolidColorBrush(Palette3D.Spine);
        group.Children.Add(new GeometryModel3D(core.ToMesh(), Palette3D.Structural(coreBrush, 0.14)));

        var ribs = new MeshFactory.Builder();
        for (int i = 0; i < 6; i++)
        {
            double a = i * 60 + 30;
            var direction = MeshFactory.Radial(a);
            double y0 = OperationsCore.RingPlinth - 0.06;
            double height = OperationsCore.RingCrown - OperationsCore.RingPlinth + 0.34;
            var at = new Point3D(direction.X * 0.375, y0 + height / 2, direction.Z * 0.375);
            AddRib(ribs, at, direction, height);
        }
        var ribBrush = new SolidColorBrush(Palette3D.SpineRib);
        group.Children.Add(new GeometryModel3D(ribs.ToMesh(), Palette3D.Structural(ribBrush, 0.24)));

        // Collars at every deck: the seats the wedges lock against.
        var collars = new MeshFactory.Builder();
        foreach (double y in new[]
        {
            OperationsCore.RingAccess, OperationsCore.RingCore,
            OperationsCore.RingControl, OperationsCore.RingCrown - 0.36
        })
        {
            MeshFactory.Tube(collars, new Point3D(0, y - 0.55, 0), new Vector3D(0, 1, 0), 0.40, 0.50, 0.07, 24);
            MeshFactory.Tube(collars, new Point3D(0, y + 0.48, 0), new Vector3D(0, 1, 0), 0.40, 0.50, 0.07, 24);
        }
        var collarBrush = new SolidColorBrush(Palette3D.ShellEdge);
        group.Children.Add(new GeometryModel3D(collars.ToMesh(), Palette3D.Structural(collarBrush, 0.30)));

        // Conduit running the length of the spine: the bus every module docks into.
        var conduit = new MeshFactory.Builder();
        MeshFactory.Cylinder(conduit, new Point3D(0, OperationsCore.RingPlinth, 0), new Vector3D(0, 1, 0), 0.055,
            OperationsCore.RingCrown - OperationsCore.RingPlinth + 0.10, 12);
        var conduitBrush = new SolidColorBrush(Palette3D.Scale(Palette3D.Accent, 0.42));
        group.Children.Add(new GeometryModel3D(conduit.ToMesh(), Palette3D.Emissive(conduitBrush)));

        return group;
    }

    private static void AddRib(MeshFactory.Builder builder, Point3D centre, Vector3D outward, double height)
    {
        outward.Normalize();
        var tangent = Vector3D.CrossProduct(new Vector3D(0, 1, 0), outward);
        tangent.Normalize();
        var up = new Vector3D(0, 1, 0);

        Point3D V(double x, double y, double z) => centre + tangent * x + up * y + outward * z;
        double hw = 0.075, hh = height / 2, hd = 0.055;

        builder.Quad(V(-hw, -hh, hd), V(hw, -hh, hd), V(hw, hh, hd), V(-hw, hh, hd));
        builder.Quad(V(hw, -hh, -hd), V(-hw, -hh, -hd), V(-hw, hh, -hd), V(hw, hh, -hd));
        builder.Quad(V(hw, -hh, hd), V(hw, -hh, -hd), V(hw, hh, -hd), V(hw, hh, hd));
        builder.Quad(V(-hw, -hh, -hd), V(-hw, -hh, hd), V(-hw, hh, hd), V(-hw, hh, -hd));
        builder.Quad(V(-hw, hh, hd), V(hw, hh, hd), V(hw, hh, -hd), V(-hw, hh, -hd));
        builder.Quad(V(-hw, -hh, -hd), V(hw, -hh, -hd), V(hw, -hh, hd), V(-hw, -hh, hd));
    }

    private static Model3DGroup BuildDecks()
    {
        var group = new Model3DGroup();
        var decks = new MeshFactory.Builder();

        foreach (double y in new[] { OperationsCore.RingAccess, OperationsCore.RingCore, OperationsCore.RingControl })
        {
            // Deck plates stop inside the wedge silhouette so the sealed machine reads as one
            // enclosed body rather than a stack of discs.
            double plate = y - 0.50;
            MeshFactory.Tube(decks, new Point3D(0, plate, 0), new Vector3D(0, 1, 0), 0.48, 1.54, 0.05, 40);
            // Radial spokes tie each deck back to the spine so the rings read as mounted.
            for (int i = 0; i < 6; i++)
            {
                double a = i * 60 + 30;
                var direction = MeshFactory.Radial(a);
                var at = new Point3D(direction.X * 1.05, plate - 0.035, direction.Z * 1.05);
                AddRib(decks, at, direction, 0.06);
            }
        }

        var brush = new SolidColorBrush(Palette3D.Deck);
        group.Children.Add(new GeometryModel3D(decks.ToMesh(), Palette3D.Structural(brush, 0.16)));
        return group;
    }

    private static LinkVisual BuildLink(MeshGeometry3D cylinder, MeshGeometry3D sphere)
    {
        var brush = new SolidColorBrush(Colors.Black);
        var scale = new ScaleTransform3D(0, 0, 0);
        var rotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
        var translation = new TranslateTransform3D();

        var transform = new Transform3DGroup();
        transform.Children.Add(scale);
        transform.Children.Add(new RotateTransform3D(rotation));
        transform.Children.Add(translation);

        var root = new Model3DGroup { Transform = transform };
        root.Children.Add(new GeometryModel3D(cylinder, Palette3D.Emissive(brush)));

        var pulseBrush = new SolidColorBrush(Colors.Black);
        var pulseScale = new ScaleTransform3D(0, 0, 0);
        var pulseTranslation = new TranslateTransform3D();
        var pulseTransform = new Transform3DGroup();
        pulseTransform.Children.Add(pulseScale);
        pulseTransform.Children.Add(pulseTranslation);

        var pulseRoot = new Model3DGroup { Transform = pulseTransform };
        pulseRoot.Children.Add(new GeometryModel3D(sphere, Palette3D.Emissive(pulseBrush)));

        return new LinkVisual
        {
            Root = root,
            Scale = scale,
            Rotation = rotation,
            Translation = translation,
            Brush = brush,
            PulseRoot = pulseRoot,
            PulseTranslation = pulseTranslation,
            PulseScale = pulseScale,
            PulseBrush = pulseBrush
        };
    }
}
