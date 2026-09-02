using System.Windows.Media;
using System.Windows.Media.Media3D;
using BluePeak.Domain;

namespace BluePeak.App.Simulator3D;

/// <summary>
/// Surface treatment for the machine. Restraint is deliberate: the chassis is almost
/// monochrome so that the only saturated colour in the scene is state.
/// </summary>
public static class Palette3D
{
    public static readonly Color ShellFace = Color.FromRgb(0x2F, 0x38, 0x44);
    public static readonly Color ShellEdge = Color.FromRgb(0x47, 0x53, 0x63);
    public static readonly Color Spine = Color.FromRgb(0x1B, 0x21, 0x2A);
    public static readonly Color SpineRib = Color.FromRgb(0x2E, 0x37, 0x43);
    public static readonly Color Deck = Color.FromRgb(0x28, 0x30, 0x3B);
    public static readonly Color Mechanism = Color.FromRgb(0x57, 0x64, 0x76);
    public static readonly Color MechanismDark = Color.FromRgb(0x33, 0x3D, 0x4A);
    public static readonly Color Connector = Color.FromRgb(0x8E, 0x95, 0xA1);
    public static readonly Color Plinth = Color.FromRgb(0x24, 0x2B, 0x35);

    /// <summary>Colour a dimmed part settles toward. Dimming by value avoids transparency sorting.</summary>
    public static readonly Color Recede = Color.FromRgb(0x12, 0x16, 0x1C);

    public static Color State(HealthState state) => state switch
    {
        HealthState.Healthy => Color.FromRgb(0x3F, 0xB9, 0x8A),
        HealthState.Degraded => Color.FromRgb(0xE0, 0xA3, 0x3E),
        HealthState.Critical => Color.FromRgb(0xE5, 0x54, 0x4B),
        HealthState.Offline => Color.FromRgb(0xE5, 0x54, 0x4B),
        HealthState.Maintenance => Color.FromRgb(0x8A, 0x7B, 0xD1),
        _ => Color.FromRgb(0x6B, 0x76, 0x86)
    };

    public static readonly Color Accent = Color.FromRgb(0x4C, 0x9D, 0xF0);

    public static Color Lerp(Color a, Color b, double k)
    {
        k = Math.Clamp(k, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * k),
            (byte)(a.G + (b.G - a.G) * k),
            (byte)(a.B + (b.B - a.B) * k));
    }

    public static Color Scale(Color c, double k) => Color.FromRgb(
        (byte)Math.Clamp(c.R * k, 0, 255),
        (byte)Math.Clamp(c.G * k, 0, 255),
        (byte)Math.Clamp(c.B * k, 0, 255));

    /// <summary>Matte structural surface with a restrained highlight, so edges read without gloss.</summary>
    public static Material Structural(SolidColorBrush brush, double specular = 0.16, double power = 28)
    {
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(brush));
        var spec = new SolidColorBrush(Color.FromArgb(255,
            (byte)(255 * specular), (byte)(255 * specular), (byte)(255 * specular)));
        spec.Freeze();
        group.Children.Add(new SpecularMaterial(spec, power));
        return group;
    }

    public static Material Emissive(SolidColorBrush brush)
    {
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(brush));
        group.Children.Add(new EmissiveMaterial(brush));
        return group;
    }
}
