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

public sealed class ModelView : Control
{
    public PhysicsModel Model { get; } = new();

    private readonly DispatcherTimer _timer;
    private DateTime _lastTickAt;
    private bool _firstSize = true;

    // Кэш позиций портов для определения необходимости пересчёта дуг
    private readonly List<Vector2> _lastPortPositions = new();

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

    public bool UseOrthogonalEdges = true;

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

    public void ResetSimulation()
    {
        Model.ResetVelocities();
        _lastTickAt = DateTime.UtcNow;
        _timer.Start();
    }

    /// <summary>
    /// Пересчитывает все ортогональные дуги, только если позиции портов изменились.
    /// </summary>
    private void RecomputeArcs()
    {
        if (!UseOrthogonalEdges) return;

        // Собираем текущие позиции всех портов
        var currentPositions = new List<Vector2>(Model.Edges.Count * 2);
        foreach (var edge in Model.Edges)
        {
            currentPositions.Add(edge.From.GetWorldPosition());
            currentPositions.Add(edge.To.GetWorldPosition());
        }

        // Сравниваем с кэшем — если не изменились, не пересчитываем
        if (_lastPortPositions.Count == currentPositions.Count)
        {
            var changed = false;
            for (var i = 0; i < currentPositions.Count; i++)
            {
                if (Vector2.Distance(_lastPortPositions[i], currentPositions[i]) > 0.5f)
                {
                    changed = true;
                    break;
                }
            }
            if (!changed) return;
        }

        _lastPortPositions.Clear();
        _lastPortPositions.AddRange(currentPositions);

        Model.Arcs.Clear();
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

    /// <summary>
    /// Корректирует зафиксированные дуги после перемещения нод.
    /// Правила:
    /// 1. Первый сегмент идёт из порта в Zone 1 узла → сдвинуть дугу на 1px perpendicular + новый сегмент к порту
    /// 2. Сегмент нулевой длины → удалить точку
    /// 3. Коллинеарные сегменты в одну сторону → сдвинуть общую точку
    /// </summary>
    public void AdjustArcs()
    {
        for (var i = 0; i < Model.Arcs.Count; i++)
        {
            var arc = Model.Arcs[i];
            var edge = Model.Edges[i];
            var points = arc.Points;
            if (points.Count < 2) continue;

            // Правило 2: удаление сегментов нулевой длины
            for (var k = points.Count - 2; k >= 0; k--)
            {
                if (Vector2.Distance(points[k], points[k + 1]) < 1f)
                    points.RemoveAt(k + 1);
            }

            if (points.Count < 2) continue;

            // Восстанавливаем L-точку если потеряна
            if (points.Count == 2 && Math.Abs(points[0].Y - points[1].Y) > OrthogonalRouter.AxisTolerance)
            {
                points.Insert(1, new Vector2(points[1].X, points[0].Y));
            }

            if (points.Count < 3) continue;

            // Правило 1: первый сегмент идёт из порта в Zone 1
            var fromNode = edge.From.Node;
            var port = fromNode.PortRight;

            var fromRect = new RectF(
                fromNode.Position.X - fromNode.Width / 2,
                fromNode.Position.Y - fromNode.Height / 2,
                fromNode.Width,
                fromNode.Height);

            // Проверяем: точка[1] (первый拐角) внутри Zone 1?
            var corner = points[1];
            if (fromRect.Contains(corner))
            {
                // Сдвигаем拐角 perpendicular к первому сегменту
                var dx = corner.X - points[0].X;
                var dy = corner.Y - points[0].Y;
                Vector2 perp;
                if (Math.Abs(dx) > Math.Abs(dy))
                    perp = new Vector2(0, dy > 0 ? -1 : 1); // горизонтальный →垂直ный сдвиг
                else
                    perp = new Vector2(dx > 0 ? -1 : 1, 0); // вертикальный → горизонтальный сдвиг

                // Сдвигаем拐角 и все последующие точки
                for (var k = 1; k < points.Count; k++)
                    points[k] += perp;

                // Добавляем новый сегмент от порта к сдвинутой точке
                points.Insert(0, port);
            }
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

            // Пересчитываем дуги на основе текущих позиций портов
            RecomputeArcs();

            InvalidateVisual();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Tick] {ex}");
        }
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        // Draw edges
        if (Model.Arcs.Count > 0)
        {
            // Ортогональные дуги из зафиксированных маршрутов
            foreach (var arc in Model.Arcs)
            {
                try
                {
                    if (arc.Points.Count < 2) continue;

                    for (var k = 0; k < arc.Points.Count - 1; k++)
                        context.DrawLine(EdgePen, ToPoint(arc.Points[k]), ToPoint(arc.Points[k + 1]));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Render] Arc: {ex.Message}");
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

        // Пересчитываем дуги при перетаскивании ноды
        RecomputeArcs();

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
