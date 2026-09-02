using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BluePeak.App.Design;
using BluePeak.Domain;

namespace BluePeak.App.Controls;

/// <summary>
/// The NOC reasoning surface. The selected service sits on a centre line, what needs it is
/// drawn above, and what it needs is drawn below across two levels. Edges carry protocol and
/// health, so an operator can see in one glance whether a fault is theirs or inherited.
/// </summary>
public sealed class DependencyCanvas : FrameworkElement
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
        nameof(Model), typeof(EstateModel), typeof(DependencyCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SubjectIdProperty = DependencyProperty.Register(
        nameof(SubjectId), typeof(string), typeof(DependencyCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public EstateModel? Model { get => (EstateModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public string? SubjectId { get => (string?)GetValue(SubjectIdProperty); set => SetValue(SubjectIdProperty, value); }

    public event Action<string>? NodeActivated;

    private readonly List<(Rect Bounds, ServiceNode Node)> _hitTargets = new();
    private string? _hoverId;

    private static readonly Typeface Ui = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface UiMedium = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    private static readonly Typeface Mono = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public DependencyCanvas()
    {
        ClipToBounds = true;
        Cursor = Cursors.Arrow;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        string? hit = null;
        foreach (var (bounds, node) in _hitTargets)
            if (bounds.Contains(point)) { hit = node.Id; break; }

        if (hit != _hoverId)
        {
            _hoverId = hit;
            Cursor = hit is null ? Cursors.Arrow : Cursors.Hand;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_hoverId is null) return;
        _hoverId = null;
        Cursor = Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        foreach (var (bounds, node) in _hitTargets)
        {
            if (!bounds.Contains(point)) continue;
            NodeActivated?.Invoke(node.Id);
            e.Handled = true;
            return;
        }
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
        _hitTargets.Clear();
        double w = ActualWidth, h = ActualHeight;
        if (w < 80 || h < 80) return;

        dc.DrawRectangle(Theme.Brush("B.Canvas"), null, new Rect(0, 0, w, h));

        var model = Model;
        var subject = model?.Node(SubjectId);
        if (model is null || subject is null)
        {
            var hint = Text("Select a service to see what it depends on and what depends on it.",
                Ui, 12, Theme.Brush("B.TextTertiary"));
            hint.MaxTextWidth = Math.Min(320, w - 40);
            dc.DrawText(hint, new Point((w - hint.Width) / 2, h / 2 - 10));
            return;
        }

        // Only show as many neighbours as can be rendered legibly. A truncated label is worse
        // than an honest overflow count, so the rest are summarised rather than squeezed.
        int capacity = Math.Max(2, (int)((w - LaneWidth - 20) / 148));

        var allDependents = model.DependentsOf(subject.Id)
            .OrderByDescending(edge => model.Node(edge.FromId)?.Health.Weight() ?? 0).ToList();
        var allDependencies = model.DependenciesOf(subject.Id)
            .OrderByDescending(edge => model.Node(edge.ToId)?.Health.Weight() ?? 0).ToList();

        var dependents = allDependents.Take(capacity).ToList();
        var dependencies = allDependencies.Take(capacity).ToList();
        int hiddenUp = allDependents.Count - dependents.Count;
        int hiddenDown = allDependencies.Count - dependencies.Count;

        // Second level: only the unhealthy branch is expanded, because that is the one that
        // explains the fault. Expanding everything would produce a hairball.
        var deepEdges = new List<DependencyEdge>();
        var deepParent = dependencies
            .Select(edge => model.Node(edge.ToId))
            .FirstOrDefault(node => node is not null && node.Health.IsBad());
        if (deepParent is not null)
            deepEdges = model.DependenciesOf(deepParent.Id)
                .OrderByDescending(edge => model.Node(edge.ToId)?.Health.Weight() ?? 0)
                .Take(capacity).ToList();

        double rowGap = deepEdges.Count > 0
            ? Math.Clamp((h - 150) / 3.0, 96, 168)
            : Math.Clamp((h - 140) / 2.0 * 0.82, 120, 236);
        double centreY = deepEdges.Count > 0 ? h * 0.36 : h * 0.5;
        double upY = centreY - rowGap;
        double downY = centreY + rowGap;
        double deepY = centreY + rowGap * 2;

        // Lane labels down the left edge.
        DrawLane(dc, "DEPENDS ON THIS", upY, w);
        DrawLane(dc, "SUBJECT", centreY, w);
        DrawLane(dc, "REQUIRES", downY, w);
        if (deepEdges.Count > 0) DrawLane(dc, "REQUIRES", deepY, w);

        var firstFailure = model.FirstFailure(subject.Id);

        double centreWidth = Math.Min(280, Math.Max(200, w * 0.32));
        var centreRect = NodeRect(w / 2 + LaneWidth / 2, centreY, centreWidth, 56);
        var upperPoints = Layout(dependents.Count, w, upY, 188, 50);
        var lowerPoints = Layout(dependencies.Count, w, downY, 188, 50);
        var deepPoints = Layout(deepEdges.Count, w, deepY, 188, 50);

        // Edges first so nodes sit on top of them.
        for (int i = 0; i < dependents.Count; i++)
        {
            var node = model.Node(dependents[i].FromId);
            if (node is null) continue;
            DrawEdge(dc, upperPoints[i], centreRect, dependents[i], node.Health, subject.Health, true);
        }
        for (int i = 0; i < dependencies.Count; i++)
        {
            var node = model.Node(dependencies[i].ToId);
            if (node is null) continue;
            DrawEdge(dc, centreRect, lowerPoints[i], dependencies[i], subject.Health, node.Health, false);
        }
        if (deepParent is not null)
        {
            int parentIndex = dependencies.FindIndex(edge => edge.ToId == deepParent.Id);
            if (parentIndex >= 0)
                for (int i = 0; i < deepEdges.Count; i++)
                {
                    var node = model.Node(deepEdges[i].ToId);
                    if (node is null) continue;
                    DrawEdge(dc, lowerPoints[parentIndex], deepPoints[i], deepEdges[i], deepParent.Health, node.Health, false);
                }
        }

        // Nodes.
        for (int i = 0; i < dependents.Count; i++)
        {
            var node = model.Node(dependents[i].FromId);
            if (node is not null) DrawNode(dc, upperPoints[i], node, false, firstFailure?.Id == node.Id);
        }
        DrawNode(dc, centreRect, subject, true, firstFailure?.Id == subject.Id);
        for (int i = 0; i < dependencies.Count; i++)
        {
            var node = model.Node(dependencies[i].ToId);
            if (node is not null) DrawNode(dc, lowerPoints[i], node, false, firstFailure?.Id == node.Id);
        }
        for (int i = 0; i < deepEdges.Count; i++)
        {
            var node = model.Node(deepEdges[i].ToId);
            if (node is not null) DrawNode(dc, deepPoints[i], node, false, firstFailure?.Id == node.Id);
        }

        if (dependents.Count == 0)
            DrawEmptyLane(dc, "Nothing depends on this — a fault here is contained", w, upY);
        else if (hiddenUp > 0)
            DrawOverflow(dc, $"+{hiddenUp} more consumers", w, upY - 40);

        if (dependencies.Count == 0)
            DrawEmptyLane(dc, "No modelled dependencies — this is a leaf", w, downY);
        else if (hiddenDown > 0)
            DrawOverflow(dc, $"+{hiddenDown} more requirements", w, downY + 40);
    }

    private void DrawOverflow(DrawingContext dc, string message, double w, double y)
    {
        var text = Text(message, Mono, 9, Theme.Frozen("#FF56626F"));
        dc.DrawText(text, new Point(w - text.Width - 16, y - text.Height / 2));
    }

    private static Rect NodeRect(double cx, double cy, double width, double height) =>
        new(cx - width / 2, cy - height / 2, width, height);

    private const double LaneWidth = 86;

    private static List<Rect> Layout(int count, double width, double y, double nodeWidth, double nodeHeight)
    {
        var result = new List<Rect>(count);
        if (count == 0) return result;
        double usable = width - LaneWidth - 18;
        double spacing = Math.Min(nodeWidth + 16, usable / count);
        double total = spacing * count;
        double startX = LaneWidth + (usable - total) / 2 + spacing / 2;
        double actualWidth = Math.Min(nodeWidth, spacing - 12);
        for (int i = 0; i < count; i++)
            result.Add(NodeRect(startX + i * spacing, y, actualWidth, nodeHeight));
        return result;
    }

    private void DrawLane(DrawingContext dc, string label, double y, double w)
    {
        var pen = Theme.FrozenPen(Theme.Frozen("#FF1A212A"), 1);
        dc.DrawLine(pen, new Point(LaneWidth - 6, y), new Point(w - 12, y));
        var text = Text(label, Mono, 8.5, Theme.Frozen("#FF4A5462"), LaneWidth - 16);
        dc.DrawText(text, new Point(10, y - text.Height / 2));
    }

    private void DrawEmptyLane(DrawingContext dc, string message, double w, double y)
    {
        var text = Text(message, Ui, 11, Theme.Frozen("#FF4A5462"));
        dc.DrawText(text, new Point((w - text.Width) / 2, y - text.Height / 2));
    }

    private void DrawEdge(DrawingContext dc, Rect from, Rect to, DependencyEdge edge, HealthState fromState, HealthState toState, bool upward)
    {
        // The edge takes the worse of the two endpoints: a healthy consumer of a failing
        // provider is still on a broken path.
        var state = toState.Weight() > fromState.Weight() ? toState : fromState;
        bool bad = state.IsBad();

        var start = new Point(from.Left + from.Width / 2, upward ? from.Bottom : from.Bottom);
        var end = new Point(to.Left + to.Width / 2, upward ? to.Top : to.Top);
        if (upward) { start = new Point(from.Left + from.Width / 2, from.Bottom); end = new Point(to.Left + to.Width / 2, to.Top); }

        var brush = bad ? Theme.ForHealth(state) : Theme.Frozen("#FF39434F");
        var pen = new Pen(Theme.Frozen(Theme.WithAlpha(brush.Color, bad ? 0.9 : 0.75)), bad ? 1.6 : 1.1);
        if (edge.Kind == DependencyKind.Asynchronous)
            pen.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
        pen.Freeze();

        double midY = (start.Y + end.Y) / 2;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.BezierTo(new Point(start.X, midY), new Point(end.X, midY), end, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);

        // Arrow head at the consumer end so direction of need is unambiguous.
        var head = new StreamGeometry();
        using (var ctx = head.Open())
        {
            double dir = upward ? -1 : 1;
            ctx.BeginFigure(new Point(end.X, end.Y), true, true);
            ctx.LineTo(new Point(end.X - 3.4, end.Y - 5.4 * dir), true, false);
            ctx.LineTo(new Point(end.X + 3.4, end.Y - 5.4 * dir), true, false);
        }
        head.Freeze();
        dc.DrawGeometry(pen.Brush, null, head);

        var label = Text(edge.Protocol, Mono, 8.8, Theme.Frozen(bad ? Theme.WithAlpha(brush.Color, 0.95) : Color.FromRgb(0x5A, 0x65, 0x74)));
        double lx = (start.X + end.X) / 2 - label.Width / 2;
        double ly = midY - label.Height / 2;
        dc.DrawRectangle(Theme.Brush("B.Canvas"), null, new Rect(lx - 3, ly, label.Width + 6, label.Height));
        dc.DrawText(label, new Point(lx, ly));
    }

    private void DrawNode(DrawingContext dc, Rect rect, ServiceNode node, bool isSubject, bool isFirstFailure)
    {
        _hitTargets.Add((rect, node));
        bool hover = _hoverId == node.Id;

        var background = isSubject ? Theme.Frozen("#FF1A2432") : Theme.Frozen(hover ? "#FF1B222C" : "#FF161C24");
        var borderColour = isFirstFailure ? Theme.Critical.Color
            : isSubject ? Theme.Accent.Color
            : node.Health.IsBad() ? Theme.WithAlpha(Theme.ForHealth(node.Health).Color, 0.55)
            : Color.FromRgb(0x25, 0x2D, 0x38);
        var border = Theme.FrozenPen(Theme.Frozen(borderColour), isFirstFailure || isSubject ? 1.4 : 1);

        dc.DrawRoundedRectangle(background, border, rect, 3, 3);

        // State bar on the leading edge rather than a coloured fill.
        dc.DrawRectangle(Theme.ForHealth(node.Health), null, new Rect(rect.Left + 1, rect.Top + 1, 2.5, rect.Height - 2));

        double textLeft = rect.Left + 12;
        double available = rect.Width - 22;

        if (isSubject)
        {
            var name = Text(node.Name, UiMedium, 13, Theme.Brush("B.TextPrimary"), available);
            dc.DrawText(name, new Point(textLeft, rect.Top + 10));
            var detail = Text($"{node.Kind} · {node.Owner}", Ui, 10, Theme.Brush("B.TextTertiary"), available);
            dc.DrawText(detail, new Point(textLeft, rect.Top + 30));
        }
        else
        {
            // Two lines of name, then the kind. Wrapping beats truncating a service name.
            var name = Text(node.Name, Ui, 11, Theme.Brush("B.TextPrimary"));
            name.MaxTextWidth = Math.Max(10, available);
            name.MaxLineCount = 2;
            name.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(name, new Point(textLeft, rect.Top + 6));

            var detail = Text(node.Kind, Ui, 9.5, Theme.Brush("B.TextTertiary"), available);
            dc.DrawText(detail, new Point(textLeft, rect.Bottom - detail.Height - 5));
        }

        if (isFirstFailure)
        {
            var badge = Text("FIRST FAILURE", Mono, 8.2, Theme.Critical);
            double bx = rect.Right - badge.Width - 8;
            double by = rect.Top - badge.Height - 4;
            dc.DrawRectangle(Theme.Frozen(Theme.WithAlpha(Theme.Critical.Color, 0.18)), null,
                new Rect(bx - 5, by - 1, badge.Width + 10, badge.Height + 2));
            dc.DrawText(badge, new Point(bx, by));
        }
        else if (isSubject)
        {
            var badge = Text("SELECTED", Mono, 8.2, Theme.Accent);
            double bx = rect.Right - badge.Width - 8;
            double by = rect.Top - badge.Height - 4;
            dc.DrawText(badge, new Point(bx, by));
        }
    }
}
