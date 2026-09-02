using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace BluePeak.App.Controls;

/// <summary>
/// WPF has no letter-spacing, and section labels at 10px need it to stop reading as noise.
/// This lays out each glyph itself so the design system keeps its tracked small-caps device.
/// </summary>
public sealed class TrackedText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(TrackedText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackingProperty = DependencyProperty.Register(
        nameof(Tracking), typeof(double), typeof(TrackedText),
        new FrameworkPropertyMetadata(1.1, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(TrackedText),
        new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(TrackedText),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.Register(
        nameof(FontWeight), typeof(FontWeight), typeof(TrackedText),
        new FrameworkPropertyMetadata(FontWeights.SemiBold, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(TrackedText),
        new FrameworkPropertyMetadata(new FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UpperProperty = DependencyProperty.Register(
        nameof(Upper), typeof(bool), typeof(TrackedText),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Tracking { get => (double)GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public bool Upper { get => (bool)GetValue(UpperProperty); set => SetValue(UpperProperty, value); }

    private readonly List<(FormattedText Glyph, double X)> _layout = new();
    private double _height;

    private void Layout()
    {
        _layout.Clear();
        string text = Text ?? "";
        if (Upper) text = text.ToUpperInvariant();
        if (text.Length == 0) { _height = FontSize * 1.35; return; }

        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double x = 0, h = 0;

        foreach (char ch in text)
        {
            var ft = new FormattedText(ch.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, FontSize, Foreground, dpi);
            _layout.Add((ft, x));
            x += ft.WidthIncludingTrailingWhitespace + Tracking;
            h = Math.Max(h, ft.Height);
        }
        _height = h;
        _measuredWidth = Math.Max(0, x - Tracking);
    }

    private double _measuredWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        Layout();
        return new Size(Math.Min(_measuredWidth, availableSize.Width), _height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_layout.Count == 0) Layout();
        double y = (ActualHeight - _height) / 2;
        foreach (var (glyph, x) in _layout)
        {
            if (x > ActualWidth) break;
            glyph.SetForegroundBrush(Foreground);
            dc.DrawText(glyph, new Point(x, y));
        }
    }
}
