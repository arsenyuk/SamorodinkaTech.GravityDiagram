using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Avalonia-контрол, отображающий физическую модель графа.
/// Запускает симулирование на таймере (16 мс), рисует узлы, рёбра и дуги.
/// Поддерживает перетаскивание узлов мышью.
/// </summary>
public sealed class ModelView : Control
{
    /// <summary>Физическая модель, отображаемая данным контролом.</summary>
    public PhysicsModel Model { get; set; } = new();

    private readonly DispatcherTimer _timer;
    private DateTime _lastTickAt;
    private bool _firstSize = true;

    private const float SimSpeed = 60f;
    private const float MaxSubstepDt = 1f / 60f;
    private const int MaxSubstepsPerTick = 240;
    private const int TimerIntervalMs = 16;
    private const double LabelFontSize = 14;

    // Drag state
    private PhysicsNode? _dragNode;
    private Vector2 _dragOffset;

    private static readonly Brush NodeFill = new SolidColorBrush(Color.Parse("#4A90D9"));
    private static readonly Brush NodeStroke = new SolidColorBrush(Color.Parse("#2C5F8A"));
    private static readonly Pen NodePen = new(NodeStroke, 2);
    private static readonly Brush ZoneBrush = new SolidColorBrush(Color.FromArgb(30, 74, 144, 217));
    private static readonly Pen ZonePen = new(new SolidColorBrush(Color.FromArgb(80, 74, 144, 217)), 1, new DashStyle([4, 4], 0));
    private static readonly Brush Zone15Brush = new SolidColorBrush(Color.FromArgb(15, 74, 144, 217));
    private static readonly Pen Zone15Pen = new(new SolidColorBrush(Color.FromArgb(40, 74, 144, 217)), 1, new DashStyle([2, 4], 0));
    private static readonly Brush ZoneUBrush = new SolidColorBrush(Color.FromArgb(8, 200, 120, 60));
    private static readonly Pen ZoneUPen = new(new SolidColorBrush(Color.FromArgb(25, 200, 120, 60)), 1, new DashStyle([6, 4], 0));
    private static readonly Pen EdgePen = new(new SolidColorBrush(Color.Parse("#2C5F8A")), 2);

    private static FormattedText MakeText(string text, double fontSize, IBrush brush)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            fontSize,
            brush);
    }

    private static Point ToPoint(Vector2 v) => new(v.X, v.Y);


    public ModelView()
    {
        ClipToBounds = true;

        _lastTickAt = DateTime.UtcNow;
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(TimerIntervalMs),
            DispatcherPriority.Render,
            (_, _) => Tick());
    }

    /// <summary>Включить ортогональную маршрутизацию дуг (иначе — прямые линии).</summary>
    public bool UseOrthogonalEdges = true;

    /// <summary>
    /// Загружает граф по индексу (0 = big A, 1 = big B), центрирует в текущем Bounds
    /// и вычисляет ортогональные дуги.
    /// </summary>
    public void LoadGraph(int index = 0)
    {
        var cx = (float)(Bounds.Width / 2);
        var cy = (float)(Bounds.Height / 2);
        if (index == 0)
            PhysicsModel.CreateGraphABC(Model, cx, cy);
        else
            PhysicsModel.CreateGraphABCSmall(Model, cx, cy);

        // Вычисляем маршруты один раз и сохраняем в Arcs
        Model.Arcs.Clear();
        if (UseOrthogonalEdges)
        {
            foreach (var edge in Model.Edges)
            {
                var fromIdx = Model.Nodes.IndexOf(edge.From.Node);
                var toIdx = Model.Nodes.IndexOf(edge.To.Node);
                var p1 = edge.From.GetWorldPosition();
                var p2 = edge.To.GetWorldPosition();
                var route = OrthogonalRouter.ComputeRoute(
                    p1, p2, fromIdx, toIdx, Model.Nodes);
                Model.Arcs.Add(new Arc(edge) { Points = route });
            }
        }

        ResetSimulation();
    }

    /// <summary>Сбрасывает скорости узлов и перезапускает таймер симуляции.</summary>
    public void ResetSimulation()
    {
        Model.ResetVelocities();
        _lastTickAt = DateTime.UtcNow;
        _timer.Start();
    }

    // RecomputeArcs removed — switched to strictly incremental AdjustArcs()

    /// Удаляет один сегмент нулевой длины. Возвращает true если удалил.
    private static bool RemoveOneZeroSegment(List<Vector2> points)
    {
        for (var k = points.Count - 2; k >= 0; k--)
        {
            if (Vector2.Distance(points[k], points[k + 1]) < 1f)
            {
                points.RemoveAt(k + 1);
                return true;
            }
        }
        return false;
    }

    /// Сдвигает все точки на delta. Возвращает true если что-то сдвинул.
    private static bool ShiftAllPoints(List<Vector2> points, Vector2 delta)
    {
        if (delta.LengthSquared() < 0.0001f) return false;
        for (var k = 0; k < points.Count; k++)
            points[k] += delta;
        return true;
    }

    /// <summary>
    /// Корректирует зафиксированные дуги после перемещения нод.
    /// За один вызов — одно преобразование.
    /// </summary>
