using System.Windows;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.Domain;

namespace BluePeak.App.Controls;

/// <summary>
/// Inline trend renderer. Drawn directly rather than charted: at row density a sparkline is
/// a shape, not a chart, and must cost nothing to draw a hundred times.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(
        nameof(Series), typeof(MetricSeries), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(HealthState), typeof(Sparkline),
        new FrameworkPropertyMetadata(HealthState.Healthy, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled), typeof(bool), typeof(Sparkline),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public MetricSeries? Series { get => (MetricSeries?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public HealthState Accent { get => (HealthState)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public bool Filled { get => (bool)GetValue(FilledProperty); set => SetValue(FilledProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var series = Series;
        double w = ActualWidth, h = ActualHeight;
        if (series is null || series.Points.Count < 2 || w <= 1 || h <= 1) return;

        double min = series.Min, max = series.Max;
        if (max - min < 1e-6) { max = min + 1; }
        double pad = 1.5;
        double innerH = Math.Max(1, h - pad * 2);

        var brush = Theme.ForHealth(Accent);
        var pen = Theme.FrozenPen(brush, 1.25);

        var figure = new PathFigure { StartPoint = Point(0) };
        var segment = new PolyLineSegment();
        for (int i = 1; i < series.Points.Count; i++) segment.Points.Add(Point(i));
        figure.Segments.Add(segment);
        figure.IsClosed = false;

        Point Point(int i)
        {
            double x = w * i / (series.Points.Count - 1.0);
            double norm = (series.Points[i].Value - min) / (max - min);
            return new Point(x, pad + innerH * (1 - norm));
        }

        if (Filled)
        {
            var fillFigure = new PathFigure { StartPoint = new Point(0, h) };
            var fillSeg = new PolyLineSegment();
            for (int i = 0; i < series.Points.Count; i++) fillSeg.Points.Add(Point(i));
            fillSeg.Points.Add(new Point(w, h));
            fillFigure.Segments.Add(fillSeg);
            fillFigure.IsClosed = true;
            var fillGeom = new PathGeometry(new[] { fillFigure });
            fillGeom.Freeze();
            var fill = Theme.Frozen(Theme.WithAlpha(brush.Color, 0.14));
            dc.DrawGeometry(fill, null, fillGeom);
        }

        var geom = new PathGeometry(new[] { figure });
        geom.Freeze();
        dc.DrawGeometry(null, pen, geom);

        // Terminal marker so the eye finds "now" without a legend.
        var last = Point(series.Points.Count - 1);
        dc.DrawEllipse(brush, null, last, 1.8, 1.8);
    }
}

/// <summary>A state marker. Shape is redundant with colour so it survives colour-blindness.</summary>
public sealed class StatePip : FrameworkElement
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(HealthState), typeof(StatePip),
        new FrameworkPropertyMetadata(HealthState.Unknown, FrameworkPropertyMetadataOptions.AffectsRender));

    public HealthState State { get => (HealthState)GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public StatePip()
    {
        Width = 9;
        Height = 9;
        VerticalAlignment = VerticalAlignment.Center;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var brush = Theme.ForHealth(State);
        double s = Math.Min(ActualWidth, ActualHeight);
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        switch (State)
        {
            case HealthState.Critical:
            case HealthState.Offline:
                dc.DrawRectangle(brush, null, new Rect(c.X - s / 2, c.Y - s / 2, s, s));
                break;
            case HealthState.Degraded:
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(c.X, c.Y - s / 2), true, true);
                    ctx.LineTo(new Point(c.X + s / 2, c.Y), true, false);
                    ctx.LineTo(new Point(c.X, c.Y + s / 2), true, false);
                    ctx.LineTo(new Point(c.X - s / 2, c.Y), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(brush, null, g);
                break;
            case HealthState.Maintenance:
                dc.DrawEllipse(null, Theme.FrozenPen(brush, 1.6), c, s / 2 - 0.8, s / 2 - 0.8);
                break;
            default:
                dc.DrawEllipse(brush, null, c, s / 2 - 0.6, s / 2 - 0.6);
                break;
        }
    }
}

/// <summary>
/// Composition of a population as one horizontal bar. Replaces the reflex to render "4 critical,
/// 6 degraded, 18 healthy" as three separate KPI tiles.
/// </summary>
public sealed class DistributionBar : FrameworkElement
{
    public static readonly DependencyProperty CriticalProperty = Reg(nameof(Critical));
    public static readonly DependencyProperty DegradedProperty = Reg(nameof(Degraded));
    public static readonly DependencyProperty HealthyProperty = Reg(nameof(Healthy));
    public static readonly DependencyProperty MaintenanceProperty = Reg(nameof(Maintenance));

    private static DependencyProperty Reg(string name) => DependencyProperty.Register(
        name, typeof(int), typeof(DistributionBar),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Critical { get => (int)GetValue(CriticalProperty); set => SetValue(CriticalProperty, value); }
    public int Degraded { get => (int)GetValue(DegradedProperty); set => SetValue(DegradedProperty, value); }
    public int Healthy { get => (int)GetValue(HealthyProperty); set => SetValue(HealthyProperty, value); }
    public int Maintenance { get => (int)GetValue(MaintenanceProperty); set => SetValue(MaintenanceProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        int total = Critical + Degraded + Healthy + Maintenance;
        if (total == 0)
        {
            dc.DrawRectangle(Theme.Frozen(Theme.WithAlpha(Theme.Unknown.Color, 0.25)), null, new Rect(0, 0, w, h));
            return;
        }

        double x = 0;
        void Seg(int count, SolidColorBrush brush, double alpha)
        {
            if (count == 0) return;
            double segW = w * count / total;
            dc.DrawRectangle(Theme.Frozen(Theme.WithAlpha(brush.Color, alpha)), null, new Rect(x, 0, Math.Max(1.5, segW - 0.5), h));
            x += segW;
        }

        Seg(Critical, Theme.Critical, 1.0);
        Seg(Degraded, Theme.Degraded, 0.95);
        Seg(Maintenance, Theme.Maintenance, 0.8);
        Seg(Healthy, Theme.Healthy, 0.42);
    }
}

/// <summary>A labelled proportion bar for a single value against a threshold.</summary>
public sealed class MeterBar : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(MeterBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(HealthState), typeof(MeterBar),
        new FrameworkPropertyMetadata(HealthState.Healthy, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThresholdProperty = DependencyProperty.Register(
        nameof(Threshold), typeof(double), typeof(MeterBar),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public HealthState State { get => (HealthState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public double Threshold { get => (double)GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var track = Theme.Frozen("#FF1E242D");
        dc.DrawRectangle(track, null, new Rect(0, 0, w, h));
        double v = Math.Clamp(Value, 0, 1);
        dc.DrawRectangle(Theme.ForHealth(State), null, new Rect(0, 0, w * v, h));
        if (!double.IsNaN(Threshold))
        {
            double tx = w * Math.Clamp(Threshold, 0, 1);
            dc.DrawRectangle(Theme.Frozen("#FF6B7686"), null, new Rect(tx - 0.5, -1, 1, h + 2));
        }
    }
}

/// <summary>
/// Density strip: one cell per interval, coloured by worst state in that interval.
/// Used for 24-hour posture without a chart axis.
/// </summary>
public sealed class HeatStrip : FrameworkElement
{
    public static readonly DependencyProperty StatesProperty = DependencyProperty.Register(
        nameof(States), typeof(IReadOnlyList<HealthState>), typeof(HeatStrip),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<HealthState>? States
    {
        get => (IReadOnlyList<HealthState>?)GetValue(StatesProperty);
        set => SetValue(StatesProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var states = States;
        double w = ActualWidth, h = ActualHeight;
        if (states is null || states.Count == 0 || w <= 0 || h <= 0) return;
        double cell = w / states.Count;
        for (int i = 0; i < states.Count; i++)
        {
            var brush = states[i] == HealthState.Healthy
                ? Theme.Frozen(Theme.WithAlpha(Theme.Healthy.Color, 0.34))
                : Theme.ForHealth(states[i]);
            dc.DrawRectangle(brush, null, new Rect(i * cell, 0, Math.Max(1, cell - 1), h));
        }
    }
}
