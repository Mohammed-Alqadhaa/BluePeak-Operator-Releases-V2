using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using BluePeak.Domain;

namespace BluePeak.App.Design;

public sealed class HealthBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        Theme.ForHealth(value is HealthState h ? h : HealthState.Unknown);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class SeverityBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        Theme.ForSeverity(value is Severity s ? s : Severity.Info);
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        bool b = value is bool flag && flag;
        if (p as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        bool present = value is not null && value is not string s1 || value is string s && s.Length > 0;
        if (p as string == "invert") present = !present;
        return present ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        int n = value switch
        {
            int i => i,
            System.Collections.ICollection col => col.Count,
            _ => 0
        };
        bool visible = n > 0;
        if (p as string == "invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Relative age, e.g. "12m" or "3h 04m". Tables need this to stay narrow.</summary>
public sealed class AgoConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is not DateTime at) return "—";
        var span = Services.EstateService.Current.Now - at;
        return Format(span);
    }

    /// <summary>
    /// Magnitude only, never a direction. Callers know whether the span is elapsed, remaining
    /// or overdue and say so themselves — a built-in prefix collides with theirs and produces
    /// nonsense like "-in 4m" on a breached target.
    /// </summary>
    public static string Format(TimeSpan span)
    {
        span = span.Duration();
        return span.TotalMinutes < 1 ? "<1m"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m"
            : $"{(int)span.TotalDays}d {span.Hours:00}h";
    }

    /// <summary>Remaining time against a target, or how far past it the subject already is.</summary>
    public static string FormatTarget(TimeSpan remaining) =>
        remaining.Ticks < 0 ? Format(remaining) + " over" : Format(remaining) + " left";

    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class ClockConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is DateTime at ? at.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "—";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class EnumLabelConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is null) return "—";
        string raw = value.ToString() ?? "";
        var sb = new System.Text.StringBuilder(raw.Length + 4);
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
            sb.Append(raw[i]);
        }
        return sb.ToString();
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class OpacityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        bool on = value is bool b && b;
        if (p as string == "invert") on = !on;
        return on ? 1.0 : 0.42;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Multiplies a normalised 0..1 value by the supplied pixel width for inline meters.</summary>
public sealed class FractionWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type t, object? p, CultureInfo c)
    {
        if (values.Length < 2) return 0d;
        double fraction = values[0] is double d ? d : 0;
        double width = values[1] is double w ? w : 0;
        if (double.IsNaN(width) || double.IsInfinity(width)) return 0d;
        return Math.Max(0, Math.Min(1, fraction)) * width;
    }
    public object[] ConvertBack(object? v, Type[] t, object? p, CultureInfo c) => Array.Empty<object>();
}

public sealed class GateBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        GateState.Passed => Theme.Healthy,
        GateState.Running => Theme.Accent,
        GateState.Failed => Theme.Critical,
        GateState.Blocked => Theme.Critical,
        GateState.WaitingApproval => Theme.Degraded,
        GateState.Skipped => Theme.Unknown,
        _ => Theme.Unknown
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class EvidenceResultBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        EvidenceResult.Pass => Theme.Healthy,
        EvidenceResult.Fail => Theme.Critical,
        EvidenceResult.Inconclusive => Theme.Degraded,
        _ => Theme.Unknown
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class AuthorityBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value switch
    {
        EvidenceAuthority.ProjectAuthoritative => Theme.Healthy,
        EvidenceAuthority.PlatformAttested => Theme.Accent,
        _ => Theme.Degraded
    };
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

public sealed class WashBrushConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var brush = value switch
        {
            HealthState h => Theme.ForHealth(h),
            Severity s => Theme.ForSeverity(s),
            _ => Theme.Unknown
        };
        double alpha = p is string ps && double.TryParse(ps, NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ? a : 0.16;
        return Theme.Frozen(Theme.WithAlpha(brush.Color, alpha));
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Resolves an icon resource key to its geometry so the nav rail can data-bind icons.</summary>
public sealed class IconLookupConverter : IValueConverter
{
    public object? Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is not string key) return null;
        return Application.Current?.TryFindResource(key) as Geometry;
    }
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => Binding.DoNothing;
}
