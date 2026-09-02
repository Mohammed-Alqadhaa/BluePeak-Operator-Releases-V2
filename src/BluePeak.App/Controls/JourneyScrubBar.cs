using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.Domain;
using BluePeak.Simulation;

namespace BluePeak.App.Controls;

/// <summary>
/// The journey transport. Segments are proportional to real stage durations, the playhead is
/// draggable anywhere on the track, and clicking a segment seeks to that stage. Nothing here
/// is decorative: every mark on the bar corresponds to a stage boundary in the timeline.
/// </summary>
public sealed class JourneyScrubBar : FrameworkElement
{
    public static readonly DependencyProperty TimelineProperty = DependencyProperty.Register(
        nameof(Timeline), typeof(JourneyTimeline), typeof(JourneyScrubBar),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(double), typeof(JourneyScrubBar),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(JourneyScrubBar),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public JourneyTimeline? Timeline { get => (JourneyTimeline?)GetValue(TimelineProperty); set => SetValue(TimelineProperty, value); }
    public double Position { get => (double)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    public event Action? ScrubStarted;
    public event Action<double>? Scrubbed;
    public event Action? ScrubEnded;

    private bool _dragging;
    private int _hoverStage = -1;

    private const double TrackTop = 16;
    private const double TrackHeight = 16;
    private const double LabelTop = 0;

    public JourneyScrubBar()
    {
        Height = 52;
        Cursor = Cursors.Hand;
        Focusable = true;
        SnapsToDevicePixels = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var timeline = Timeline;
        if (timeline is null || timeline.Duration <= 0) return;
        CaptureMouse();
        _dragging = true;
        ScrubStarted?.Invoke();
        Scrubbed?.Invoke(TimeAt(e.GetPosition(this).X));
        Focus();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        if (_dragging)
        {
            Scrubbed?.Invoke(TimeAt(point.X));
            e.Handled = true;
            return;
        }

        int stage = StageAtX(point.X);
        if (stage != _hoverStage)
        {
            _hoverStage = stage;
            UpdateTooltip();
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_hoverStage != -1)
        {
            _hoverStage = -1;
            InvalidateVisual();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        ScrubEnded?.Invoke();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var timeline = Timeline;
        if (timeline is null) return;
        double step = e.KeyboardDevice.Modifiers == ModifierKeys.Shift ? 5 : 1;
        switch (e.Key)
        {
            case Key.Left:
                ScrubStarted?.Invoke();
                Scrubbed?.Invoke(Position - step);
                ScrubEnded?.Invoke();
                e.Handled = true;
                break;
            case Key.Right:
                ScrubStarted?.Invoke();
                Scrubbed?.Invoke(Position + step);
                ScrubEnded?.Invoke();
                e.Handled = true;
                break;
            case Key.Home:
                ScrubStarted?.Invoke();
                Scrubbed?.Invoke(0);
                ScrubEnded?.Invoke();
                e.Handled = true;
                break;
            case Key.End:
                ScrubStarted?.Invoke();
                Scrubbed?.Invoke(timeline.Duration);
                ScrubEnded?.Invoke();
                e.Handled = true;
                break;
        }
    }

    private double TimeAt(double x)
    {
        var timeline = Timeline;
        if (timeline is null || ActualWidth <= 1) return 0;
        return Math.Clamp(x / ActualWidth, 0, 1) * timeline.Duration;
    }

    private int StageAtX(double x)
    {
        var timeline = Timeline;
        if (timeline is null || timeline.Duration <= 0) return -1;
        return timeline.StageAt(TimeAt(x));
    }

    private void UpdateTooltip()
    {
        var timeline = Timeline;
        if (timeline is null || _hoverStage < 0 || _hoverStage >= timeline.Journey.Stages.Count)
        {
            ToolTip = null;
            return;
        }
        var stage = timeline.Journey.Stages[_hoverStage];
        ToolTip = new System.Windows.Controls.StackPanel
        {
            MaxWidth = 320,
            Children =
            {
                new System.Windows.Controls.TextBlock
                {
                    Text = $"{_hoverStage + 1}. {stage.Title}",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Theme.Brush("B.TextPrimary")
                },
                new System.Windows.Controls.TextBlock
                {
                    Text = stage.Caption,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0),
                    Foreground = Theme.Brush("B.TextSecondary")
                }
            }
        };
    }

    private static SolidColorBrush KindBrush(StageKind kind) => kind switch
    {
        StageKind.Establish => Theme.Frozen("#FF4A5563"),
        StageKind.Disassemble => Theme.Frozen("#FF5A6B7E"),
        StageKind.Inspect => Theme.Frozen("#FF3E6E9E"),
        StageKind.Trace => Theme.Frozen("#FF4C88C4"),
        StageKind.Diagnose => Theme.Frozen("#FFB4483F"),
        StageKind.Act => Theme.Frozen("#FFB07F32"),
        StageKind.Verify => Theme.Frozen("#FF359070"),
        StageKind.Reassemble => Theme.Frozen("#FF4A5563"),
        _ => Theme.Frozen("#FF3A424E")
    };

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        // Hit area so the whole strip is draggable, not just the visible track.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        var timeline = Timeline;
        if (timeline is null || timeline.Duration <= 0)
        {
            dc.DrawRectangle(Theme.Frozen("#FF171D26"), null, new Rect(0, TrackTop, w, TrackHeight));
            return;
        }

        var stages = timeline.Journey.Stages;
        double duration = timeline.Duration;
        int current = timeline.StageAt(Position);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var labelFace = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var monoFace = new Typeface(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // Stage segments.
        for (int i = 0; i < stages.Count; i++)
        {
            double x0 = timeline.StageStart(i) / duration * w;
            double x1 = (timeline.StageStart(i) + stages[i].Duration) / duration * w;
            double segWidth = Math.Max(1, x1 - x0 - 1.5);

            var brush = KindBrush(stages[i].Kind);
            double alpha = i == current ? 1.0 : i == _hoverStage ? 0.78 : 0.46;
            var fill = Theme.Frozen(Theme.WithAlpha(brush.Color, alpha));
            dc.DrawRectangle(fill, null, new Rect(x0, TrackTop, segWidth, TrackHeight));

            // Verdict tick: a hairline at the top of the segment in the stage's outcome colour.
            if (stages[i].Verdict != HealthState.Unknown)
            {
                var verdict = Theme.ForHealth(stages[i].Verdict);
                dc.DrawRectangle(Theme.Frozen(Theme.WithAlpha(verdict.Color, i == current ? 1.0 : 0.7)),
                    null, new Rect(x0, TrackTop, segWidth, 2.5));
            }

            // Stage number, drawn only where the segment is wide enough to hold it.
            if (segWidth > 20)
            {
                var number = new FormattedText((i + 1).ToString(), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, monoFace, 9.5,
                    Theme.Frozen(Theme.WithAlpha(Colors.White, i == current ? 0.92 : 0.5)), dpi);
                dc.DrawText(number, new Point(x0 + 4, TrackTop + (TrackHeight - number.Height) / 2));
            }
        }

        // Elapsed overlay across the completed part of the track.
        double playX = Math.Clamp(Position / duration, 0, 1) * w;
        dc.DrawRectangle(Theme.Frozen(Theme.WithAlpha(Colors.White, 0.06)), null,
            new Rect(0, TrackTop, playX, TrackHeight));

        // Playhead.
        var head = Theme.Accent;
        dc.DrawRectangle(head, null, new Rect(playX - 1, TrackTop - 4, 2, TrackHeight + 8));
        var cap = new StreamGeometry();
        using (var ctx = cap.Open())
        {
            ctx.BeginFigure(new Point(playX - 4.5, TrackTop - 9), true, true);
            ctx.LineTo(new Point(playX + 4.5, TrackTop - 9), true, false);
            ctx.LineTo(new Point(playX, TrackTop - 3.5), true, false);
        }
        cap.Freeze();
        dc.DrawGeometry(head, null, cap);

        // Current stage label and time readout.
        var stage = stages[Math.Clamp(current, 0, stages.Count - 1)];
        var title = new FormattedText($"{current + 1} / {stages.Count}   {stage.Title}",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, labelFace, 11.5,
            Theme.Brush("B.TextPrimary"), dpi)
        {
            MaxTextWidth = Math.Max(40, w - 150),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(title, new Point(0, LabelTop));

        var clock = new FormattedText(
            $"{Format(Position)} / {Format(duration)}",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight, monoFace, 11,
            Theme.Brush("B.TextSecondary"), dpi);
        dc.DrawText(clock, new Point(w - clock.Width, LabelTop + 1));

        // Caption under the track: what this beat is actually saying.
        var caption = new FormattedText(stage.Caption, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 11, Theme.Brush("B.TextTertiary"), dpi)
        {
            MaxTextWidth = Math.Max(40, w),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(caption, new Point(0, TrackTop + TrackHeight + 5));
    }

    private static string Format(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"m\:ss");
}
