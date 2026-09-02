using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.Domain;

namespace BluePeak.App.Controls;

/// <summary>
/// Correlation surface for a security case. Entities are placed on a deterministic radial
/// layout around the highest-risk subject, so the same case always draws the same picture and
/// an analyst can point at it in a handover. Edges come from observed relationships only.
/// </summary>
public sealed class EntityGraph : FrameworkElement
{
    public static readonly DependencyProperty EntitiesProperty = DependencyProperty.Register(
        nameof(Entities), typeof(IReadOnlyList<SecurityEntity>), typeof(EntityGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(
        nameof(SelectedId), typeof(string), typeof(EntityGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<SecurityEntity>? Entities
    {
        get => (IReadOnlyList<SecurityEntity>?)GetValue(EntitiesProperty);
        set => SetValue(EntitiesProperty, value);
    }

    public string? SelectedId { get => (string?)GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }

    public event Action<string>? EntityActivated;

    private readonly List<(Rect Bounds, SecurityEntity Entity)> _hits = new();
    private string? _hover;

    private static readonly Typeface Ui = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface UiMedium = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    private static readonly Typeface Mono = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public EntityGraph() => ClipToBounds = true;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        string? hit = null;
        foreach (var (bounds, entity) in _hits)
            if (bounds.Contains(point)) { hit = entity.Id; break; }
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
        foreach (var (bounds, entity) in _hits)
        {
            if (!bounds.Contains(point)) continue;
            EntityActivated?.Invoke(entity.Id);
            e.Handled = true;
            return;
        }
    }

    private static string KindLabel(EntityKind kind) => kind switch
    {
        EntityKind.User => "USER",
        EntityKind.Host => "HOST",
        EntityKind.IpAddress => "ADDRESS",
        EntityKind.Domain => "DOMAIN",
        EntityKind.Process => "PROCESS",
        EntityKind.File => "FILE",
        EntityKind.Account => "ACCOUNT",
        EntityKind.Token => "TOKEN",
        EntityKind.Mailbox => "MAILBOX",
        _ => kind.ToString().ToUpperInvariant()
    };

    private static SolidColorBrush RiskBrush(int risk) =>
        risk >= 75 ? Theme.Critical : risk >= 45 ? Theme.Degraded : Theme.Unknown;

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
        double w = ActualWidth, h = ActualHeight;
        if (w < 60 || h < 60) return;
        dc.DrawRectangle(Theme.Brush("B.Canvas"), null, new Rect(0, 0, w, h));

        var entities = Entities;
        if (entities is null || entities.Count == 0)
        {
            var hint = Text("No entities correlated on this case.", Ui, 11.5, Theme.Brush("B.TextTertiary"));
            dc.DrawText(hint, new Point((w - hint.Width) / 2, h / 2 - 8));
            return;
        }

        // The subject is the highest-risk user, or simply the highest-risk entity.
        var subject = entities.Where(e => e.Kind == EntityKind.User).OrderByDescending(e => e.RiskScore).FirstOrDefault()
                      ?? entities.OrderByDescending(e => e.RiskScore).First();
        var others = entities.Where(e => e.Id != subject.Id).ToList();

        double nodeW = Math.Min(178, Math.Max(120, (w - 60) / 3.1));
        double nodeH = 46;
        var centre = new Point(w / 2, h / 2);
        var centreRect = new Rect(centre.X - nodeW / 2, centre.Y - 30, nodeW, 60);

        double radiusX = Math.Max(nodeW * 0.95, w / 2 - nodeW / 2 - 14);
        double radiusY = Math.Max(70, h / 2 - nodeH / 2 - 26);

        var placed = new List<(Rect Rect, SecurityEntity Entity)>();
        for (int i = 0; i < others.Count; i++)
        {
            // Start at the top and step round; a fixed start angle keeps the layout stable.
            double angle = -Math.PI / 2 + 2 * Math.PI * i / others.Count;
            double cx = centre.X + Math.Cos(angle) * radiusX;
            double cy = centre.Y + Math.Sin(angle) * radiusY;
            cx = Math.Clamp(cx, nodeW / 2 + 8, w - nodeW / 2 - 8);
            cy = Math.Clamp(cy, nodeH / 2 + 18, h - nodeH / 2 - 8);
            placed.Add((new Rect(cx - nodeW / 2, cy - nodeH / 2, nodeW, nodeH), others[i]));
        }

        // Edges: subject to each observed relation, plus relations between the outer ring.
        foreach (var (rect, entity) in placed)
        {
            bool direct = subject.RelatedEntityIds.Contains(entity.Id) || entity.RelatedEntityIds.Contains(subject.Id);
            DrawEdge(dc, centreRect, rect, direct, Math.Max(entity.RiskScore, subject.RiskScore));
        }
        for (int i = 0; i < placed.Count; i++)
            for (int j = i + 1; j < placed.Count; j++)
            {
                var a = placed[i];
                var b = placed[j];
                if (!a.Entity.RelatedEntityIds.Contains(b.Entity.Id) && !b.Entity.RelatedEntityIds.Contains(a.Entity.Id))
                    continue;
                DrawEdge(dc, a.Rect, b.Rect, false, Math.Max(a.Entity.RiskScore, b.Entity.RiskScore));
            }

        foreach (var (rect, entity) in placed) DrawNode(dc, rect, entity, false);
        DrawNode(dc, centreRect, subject, true);
    }

    private void DrawEdge(DrawingContext dc, Rect a, Rect b, bool direct, int risk)
    {
        var from = new Point(a.Left + a.Width / 2, a.Top + a.Height / 2);
        var to = new Point(b.Left + b.Width / 2, b.Top + b.Height / 2);
        var colour = risk >= 75 ? Theme.Critical.Color : risk >= 45 ? Theme.Degraded.Color : Color.FromRgb(0x3A, 0x44, 0x51);
        var pen = new Pen(Theme.Frozen(Theme.WithAlpha(colour, direct ? 0.55 : 0.28)), direct ? 1.4 : 1);
        if (!direct) pen.DashStyle = new DashStyle(new double[] { 3, 4 }, 0);
        pen.Freeze();
        dc.DrawLine(pen, from, to);
    }

    private void DrawNode(DrawingContext dc, Rect rect, SecurityEntity entity, bool isSubject)
    {
        _hits.Add((rect, entity));
        bool selected = SelectedId == entity.Id;
        bool hover = _hover == entity.Id;

        var risk = RiskBrush(entity.RiskScore);
        var background = Theme.Frozen(selected ? "#FF1A2432" : hover ? "#FF1B222C" : "#FF161C24");
        var borderColour = selected ? Theme.Accent.Color
            : entity.RiskScore >= 75 ? Theme.WithAlpha(risk.Color, 0.6)
            : Color.FromRgb(0x25, 0x2D, 0x38);
        dc.DrawRoundedRectangle(background, Theme.FrozenPen(Theme.Frozen(borderColour), selected ? 1.5 : 1), rect, 3, 3);
        dc.DrawRectangle(risk, null, new Rect(rect.Left + 1, rect.Top + 1, 2.5, rect.Height - 2));

        double left = rect.Left + 11;
        double available = rect.Width - 20;

        var kind = Text(KindLabel(entity.Kind), Mono, 8.2, Theme.Frozen("#FF56626F"));
        dc.DrawText(kind, new Point(left, rect.Top + 6));

        // Risk score sits opposite the kind so both stay readable at any node width.
        var score = Text(entity.RiskScore.ToString(), Mono, 9.5, risk);
        dc.DrawText(score, new Point(rect.Right - score.Width - 9, rect.Top + 5));

        var name = Text(entity.Name, isSubject ? UiMedium : Ui, isSubject ? 12.5 : 11,
            Theme.Brush("B.TextPrimary"), available);
        dc.DrawText(name, new Point(left, rect.Top + 19));

        if (isSubject)
        {
            var context = Text(entity.IsManaged ? "Managed subject" : "Unmanaged subject", Ui, 9.5,
                Theme.Brush("B.TextTertiary"), available);
            dc.DrawText(context, new Point(left, rect.Top + 38));
        }
        else if (!entity.IsManaged)
        {
            var flag = Text("unmanaged", Mono, 8.2, Theme.Degraded);
            dc.DrawText(flag, new Point(left, rect.Bottom - flag.Height - 4));
        }
    }
}
