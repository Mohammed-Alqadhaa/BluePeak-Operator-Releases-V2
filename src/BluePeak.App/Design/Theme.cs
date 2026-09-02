using System.Windows;
using System.Windows.Media;
using BluePeak.Domain;

namespace BluePeak.App.Design;

/// <summary>
/// Code-side access to the design tokens. Anything drawn with a DrawingContext rather than
/// XAML resolves its colours here, so custom visuals and templated controls stay identical.
/// </summary>
public static class Theme
{
    public static Color Color(string key) =>
        Application.Current?.TryFindResource(key) is Color c ? c : Colors.Magenta;

    public static SolidColorBrush Brush(string key) =>
        Application.Current?.TryFindResource(key) is SolidColorBrush b ? b : Brushes.Magenta;

    // Cached hot-path brushes for custom-drawn visuals.
    private static SolidColorBrush? _healthy, _degraded, _critical, _unknown, _maintenance, _accent;
    private static Pen? _hairline;

    public static SolidColorBrush Healthy => _healthy ??= Frozen("#FF3FB98A");
    public static SolidColorBrush Degraded => _degraded ??= Frozen("#FFE0A33E");
    public static SolidColorBrush Critical => _critical ??= Frozen("#FFE5544B");
    public static SolidColorBrush Unknown => _unknown ??= Frozen("#FF6B7686");
    public static SolidColorBrush Maintenance => _maintenance ??= Frozen("#FF8A7BD1");
    public static SolidColorBrush Accent => _accent ??= Frozen("#FF4C9DF0");
    public static Pen Hairline => _hairline ??= FrozenPen("#FF222933", 1);

    public static SolidColorBrush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        b.Freeze();
        return b;
    }

    public static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static Pen FrozenPen(string hex, double thickness)
    {
        var p = new Pen(Frozen(hex), thickness);
        p.Freeze();
        return p;
    }

    public static Pen FrozenPen(Brush brush, double thickness)
    {
        var p = new Pen(brush, thickness);
        p.Freeze();
        return p;
    }

    public static SolidColorBrush ForHealth(HealthState state) => state switch
    {
        HealthState.Healthy => Healthy,
        HealthState.Degraded => Degraded,
        HealthState.Critical => Critical,
        HealthState.Offline => Critical,
        HealthState.Maintenance => Maintenance,
        _ => Unknown
    };

    public static SolidColorBrush ForSeverity(Severity severity) => severity switch
    {
        Severity.Critical => Critical,
        Severity.High => Critical,
        Severity.Medium => Degraded,
        Severity.Low => Accent,
        _ => Unknown
    };

    public static Color WithAlpha(Color c, double alpha) =>
        System.Windows.Media.Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);
}
