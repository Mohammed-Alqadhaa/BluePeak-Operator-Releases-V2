using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.Domain;

namespace BluePeak.App.Controls;

/// <summary>
/// The whole estate on one surface, stacked by architectural layer with foundation at the
/// bottom. Selecting an element lights its transitive requirements downward and its blast
/// radius upward, which answers "what breaks if this fails" without leaving the picture.
/// </summary>
public sealed class LayerMap : FrameworkElement
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(EstateModel), typeof(LayerMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(
        nameof(SelectedId), typeof(string), typeof(LayerMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public EstateModel? Model { get => (EstateModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public string? SelectedId { get => (string?)GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }

    public event Action<string>? NodeActivated;

    private readonly List<(Rect Bounds, ServiceNode Node)> _hits = new();
    private string? _hover;

    private static readonly Typeface Ui = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface Mono = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private const double LaneLabel = 128;
    private const double TileHeight = 44;
    private const double TileGap = 8;

    public LayerMap() => ClipToBounds = true;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        string? hit = null;
        foreach (var (bounds, node) in _hits)
            if (bounds.Contains(point)) { hit = node.Id; break; }
        if (hit == _hover) return;
        _hover = hit;
        Cursor = hit is null ? Cursors.Arrow : Cursors.Hand;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_hover is null) return;
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        foreach (var (bounds, node) in _hits)
        {
            if (!bounds.Contains(point)) continue;
            NodeActivated?.Invoke(node.Id);
            e.Handled = true;
            return;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var model = Model;
        if (model is null) return new Size(0, 0);
        double height = 12;
        foreach (EstateLayer layer in Enum.GetValues<EstateLayer>())
        {
            int count = model.ByLayer(layer).Count();
            if (count == 0) continue;
            double usable = Math.Max(200, availableSize.Width) - LaneLabel - 24;
            int perRow = Math.Max(1, (int)(usable / 176));
            int rows = (int)Math.Ceiling(count / (double)perRow);
            height += rows * (TileHeight + TileGap) + 22;
        }
        return new Size(availableSize.Width, height + 12);
    }

    private FormattedText Text(string value, Typeface face, double size, Brush brush, double maxWidth = double.NaN)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush, dpi);
        if (!double.IsNaN(maxWidth))
        {
            ft.MaxTextWidth = Math.Max(10, maxWidth);
            ft.MaxLineCount = 1;
            ft.Trimming = TextTrimming.CharacterEllipsis;
        }
        return ft;
    }

    protected override void OnRender(DrawingContext dc)
    {
        _hits.Clear();
        double w = ActualWidth;
        var model = Model;
        if (model is null || w < 120) return;

        dc.DrawRectangle(Theme.Brush("B.Base"), null, new Rect(0, 0, w, ActualHeight));

        // Relationship sets for the current selection.
        HashSet<string> requires = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> impacted = new(StringComparer.OrdinalIgnoreCase);
        var selected = model.Node(SelectedId);
        if (selected is not null)
        {
            foreach (var node in model.DependencyClosure(selected.Id)) requires.Add(node.Id);
            foreach (var node in model.BlastRadius(selected.Id)) impacted.Add(node.Id);
        }

        double usable = w - LaneLabel - 24;
        int perRow = Math.Max(1, (int)(usable / 176));
        double tileWidth = (usable - (perRow - 1) * TileGap) / perRow;

        double y = 12;
        // Foundation first: the estate is drawn bottom-up in meaning, top-down on screen with
        // the label making the direction explicit.
        foreach (EstateLayer layer in Enum.GetValues<EstateLayer>())
        {
            var nodes = model.ByLayer(layer).OrderByDescending(n => n.Health.Weight()).ThenBy(n => n.Name, StringComparer.Ordinal).ToList();
            if (nodes.Count == 0) continue;

            var rollup = model.Rollup(layer);
            int rows = (int)Math.Ceiling(nodes.Count / (double)perRow);
            double laneHeight = rows * (TileHeight + TileGap) - TileGap;

            // Lane rule and label.
            dc.DrawRectangle(Theme.Frozen("#FF12171F"), null, new Rect(0, y - 6, w, laneHeight + 12));
            dc.DrawRectangle(Theme.Frozen("#FF1A212A"), null, new Rect(0, y - 6, w, 1));

            var name = Text(LayerName(layer), Ui, 11.5, Theme.Brush("B.TextSecondary"), LaneLabel - 20);
            dc.DrawText(name, new Point(16, y + 2));
            var counts = Text($"{rollup.Total} elements", Mono, 9, Theme.Frozen("#FF4A5462"), LaneLabel - 20);
            dc.DrawText(counts, new Point(16, y + 19));
            if (rollup.Critical + rollup.Degraded > 0)
            {
                var bad = Text($"{rollup.Critical + rollup.Degraded} not healthy", Mono, 9,
                    Theme.ForHealth(rollup.Worst), LaneLabel - 20);
                dc.DrawText(bad, new Point(16, y + 33));
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                int row = i / perRow, col = i % perRow;
                var rect = new Rect(
                    LaneLabel + col * (tileWidth + TileGap),
                    y + row * (TileHeight + TileGap),
                    tileWidth, TileHeight);
                DrawTile(dc, rect, nodes[i], selected, requires, impacted);
            }

            y += laneHeight + 22;
        }
    }

    private static string LayerName(EstateLayer layer) => layer switch
    {
        EstateLayer.Foundation => "Foundation",
        EstateLayer.Network => "Network",
        EstateLayer.CoreServices => "Core Services",
        EstateLayer.Identity => "Identity & Trust",
        EstateLayer.Control => "Control",
        EstateLayer.Applications => "Applications",
        EstateLayer.Proof => "Proof",
        _ => layer.ToString()
    };

    private void DrawTile(DrawingContext dc, Rect rect, ServiceNode node, ServiceNode? selected,
        HashSet<string> requires, HashSet<string> impacted)
    {
        _hits.Add((rect, node));

        bool isSelected = selected?.Id == node.Id;
        bool isRequired = requires.Contains(node.Id);
        bool isImpacted = impacted.Contains(node.Id);
        bool related = isSelected || isRequired || isImpacted;
        bool dim = selected is not null && !related;
        bool hover = _hover == node.Id;

        double alpha = dim ? 0.26 : 1.0;

        var background = isSelected ? Theme.Frozen("#FF1A2432")
            : hover ? Theme.Frozen("#FF1B222C")
            : Theme.Frozen(Theme.WithAlpha(Color.FromRgb(0x16, 0x1C, 0x24), dim ? 0.5 : 1.0));

        Color borderColour = isSelected ? Theme.Accent.Color
            : isImpacted ? Theme.WithAlpha(Theme.Critical.Color, 0.5)
            : isRequired ? Theme.WithAlpha(Theme.Accent.Color, 0.35)
            : Color.FromRgb(0x22, 0x2A, 0x34);

        dc.DrawRoundedRectangle(background,
            Theme.FrozenPen(Theme.Frozen(Theme.WithAlpha(borderColour, alpha)), isSelected ? 1.5 : 1), rect, 3, 3);

        dc.DrawRectangle(Theme.Frozen(Theme.WithAlpha(Theme.ForHealth(node.Health).Color, alpha)), null,
            new Rect(rect.Left + 1, rect.Top + 1, 2.5, rect.Height - 2));

        var nameBrush = Theme.Frozen(Theme.WithAlpha(
            dim ? Color.FromRgb(0x6B, 0x76, 0x86) : Color.FromRgb(0xE7, 0xEC, 0xF3), alpha));
        var name = Text(node.Name, Ui, 11, nameBrush, rect.Width - 22);
        dc.DrawText(name, new Point(rect.Left + 11, rect.Top + 6));

        var kindBrush = Theme.Frozen(Theme.WithAlpha(Color.FromRgb(0x69, 0x74, 0x87), alpha));
        var kind = Text(node.Kind, Ui, 9.5, kindBrush, rect.Width - 62);
        dc.DrawText(kind, new Point(rect.Left + 11, rect.Top + 24));

        // Relationship marker: what this tile is to the selection.
        if (isImpacted || isRequired)
        {
            var marker = Text(isImpacted ? "breaks" : "needed", Mono, 8.2,
                isImpacted ? Theme.Critical : Theme.Accent);
            dc.DrawText(marker, new Point(rect.Right - marker.Width - 9, rect.Bottom - marker.Height - 5));
        }
        else if (node.OpenSignals > 0 && !dim)
        {
            var marker = Text(node.OpenSignals + " sig", Mono, 8.2, Theme.ForHealth(node.Health));
            dc.DrawText(marker, new Point(rect.Right - marker.Width - 9, rect.Bottom - marker.Height - 5));
        }
    }
}