public void AdjustArcs()
    {
        const float Eps = 0.01f;
        const float PushOut = 6f; // minimal nudge outside node rect

        bool NearlyEqual(float a, float b, float eps = 0.1f) => MathF.Abs(a - b) <= eps;
        bool IsAxisAligned(in Vector2 a, in Vector2 b, float eps = 0.1f)
            => NearlyEqual(a.X, b.X, eps) || NearlyEqual(a.Y, b.Y, eps);

        bool SegmentIntersectsRect(in Vector2 a, in Vector2 b, in Rect rect)
        {
            // Horizontal segment
            if (NearlyEqual(a.Y, b.Y))
            {
                var y = a.Y;
                var minX = MathF.Min(a.X, b.X);
                var maxX = MathF.Max(a.X, b.X);
                var rLeft = (float)rect.X;
                var rRight = (float)(rect.X + rect.Width);
                var rTop = (float)rect.Y;
                var rBottom = (float)(rect.Y + rect.Height);
                if (y > rTop && y < rBottom)
                {
                    var overlapL = MathF.Max(minX, rLeft);
                    var overlapR = MathF.Min(maxX, rRight);
                    return overlapR > overlapL; // interior overlap
                }
                return false;
            }
            // Vertical segment
            if (NearlyEqual(a.X, b.X))
            {
                var x = a.X;
                var minY = MathF.Min(a.Y, b.Y);
                var maxY = MathF.Max(a.Y, b.Y);
                var rLeft = (float)rect.X;
                var rRight = (float)(rect.X + rect.Width);
                var rTop = (float)rect.Y;
                var rBottom = (float)(rect.Y + rect.Height);
                if (x > rLeft && x < rRight)
                {
                    var overlapT = MathF.Max(minY, rTop);
                    var overlapB = MathF.Min(maxY, rBottom);
                    return overlapB > overlapT;
                }
                return false;
            }
            // Diagonal segments are handled by orthogonality restoration before intersection checks
            return false;
        }

        Vector2 NudgeOutsideY(float x, float y, in Rect rect)
        {
            var rTop = (float)rect.Y;
            var rBottom = (float)(rect.Y + rect.Height);
            var dyTop = MathF.Abs(y - rTop);
            var dyBottom = MathF.Abs(y - rBottom);
            return dyTop <= dyBottom ? new Vector2(x, rTop - PushOut) : new Vector2(x, rBottom + PushOut);
        }
        Vector2 NudgeOutsideX(float x, float y, in Rect rect)
        {
            var rLeft = (float)rect.X;
            var rRight = (float)(rect.X + rect.Width);
            var dxLeft = MathF.Abs(x - rLeft);
            var dxRight = MathF.Abs(x - rRight);
            return dxLeft <= dxRight ? new Vector2(rLeft - PushOut, y) : new Vector2(rRight + PushOut, y);
        }

        bool TryInsertOrthogonalCorner(List<Vector2> points, int segIndex)
        {
            // Insert a single corner for diagonal pair A->B.
            if (segIndex < 0 || segIndex >= points.Count - 1) return false;
            var a = points[segIndex];
            var b = points[segIndex + 1];
            if (IsAxisAligned(a, b)) return false;
            var cand1 = new Vector2(b.X, a.Y);
            var cand2 = new Vector2(a.X, b.Y);
            var d1 = Vector2.Distance(a, cand1) + Vector2.Distance(cand1, b);
            var d2 = Vector2.Distance(a, cand2) + Vector2.Distance(cand2, b);
            var corner = d1 <= d2 ? cand1 : cand2;
            points.Insert(segIndex + 1, corner);
            return true;
        }

        bool RemoveOneCollinearMiddle(List<Vector2> points)
        {
            for (var k = 1; k < points.Count - 1; k++)
            {
                var a = points[k - 1];
                var b = points[k];
                var c = points[k + 1];
                var sameX = NearlyEqual(a.X, b.X) && NearlyEqual(b.X, c.X);
                var sameY = NearlyEqual(a.Y, b.Y) && NearlyEqual(b.Y, c.Y);
                if (sameX || sameY)
                {
                    points.RemoveAt(k);
                    return true;
                }
            }
            return false;
        }

        for (var i = 0; i < Model.Arcs.Count; i++)
        {
            var arc = Model.Arcs[i];
            var edge = Model.Edges[i];
            var points = arc.Points;
            if (points.Count < 2) continue;
            // Remove one zero-length segment upfront; do only that this pass
            if (RemoveOneZeroSegment(points))
                continue;

            var fromPos = edge.From.GetWorldPosition();
            var toPos = edge.To.GetWorldPosition();

            // 1) Endpoint adherence (end segments and neighbors only)
            // Source end pin
            if (Vector2.DistanceSquared(points[0], fromPos) > Eps * Eps)
            {
                points[0] = fromPos;
                if (points.Count >= 2)
                {
                    var p1 = points[1];
                    if (!IsAxisAligned(points[0], p1))
                    {
                        // Adjust p1 to align either X or Y with p0 (pick smaller move)
                        var opt1 = new Vector2(p1.X, points[0].Y);
                        var opt2 = new Vector2(points[0].X, p1.Y);
                        points[1] = Vector2.Distance(p1, opt1) <= Vector2.Distance(p1, opt2) ? opt1 : opt2;
                    }
                }
                // no early continue: we also update target in the same pass
            }
            // Target end pin
            if (Vector2.DistanceSquared(points[^1], toPos) > Eps * Eps)
            {
                points[^1] = toPos;
                if (points.Count >= 2)
                {
                    var p2 = points[^2];
                    if (!IsAxisAligned(p2, points[^1]))
                    {
                        var opt1 = new Vector2(p2.X, points[^1].Y);
                        var opt2 = new Vector2(points[^1].X, p2.Y);
                        points[^2] = Vector2.Distance(p2, opt1) <= Vector2.Distance(p2, opt2) ? opt1 : opt2;
                    }
                }
                // no early continue: allow orthogonality fix / push-out in the same pass
            }

            // Remove zero-length again if created by endpoint snaps — stop after this arc if removed
            if (RemoveOneZeroSegment(points))
                continue;
            // 2) Restore orthogonality (insert one corner for first diagonal segment)
            for (var k = 0; k < points.Count - 1; k++)
            {
                if (!IsAxisAligned(points[k], points[k + 1]))
                {
                    if (TryInsertOrthogonalCorner(points, k))
                        goto NextArc; // one transform per arc
                }
            }

            // 3) Push-out against node rectangles (insert one pivot)
            for (var k = 0; k < points.Count - 1; k++)
            {
                var a = points[k];
                var b = points[k + 1];
                if (!IsAxisAligned(a, b))
                    continue;

                // Check against all nodes except the two endpoint nodes
                foreach (var node in Model.Nodes)
                {
                    if (node == edge.From.Node || node == edge.To.Node)
                        continue;

                    var rect = new Rect(
                        node.Position.X - node.Width / 2,
                        node.Position.Y - node.Height / 2,
                        node.Width,
                        node.Height);

                    if (!SegmentIntersectsRect(a, b, rect))
                        continue;

                    // Insert a single pivot aligned with one endpoint to keep orthogonality.
                    // For horizontal segment, move vertically at x=a.X or x=b.X (pick a.X).
                    // For vertical segment, move horizontally at y=a.Y (pick y of a).
                    Vector2 pivot;
                    if (NearlyEqual(a.Y, b.Y))
                    {
                        var n = NudgeOutsideY(a.X, a.Y, rect);
                        // Guard against zero-length insertion
                        if (!NearlyEqual(n.X, a.X) || !NearlyEqual(n.Y, a.Y))
                            pivot = n;
                        else
                            continue;
                    }
                    else
                    {
                        var n = NudgeOutsideX(a.X, a.Y, rect);
                        if (!NearlyEqual(n.X, a.X) || !NearlyEqual(n.Y, a.Y))
                            pivot = n;
                        else
                            continue;
                    }

                    points.Insert(k + 1, pivot);
                    goto NextArc; // one transform per arc
                }
            }

            // 4) Degeneracy cleanup (only if nothing else applied)
            if (RemoveOneZeroSegment(points))
                continue;
            if (RemoveOneCollinearMiddle(points))
                continue;

        NextArc: ;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lastTickAt = DateTime.UtcNow;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_firstSize && Bounds.Width > 0 && Bounds.Height > 0)
        {
            _firstSize = false;
            LoadGraph();
        }
        _timer.Start();
    }

    /// <summary>Основной цикл симуляции (вызывается DispatcherTimer каждые 16 мс).</summary>
    private void Tick()
    {
        try
        {
            if (Bounds.Width < 1 || Bounds.Height < 1) return;

            var now = DateTime.UtcNow;
            var dt = (float)(now - _lastTickAt).TotalSeconds;
            _lastTickAt = now;
            dt = Math.Clamp(dt, 0f, 0.05f);

            var simDt = dt * SimSpeed;
            var steps = (int)Math.Ceiling(simDt / MaxSubstepDt);
            steps = Math.Clamp(steps, 1, MaxSubstepsPerTick);
            var subDt = simDt / steps;

            for (var i = 0; i < steps; i++)
                Model.Step(subDt);

            // Инкрементально корректируем дуги (без полного пересчёта)
            AdjustArcs();

            InvalidateVisual();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Tick] {ex}");
        }
    }

    /// <summary>
    /// Отрисовка фона, дуг (ортогональных полилиний), прямоугольников узлов,
    /// зон отталкивания и портов.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        // Draw edges
        if (Model.Arcs.Count > 0)
        {
            // Ортогональные дуги из зафиксированных маршрутов
            foreach (var arc in Model.Arcs)
            {
                if (arc.Points.Count < 2) continue;

                for (var k = 0; k < arc.Points.Count - 1; k++)
                {
                    var a = arc.Points[k];
                    var b = arc.Points[k + 1];
                    bool finite = !(float.IsNaN(a.X) || float.IsNaN(a.Y) || float.IsInfinity(a.X) || float.IsInfinity(a.Y)
                                   || float.IsNaN(b.X) || float.IsNaN(b.Y) || float.IsInfinity(b.X) || float.IsInfinity(b.Y));
                    if (!finite) continue;
                    try
                    {
                        context.DrawLine(EdgePen, ToPoint(a), ToPoint(b));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Render] Arc seg {k}: {ex.Message}");
                        // Skip only this segment, continue drawing rest
                    }
                }
            }
        }
        else
        {
            // Прямые линии (не-ортогональный режим)
            foreach (var edge in Model.Edges)
            {
                context.DrawLine(EdgePen, ToPoint(edge.From.GetWorldPosition()), ToPoint(edge.To.GetWorldPosition()));
            }
        }

        foreach (var node in Model.Nodes)
            {
                var center = new Point(node.Position.X, node.Position.Y);

                // Zone 1: own radius
                var nodeRadius = MathF.Sqrt(node.Width * node.Width + node.Height * node.Height) / 2;
                context.DrawEllipse(ZoneBrush, ZonePen, center, nodeRadius, nodeRadius);

                // Zone 2: 2 × own radius
                context.DrawEllipse(Zone15Brush, Zone15Pen, center, 2 * nodeRadius, 2 * nodeRadius);

                // Zone 3: max Zone 2 of connected neighbors
                var nodeIdx = Model.Nodes.IndexOf(node);
                var zone3 = 0f;
                foreach (var e in Model.Edges)
                {
                    var neighbor = -1;
                    var ei = Model.Nodes.IndexOf(e.From.Node);
                    var ej = Model.Nodes.IndexOf(e.To.Node);
                    if (ei == nodeIdx) neighbor = ej;
                    else if (ej == nodeIdx) neighbor = ei;
                    if (neighbor >= 0)
                    {
                        var n = Model.Nodes[neighbor];
                        var nr = MathF.Sqrt(n.Width * n.Width + n.Height * n.Height) / 2;
                        var z2 = 2 * nr;
                        if (z2 > zone3) zone3 = z2;
                    }
                }
                if (zone3 > 0)
                    context.DrawEllipse(ZoneUBrush, ZoneUPen, center, zone3, zone3);

                // Draw node rectangle
                var rect = new Rect(
                    node.Position.X - node.Width / 2,
                    node.Position.Y - node.Height / 2,
                    node.Width,
                    node.Height);
                context.DrawRectangle(null, NodePen, rect, 8);

                var ft = MakeText(node.Label, LabelFontSize, NodeStroke);
                var textX = node.Position.X - ft.Width / 2;
                var textY = node.Position.Y - ft.Height / 2;
                context.DrawText(ft, new Point(textX, textY));

                // Draw ports
                var portBrush = new SolidColorBrush(Color.Parse("#2C5F8A"));
                const float portR = 5;
                context.DrawEllipse(portBrush, null, ToPoint(node.PortLeft), portR, portR);
                context.DrawEllipse(portBrush, null, ToPoint(node.PortRight), portR, portR);
            }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var point = new Vector2((float)pos.X, (float)pos.Y);

        for (var i = Model.Nodes.Count - 1; i >= 0; i--)
        {
            var node = Model.Nodes[i];
            var halfW = node.Width / 2;
            var halfH = node.Height / 2;

            if (point.X >= node.Position.X - halfW &&
                point.X <= node.Position.X + halfW &&
                point.Y >= node.Position.Y - halfH &&
                point.Y <= node.Position.Y + halfH)
            {
                _dragNode = node;
                _dragOffset = new Vector2(node.Position.X - point.X, node.Position.Y - point.Y);
                e.Pointer.Capture(this);
                break;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragNode == null) return;

        var pos = e.GetPosition(this);
        _dragNode.Position = new Vector2(
            (float)pos.X + _dragOffset.X,
            (float)pos.Y + _dragOffset.Y);
        _dragNode.Velocity = Vector2.Zero;

        // Корректируем дуги при перетаскивании ноды
        AdjustArcs();

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragNode != null)
        {
            _dragNode = null;
            e.Pointer.Capture(null);
        }
    }
}
