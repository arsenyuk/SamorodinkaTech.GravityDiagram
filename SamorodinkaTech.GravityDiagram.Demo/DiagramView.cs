using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Demo;

public sealed class DiagramView : Control
{
    private const float ArcOutDistance = 18f;
    private const float ArcLaneSpacing = 14f;
    private const float ArcNodeClearance = 10f;
    private const float ArcOverlapClearance = 10f;
    private const float LabelMaxWidth = 240f;
    private const float LabelFontSize = 12f;
    private const float PortLabelPadding = 3f;
    private const float ArcLabelPadding = 1f;

    // Must match DrawLabelWithBackground() so placement avoids the rendered label block.
    private const float LabelBackgroundPadding = 2f;
    private const float LabelNodeClearance = 8f;
    private const float LabelArcClearance = 6f;
    private const int LabelPlacementIterations = 28;
    private const float PortLabelSpringK = 0.28f;
    private const float ArcCrossedExitSmallLanePenalty = 500f;
    private const float ArcLabelSpringK = 0.08f;
    private const float PortLabelMaxDrift = 18f;
    private const float ArcLabelMaxDrift = 80f;

    private const float ArcLabelDistanceFromArc = 4f;

    // IMPORTANT: port headers should be fixed relative to their ports (no independent drifting).
    // To prevent rectangles from covering foreign port headers, we move NODES (not headers).
    private const bool EnableArcLabelAwareNodeMovement = false;
    private const bool EnablePortLabelAwareNodeMovement = true;

    private const int LabelAwareSeparationIterations = 2;
    private const float LabelAwareNodePushPadding = 1f;
    private const float LabelAwareNodePushDamping = 0.12f;
    private const float LabelAwareMaxNodeMovePerUpdate = 3f;
    private const int WarmStartMaxSteps = 2200;
    private const int WarmStartCheckEvery = 20;
    private const float WarmStartStopSpeed = 18f;
    private const float WarmStartDt = 1f / 60f;
    private const int WarmStartMaxTicksWithLabels = 1200;

    private const double NodeTextPadding = 1.0;

    public enum RectTextHAlign
    {
        Left,
        Center,
        Right,
    }

    public enum RectTextVAlign
    {
        Top,
        Center,
        Bottom,
    }

    public RectTextHAlign NodeTextHorizontalAlignment { get; set; } = RectTextHAlign.Center;
    public RectTextVAlign NodeTextVerticalAlignment { get; set; } = RectTextVAlign.Center;

    private static readonly Color[] Rainbow =
    {
        Colors.Red,
        Colors.Orange,
        Colors.Yellow,
        Colors.Green,
        Colors.Cyan,
        Colors.Blue,
        Colors.Purple,
    };

    private static Color RainbowColor(int index)
        => Rainbow[Math.Abs(index) % Rainbow.Length];

    // Run the simulation faster than real-time so changes are visible.
    // Substep so each physics step remains stable.
    private const float LiveSimSpeed = 60f;
    private const float LiveMaxSubstepDt = 1f / 60f;
    private const int LiveMaxSubstepsPerTick = 240;

    private const int AutoStopCheckEveryTicks = 10;
    private const int AutoStopNoPixelMoveTicks = 30;

    private readonly DispatcherTimer _timer;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastTickAt;

    private bool _didWarmStartAfterMeasure;

    // Keeps arc routes stable between frames (prevents “blinking” when two candidates have similar scores).
    private readonly Dictionary<DiagramId, float> _arcExtraLaneShiftById = new();

    // Label placement caches.
    private readonly Dictionary<DiagramId, Vector2> _portLabelOriginById = new();
    private readonly Dictionary<DiagramId, Vector2> _arcLabelOriginById = new();
    private readonly List<RectF> _lastPlacedLabelObstacles = new();

    private IReadOnlyList<RoutedArc>? _cachedRoutedArcs;
    private PlacedLabels? _cachedPlacedLabels;

    private RectNode? _dragNode;
    private Vector2 _dragOffset;

    private int _tickCounter;
    private bool _isAutoStopped;
    private int _noPixelMoveTicks;

    private int _lastPixelMoveTick;
    private string _lastPixelMoveInfo = string.Empty;

    public DiagramView()
    {
        ClipToBounds = true;
        Diagram = SampleDiagram.CreateThreeNode();
        Engine = new GravityLayoutEngine(new LayoutSettings
        {
            NodeMass = 12.8f,
            // Moderate attraction for compact, but not "glued" layout.
                // Mutual attraction between rectangles (keep small by default).
                BackgroundPairGravity = 0.06f,
            EdgeSpringRestLength = 220f,
			ConnectedArcAttractionK = 6.0f,
            // Arcs try to shorten, but stop pulling once MinNodeSpacing is reached.
            MinimizeArcLength = true,
                OverlapRepulsionK = 35f,
            MinNodeSpacing = 8f,
                UseHardMinSpacing = true,
                HardMinSpacingIterations = 6,
                HardMinSpacingSlop = 0.5f,
            Softening = 50f,
            Drag = 2.4f,
            MaxSpeed = 2400f,
        });

        // NOTE: we do a physics-only warm-start here, but bounds are not measured yet.
        // Label/port-header aware spacing can only stabilize once we know Bounds.
        WarmStartLayout(includeLabelPass: false);

        _lastTickAt = DateTimeOffset.UtcNow;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
        // Start timer after the view is measured so we can do a one-time post-measure warm-start.
    }

    private void ForceAutoStopNow()
    {
        _isAutoStopped = true;
        _noPixelMoveTicks = AutoStopNoPixelMoveTicks;
        foreach (var n in Diagram.Nodes)
        {
            n.Velocity = Vector2.Zero;
        }
        if (_timer.IsEnabled)
            _timer.Stop();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        if (!_didWarmStartAfterMeasure && e.NewSize.Width > 1 && e.NewSize.Height > 1)
        {
            // One-time post-measure warm-start so label/port-header spacing doesn't keep
            // moving the layout after the initial render.
            _didWarmStartAfterMeasure = true;

            if (_timer.IsEnabled)
                _timer.Stop();

            var stable = WarmStartLayout(includeLabelPass: true);
            UpdateTextLayoutAndSpacing();
            _lastTickAt = DateTimeOffset.UtcNow;

            if (stable)
            {
                ForceAutoStopNow();
                InvalidateVisual();
                return;
            }
        }

        if (!_timer.IsEnabled)
            _timer.Start();
    }

    public Diagram Diagram { get; private set; }
    public GravityLayoutEngine Engine { get; private set; }

    public AutoStopDebugSnapshot GetAutoStopDebugSnapshot()
        => new(
            TickCounter: _tickCounter,
            IsAutoStopped: _isAutoStopped,
            NoPixelMoveTicks: _noPixelMoveTicks,
            AutoStopNoPixelMoveTicksThreshold: AutoStopNoPixelMoveTicks,
            LastPixelMoveTick: _lastPixelMoveTick,
            LastPixelMoveInfo: _lastPixelMoveInfo,
            EnablePortLabelAwareNodeMovement: EnablePortLabelAwareNodeMovement,
            EnableArcLabelAwareNodeMovement: EnableArcLabelAwareNodeMovement);

    public void ResetVelocities()
    {
        foreach (var n in Diagram.Nodes)
        {
            n.Velocity = Vector2.Zero;
        }
        _arcExtraLaneShiftById.Clear();

        ResumeSimulation();
    }

    public void StabilizeLayout()
    {
        ResumeSimulation();
        ResetVelocities();
        var stable = WarmStartLayout(includeLabelPass: true);
        UpdateTextLayoutAndSpacing();
        if (stable)
        {
            ForceAutoStopNow();
        }
        InvalidateVisual();
    }

    public void SetDiagram(Diagram diagram)
    {
        Diagram = diagram ?? throw new ArgumentNullException(nameof(diagram));
        Diagram.DistributeAllPortsProportionally();
        ResumeSimulation();
        var stable = WarmStartLayout(includeLabelPass: true);
        UpdateTextLayoutAndSpacing();
        if (stable)
        {
            ForceAutoStopNow();
        }
        InvalidateVisual();
    }

    private void ResumeSimulation()
    {
        _isAutoStopped = false;
        _tickCounter = 0;
        _noPixelMoveTicks = 0;
        _lastTickAt = DateTimeOffset.UtcNow;
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private static int Pixel(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    private void TryAutoStop(float nextDt, bool anyNodeMovedPixelsThisTick)
    {
        if (_isAutoStopped) return;
        if (_dragNode is not null) return;
        if (nextDt <= 0f) return;

        if (anyNodeMovedPixelsThisTick) return;
        if (_noPixelMoveTicks < AutoStopNoPixelMoveTicks) return;

        {
            _isAutoStopped = true;
            foreach (var n in Diagram.Nodes)
            {
                n.Velocity = Vector2.Zero;
            }
            _timer.Stop();
        }
    }

    private bool WarmStartLayout(bool includeLabelPass)
    {
        // Make initial presentation stable/compact, without watching the simulation “converge”.
        // This runs fast for small graphs and improves first impression a lot.
        foreach (var n in Diagram.Nodes)
        {
            n.Velocity = Vector2.Zero;
        }

        if (includeLabelPass)
        {
            // For visual stability, mimic the real Tick(): many small physics substeps,
            // then ONE label/layout pass, then check pixel movement.
            // This avoids “warm-start is stable, but layout pass keeps nudging nodes afterwards”.

            var stableTicks = 0;
            var nodesCount = Diagram.Nodes.Count;

            var simDt = WarmStartDt * LiveSimSpeed;
            var steps = (int)Math.Clamp(MathF.Ceiling(simDt / LiveMaxSubstepDt), 1f, LiveMaxSubstepsPerTick);
            var subDt = simDt / steps;

            for (var tick = 0; tick < WarmStartMaxTicksWithLabels; tick++)
            {
                var pxBeforeX = new int[nodesCount];
                var pxBeforeY = new int[nodesCount];
                for (var ni = 0; ni < nodesCount; ni++)
                {
                    var c = Diagram.Nodes[ni].Bounds.Center;
                    pxBeforeX[ni] = Pixel((float)c.X);
                    pxBeforeY[ni] = Pixel((float)c.Y);
                }

                for (var i = 0; i < steps; i++)
                {
                    Engine.Step(Diagram, subDt);
                }

                UpdateTextLayoutAndSpacing();

                var anyMovedPixels = false;
                for (var ni = 0; ni < nodesCount; ni++)
                {
                    var c = Diagram.Nodes[ni].Bounds.Center;
                    var x = Pixel((float)c.X);
                    var y = Pixel((float)c.Y);
                    if (x != pxBeforeX[ni] || y != pxBeforeY[ni])
                    {
                        anyMovedPixels = true;
                        break;
                    }
                }

                if (anyMovedPixels)
                    stableTicks = 0;
                else
                    stableTicks++;

                if (stableTicks >= AutoStopNoPixelMoveTicks)
                    return true;
            }

            return false;
        }

        // Physics-only warm-start (pre-measure): keep it bounded and fast.
        for (var i = 0; i < WarmStartMaxSteps; i++)
        {
            Engine.Step(Diagram, WarmStartDt);

            if (i % WarmStartCheckEvery == 0)
            {
                var maxSpeed = 0f;
                foreach (var n in Diagram.Nodes)
                {
                    maxSpeed = MathF.Max(maxSpeed, n.Velocity.Length());
                }
                if (maxSpeed < WarmStartStopSpeed)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Tick()
    {
        var now = DateTimeOffset.UtcNow;
        var dt = (float)(now - _lastTickAt).TotalSeconds;
        _lastTickAt = now;

        // Clamp dt to avoid huge jumps if the app was paused.
        dt = Math.Clamp(dt, 0f, 0.05f);

        if (_dragNode is null)
        {
            var simDt = dt * LiveSimSpeed;
            if (simDt > 0f)
            {
                var nodesCount = Diagram.Nodes.Count;
                var pxBeforeX = new int[nodesCount];
                var pxBeforeY = new int[nodesCount];
                for (var i = 0; i < nodesCount; i++)
                {
                    var c = Diagram.Nodes[i].Bounds.Center;
                    pxBeforeX[i] = Pixel((float)c.X);
                    pxBeforeY[i] = Pixel((float)c.Y);
                }

                var steps = (int)Math.Clamp(MathF.Ceiling(simDt / LiveMaxSubstepDt), 1f, LiveMaxSubstepsPerTick);
                var subDt = simDt / steps;
                for (var i = 0; i < steps; i++)
                {
                    Engine.Step(Diagram, subDt);
                }

                // Text/layout adjustments can also move nodes.
                // IMPORTANT: only count *final* pixel movement after ALL passes.
                // Otherwise we can detect transient movement (physics) that gets cancelled by the
                // label/layout pass in the same tick, preventing auto-stop forever.
                UpdateTextLayoutAndSpacing();

                var anyMovedPixels = false;
                string? firstMoveInfo = null;
                for (var i = 0; i < nodesCount; i++)
                {
                    var c = Diagram.Nodes[i].Bounds.Center;
                    var x = Pixel((float)c.X);
                    var y = Pixel((float)c.Y);
                    if (x != pxBeforeX[i] || y != pxBeforeY[i])
                    {
                        anyMovedPixels = true;
                        firstMoveInfo = $"{Diagram.Nodes[i].Id.Value}: ({pxBeforeX[i]},{pxBeforeY[i]}) -> ({x},{y})";
                        break;
                    }
                }

                if (anyMovedPixels)
                    _noPixelMoveTicks = 0;
                else
                    _noPixelMoveTicks++;

                _tickCounter++;

                if (anyMovedPixels && firstMoveInfo is not null)
                {
                    _lastPixelMoveTick = _tickCounter;
                    _lastPixelMoveInfo = firstMoveInfo;
                    if (_noPixelMoveTicks >= 10)
                    {
                        Console.WriteLine($"[auto-stop] pixel move at tick {_tickCounter}: {_lastPixelMoveInfo}");
                    }
                }

                if (_tickCounter % AutoStopCheckEveryTicks == 0)
                {
                    TryAutoStop(subDt, anyMovedPixels);
                }
            }
        }

        // When dragging, we still want labels/arcs to refresh, but should not move nodes.
        if (_dragNode is not null)
        {
            UpdateTextLayoutAndSpacing();
        }

        InvalidateVisual();
    }

    private void DrawDebugOverlay(DrawingContext context)
    {
        // 1) Show min-spacing bounds around rectangles.
        var spacing = Engine.Settings.MinNodeSpacing;
        if (spacing > 0.1f)
        {
            var dash = new DashStyle(new double[] { 4, 4 }, 0);
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(230, 120, 70)), 1, dashStyle: dash);
            foreach (var node in Diagram.Nodes)
            {
                var r = node.Bounds;
                var m = spacing / 2f;
                var rr = new Rect(r.Left - m, r.Top - m, r.Width + 2 * m, r.Height + 2 * m);
                context.DrawRectangle(null, pen, rr, 10);
            }
        }

        // 2) Show net force vectors (sum of all forces) per node.
        var forces = Engine.LastForcesByNodeId;
        if (forces.Count == 0) return;

        var max = 0f;
        foreach (var node in Diagram.Nodes)
        {
            if (forces.TryGetValue(node.Id, out var f))
            {
                max = MathF.Max(max, f.Length());
            }
        }
        if (max < 0.001f) return;

        // Scale so that the biggest force is ~70px.
        var scale = 70f / max;
        scale = Math.Clamp(scale, 0.002f, 0.08f);

        var forcePen = new Pen(new SolidColorBrush(Color.FromRgb(200, 40, 40)), 1.4);
        var forceFill = forcePen.Brush ?? Brushes.Red;
        foreach (var node in Diagram.Nodes)
        {
            if (!forces.TryGetValue(node.Id, out var f))
                continue;

            var start = node.Position;
            var end = start + f * scale;
            DrawVectorArrow(context, start, end, forceFill, forcePen);
        }

        DrawLabel(context,
            $"auto-stop: noPixelMoveTicks={_noPixelMoveTicks}/{AutoStopNoPixelMoveTicks} • lastMoveTick={_lastPixelMoveTick} • {_lastPixelMoveInfo}",
            new Point(10, 28), Brushes.DimGray, 12);
    }

    private static void DrawVectorArrow(DrawingContext context, Vector2 start, Vector2 end, IBrush fill, Pen pen)
    {
        var a = new Point(start.X, start.Y);
        var b = new Point(end.X, end.Y);
        context.DrawLine(pen, a, b);

        var d = end - start;
        var len = d.Length();
        if (len < 1f) return;
        var dir = d / len;
        var perp = new Vector2(-dir.Y, dir.X);

        var arrowLen = MathF.Min(12f, len * 0.35f);
        var arrowWidth = 8f;
        var tip = end;
        var baseCenter = tip - dir * arrowLen;
        var left = baseCenter + perp * (arrowWidth * 0.5f);
        var right = baseCenter - perp * (arrowWidth * 0.5f);

        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(new Point(tip.X, tip.Y), isFilled: true);
            g.LineTo(new Point(left.X, left.Y));
            g.LineTo(new Point(right.X, right.Y));
            g.EndFigure(isClosed: true);
        }
        context.DrawGeometry(fill, null, geom);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        ResumeSimulation();
        var p = e.GetPosition(this);
        var wp = new Vector2((float)p.X, (float)p.Y);

        var nodes = Diagram.Nodes;
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var n = nodes[i];
            var b = n.Bounds;
            if (wp.X >= b.Left && wp.X <= b.Right && wp.Y >= b.Top && wp.Y <= b.Bottom)
            {
                _dragNode = n;
                _dragOffset = n.Position - wp;
                n.Velocity = Vector2.Zero;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragNode is null) return;
        var p = e.GetPosition(this);
        var wp = new Vector2((float)p.X, (float)p.Y);
        _dragNode.Position = wp + _dragOffset;
        _dragNode.Velocity = Vector2.Zero;
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragNode is null) return;
        _dragNode = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void DrawNodes(DrawingContext context)
    {
        var nodesOrdered = Diagram.Nodes.OrderBy(n => n.Id.Value, StringComparer.Ordinal).ToArray();
        var nodeIndexById = nodesOrdered
            .Select((n, i) => (n.Id, i))
            .ToDictionary(x => x.Id, x => x.i);

        var portsOrdered = Diagram.Ports.OrderBy(p => p.Id.Value, StringComparer.Ordinal).ToArray();
        var portIndexById = portsOrdered
            .Select((p, i) => (p.Id, i))
            .ToDictionary(x => x.Id, x => x.i);

        foreach (var node in Diagram.Nodes)
        {
            nodeIndexById.TryGetValue(node.Id, out var idx);
            var color = RainbowColor(idx);
            var stroke = new Pen(new SolidColorBrush(color), 1.6);
            var fill = new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B));

            var r = node.Bounds;
            var rect = new Rect(r.Left, r.Top, r.Width, r.Height);
            context.DrawRectangle(fill, stroke, rect, 8);
            DrawRectLabel(context, node.Text, rect, Brushes.Black, 14, NodeTextHorizontalAlignment, NodeTextVerticalAlignment);
        }

        // Ports are drawn after nodes.
        var portOutline = new Pen(Brushes.Black, 1);
        foreach (var port in Diagram.Ports)
        {
            portIndexById.TryGetValue(port.Id, out var pidx);
            var pcolor = RainbowColor(pidx);
            var portBrush = new SolidColorBrush(pcolor);

            var node = Diagram.Nodes.FirstOrDefault(n => n.Id == port.Ref.NodeId);
            if (node is null) continue;

            var p = GravityLayoutEngine.GetPortWorldPosition(node, port.Ref);
            context.DrawEllipse(portBrush, portOutline, new Point(p.X, p.Y), 4, 4);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        // Draw using cached artifacts produced by Tick().
        var routed = _cachedRoutedArcs ?? Array.Empty<RoutedArc>();
        var placed = _cachedPlacedLabels;

        DrawArcs(context, routed);
        DrawNodes(context);
        if (placed is not null)
        {
            DrawLabels(context, placed);
        }
        DrawDebugOverlay(context);

        var t = (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;
        DrawLabel(context, $"drag to move • t={t:0.0}s • nodes={Diagram.Nodes.Count} arcs={Diagram.Arcs.Count}",
            new Point(10, 10), Brushes.DimGray, 12);
    }

    private bool UpdateTextLayoutAndSpacing()
    {
        // If the view is not measured yet, don't try to place labels.
        if (Bounds.Width <= 1 || Bounds.Height <= 1)
            return false;

        var beforeX = Array.Empty<int>();
        var beforeY = Array.Empty<int>();
        if (_dragNode is null)
        {
            var ncount = Diagram.Nodes.Count;
            beforeX = new int[ncount];
            beforeY = new int[ncount];
            for (var i = 0; i < ncount; i++)
            {
                var c = Diagram.Nodes[i].Bounds.Center;
                beforeX[i] = Pixel((float)c.X);
                beforeY[i] = Pixel((float)c.Y);
            }
        }

        // 1) Route arcs using last label obstacles (stability).
        var routed1 = ComputeRoutedArcs(_lastPlacedLabelObstacles);

        // 2) Place labels using routed1.
        var placed1 = PlaceAllLabels(routed1);

        // 3) Never push nodes because of ARC labels (keep blocks tight).
        if (EnableArcLabelAwareNodeMovement && _dragNode is null)
        {
            ApplyArcLabelAwareNodeSeparation(placed1);
        }

        // 4) Route again, now avoiding label rectangles.
        var labelObstacles = placed1.AllLabelRects.Select(ToRectF).ToList();
        var routed2 = ComputeRoutedArcs(labelObstacles);

        // 5) Place labels again using final routes.
        var placed2 = PlaceAllLabels(routed2);

        // 6) Ensure nodes don't cover foreign port headers (move nodes, not headers).
        if (EnablePortLabelAwareNodeMovement && ApplyPortLabelAwareNodeSpacing(placed2))
        {
            var labelObstacles2 = placed2.AllLabelRects.Select(ToRectF).ToList();
            var routed3 = ComputeRoutedArcs(labelObstacles2);
            var placed3 = PlaceAllLabels(routed3);
            routed2 = routed3;
            placed2 = placed3;
        }

        _lastPlacedLabelObstacles.Clear();
        _lastPlacedLabelObstacles.AddRange(placed2.AllLabelRects.Select(ToRectF));

        _cachedRoutedArcs = routed2;
        _cachedPlacedLabels = placed2;

        if (_dragNode is not null) return false;

        for (var i = 0; i < Diagram.Nodes.Count; i++)
        {
            var c = Diagram.Nodes[i].Bounds.Center;
            var x = Pixel((float)c.X);
            var y = Pixel((float)c.Y);
            if (x != beforeX[i] || y != beforeY[i])
                return true;
        }

        return false;
    }

    private void ApplyArcLabelAwareNodeSeparation(PlacedLabels placed)
    {
        if (placed.ArcLabelRects.Count == 0) return;

        // Build obstacles: ARC labels only. Port labels are handled by ApplyPortLabelAwareNodeSpacing().
        var obstacles = placed.ArcLabelRects.Values.Select(ToRectF).ToArray();

        // Quick overlap check: if no node intersects any label (except own-port labels), do nothing.
        var hasAnyOverlap = false;
        foreach (var n in Diagram.Nodes)
        {
            var nodeRect = n.Bounds;
            for (var i = 0; i < obstacles.Length; i++)
            {
                var oRect = obstacles[i];
                if (RectsOverlap(nodeRect, oRect, LabelAwareNodePushPadding))
                {
                    hasAnyOverlap = true;
                    break;
                }
            }
            if (hasAnyOverlap) break;
        }
        if (!hasAnyOverlap) return;

        for (var iter = 0; iter < LabelAwareSeparationIterations; iter++)
        {
            var any = false;
            var deltas = new Vector2[Diagram.Nodes.Count];

            for (var nodeIndex = 0; nodeIndex < Diagram.Nodes.Count; nodeIndex++)
            {
                var n = Diagram.Nodes[nodeIndex];
                var nodeRect = n.Bounds;
                var moved = Vector2.Zero;

                for (var i = 0; i < obstacles.Length; i++)
                {
                    var oRect = obstacles[i];

                    if (!RectsOverlap(nodeRect, oRect, LabelAwareNodePushPadding))
                        continue;

                    var push = ComputeSeparationVector(nodeRect, oRect, LabelAwareNodePushPadding);
                    if (push.LengthSquared() < 0.0001f) continue;

                    moved += push;
                    // Update rect for subsequent obstacles in this iteration.
                    nodeRect = RectF.FromCenter(n.Position + moved, n.Width, n.Height);
                }

                if (moved.LengthSquared() > 0.0001f)
                {
                    any = true;

                    moved *= LabelAwareNodePushDamping;
                    var len = moved.Length();
                    if (len > LabelAwareMaxNodeMovePerUpdate)
                    {
                        moved = moved / MathF.Max(0.0001f, len) * LabelAwareMaxNodeMovePerUpdate;
                    }

                    deltas[nodeIndex] = moved;
                }
            }

            if (!any) break;

            // Subtract mean displacement to avoid global drift (e.g., "everything flew right").
            var sum = Vector2.Zero;
            var count = 0;
            for (var i = 0; i < deltas.Length; i++)
            {
                if (deltas[i].LengthSquared() > 0.0001f)
                {
                    sum += deltas[i];
                    count++;
                }
            }
            var mean = count > 0 ? (sum / count) : Vector2.Zero;

            // If only one node needs to move, subtracting mean would cancel it out.
            if (count <= 1) mean = Vector2.Zero;

            for (var i = 0; i < deltas.Length; i++)
            {
                var d = deltas[i];
                if (d.LengthSquared() < 0.0001f) continue;
                Diagram.Nodes[i].Position += (d - mean);
                Diagram.Nodes[i].Velocity = Vector2.Zero;
            }
        }
    }

    private bool ApplyPortLabelAwareNodeSpacing(PlacedLabels placed)
    {
        // Don't fight user interaction: port label spacing can make drag feel broken.
        if (_dragNode is not null) return false;

        if (placed.PortLabelRects.Count == 0) return false;
        if (Diagram.Nodes.Count < 2) return false;

        // Treat each node's effective obstacle as the union of:
        // - the node rectangle
        // - ALL port label rectangles that belong to that node
        // Then resolve overlaps pairwise by pushing nodes apart.
        // This produces a true “min spacing depends on port label layout”.

        var nodeIndexById = new Dictionary<DiagramId, int>(Diagram.Nodes.Count);
        for (var i = 0; i < Diagram.Nodes.Count; i++)
            nodeIndexById[Diagram.Nodes[i].Id] = i;

        var portById = Diagram.Ports.ToDictionary(p => p.Id, p => p);

        // Cache owned port header data per node. We recompute the actual header rects as a function
        // of the hypothetical node position (pos) to keep behavior correct even if headers are
        // clamped to view bounds.
        var ownedPortHeaders = new List<(PortRef Ref, Vector2 Size)>[Diagram.Nodes.Count];
        for (var i = 0; i < ownedPortHeaders.Length; i++) ownedPortHeaders[i] = new List<(PortRef, Vector2)>();

        foreach (var kv in placed.PortLabelRects)
        {
            if (!portById.TryGetValue(kv.Key, out var port)) continue;
            if (!nodeIndexById.TryGetValue(port.Ref.NodeId, out var nodeIndex)) continue;

            var r = kv.Value;
            var size = new Vector2((float)r.Width, (float)r.Height);
            ownedPortHeaders[nodeIndex].Add((port.Ref, size));
        }

        var fixedIndex = -1;
        if (_dragNode is not null)
        {
            for (var i = 0; i < Diagram.Nodes.Count; i++)
            {
                if (ReferenceEquals(Diagram.Nodes[i], _dragNode))
                {
                    fixedIndex = i;
                    break;
                }
            }
        }

        // Important:
        // - Port headers do NOT move; nodes move.
        // - Other rectangles must not cover foreign port headers.
        // Implement this by treating each node's rectangle expanded by MinNodeSpacing/2 as the
        // "rectangle with offset" area, unioned with its owned port header blocks.
        // This stays local (only resolves real overlaps) and does not re-enforce full min spacing.
        var nodeMargin = MathF.Max(0f, Engine.Settings.MinNodeSpacing) * 0.5f;
        var labelPad = MathF.Max(0f, LabelBackgroundPadding);

        var viewW = (float)Math.Max(0, Bounds.Width);
        var viewH = (float)Math.Max(0, Bounds.Height);

        // Hysteresis to avoid micro-oscillations when rectangles are just touching.
        // Values are in world units (pixels).
        const float overlapEpsilon = 0.20f;
        const float overlapSlop = 0.00f;

        RectF EffectiveObstacle(int nodeIndex, Vector2 pos)
        {
            var n = Diagram.Nodes[nodeIndex];
            var union = Expand(RectF.FromCenter(pos, n.Width, n.Height), nodeMargin);

            var list = ownedPortHeaders[nodeIndex];
            for (var k = 0; k < list.Count; k++)
            {
                var (pref, size) = list[k];
                var nodeAtPos = new RectNode
                {
                    Id = n.Id,
                    Text = n.Text,
                    Position = pos,
                    Velocity = Vector2.Zero,
                    Width = n.Width,
                    Height = n.Height,
                };

                var portPoint = GravityLayoutEngine.GetPortWorldPosition(nodeAtPos, pref);
                var preferredOrigin = GetPortLabelPreferredOrigin(pref.Side, portPoint, size);

                // Match actual draw behavior: clamp into view.
                var maxX = MathF.Max(0f, viewW - size.X);
                var maxY = MathF.Max(0f, viewH - size.Y);
                // NOTE: do not round here; rounding makes the constraint discontinuous and can cause jitter.
                // The label block is expanded by LabelBackgroundPadding anyway, so this is conservative.
                var origin = new Vector2(
                    MathF.Min(MathF.Max(preferredOrigin.X, 0f), maxX),
                    MathF.Min(MathF.Max(preferredOrigin.Y, 0f), maxY));

                var lr = new RectF(
                    origin.X - labelPad,
                    origin.Y - labelPad,
                    size.X + 2 * labelPad,
                    size.Y + 2 * labelPad);
                union = UnionRects(union, lr);
            }
            return union;
        }

        static (float Ox, float Oy) Overlap(in RectF a, in RectF b)
        {
            var ox = MathF.Min(a.Right, b.Right) - MathF.Max(a.Left, b.Left);
            var oy = MathF.Min(a.Bottom, b.Bottom) - MathF.Max(a.Top, b.Top);
            return (ox, oy);
        }

        static Vector2 SeparationVectorExact(in RectF a, in RectF b, float slop, float eps, string tieA, string tieB)
        {
            var (ox, oy) = Overlap(a, b);
            var px = ox - eps;
            var py = oy - eps;
            if (px <= 0f || py <= 0f) return Vector2.Zero;

            var ac = Center(a);
            var bc = Center(b);
            if (px < py)
            {
                var sx = (float)MathF.Sign(ac.X - bc.X);
                if (MathF.Abs(sx) < 0.001f)
                {
                    sx = string.CompareOrdinal(tieA, tieB) < 0 ? -1f : 1f;
                }
                return new Vector2(sx * (px + slop), 0f);
            }
            else
            {
                var sy = (float)MathF.Sign(ac.Y - bc.Y);
                if (MathF.Abs(sy) < 0.001f)
                {
                    sy = string.CompareOrdinal(tieA, tieB) < 0 ? -1f : 1f;
                }
                return new Vector2(0f, sy * (py + slop));
            }
        }

        var currentPos = Diagram.Nodes.Select(n => n.Position).ToArray();

        bool HasAnyOverlap()
        {
            for (var i = 0; i < Diagram.Nodes.Count; i++)
            {
                for (var j = i + 1; j < Diagram.Nodes.Count; j++)
                {
                    var ri = EffectiveObstacle(i, currentPos[i]);
                    var rj = EffectiveObstacle(j, currentPos[j]);
                    var (ox, oy) = Overlap(ri, rj);
                    if (ox > overlapEpsilon && oy > overlapEpsilon) return true;
                }
            }
            return false;
        }

        if (!HasAnyOverlap()) return false;

        const int portHeaderSeparationIterations = 8;
        const float portHeaderPushDamping = 0.35f;
        const float portHeaderMaxMovePerUpdate = 10f;

        for (var iter = 0; iter < portHeaderSeparationIterations; iter++)
        {
            var any = false;
            var deltas = new Vector2[Diagram.Nodes.Count];

            for (var i = 0; i < Diagram.Nodes.Count; i++)
            {
                for (var j = i + 1; j < Diagram.Nodes.Count; j++)
                {
                    var ri = EffectiveObstacle(i, currentPos[i]);
                    var rj = EffectiveObstacle(j, currentPos[j]);

                    var push = SeparationVectorExact(
                        ri,
                        rj,
                        slop: overlapSlop,
                        eps: overlapEpsilon,
                        tieA: Diagram.Nodes[i].Id.Value,
                        tieB: Diagram.Nodes[j].Id.Value);
                    if (push.LengthSquared() < 0.0001f) continue;

                    any = true;

                    var iFixed = i == fixedIndex;
                    var jFixed = j == fixedIndex;

                    if (iFixed && jFixed)
                        continue;

                    if (iFixed)
                    {
                        deltas[j] -= push;
                    }
                    else if (jFixed)
                    {
                        deltas[i] += push;
                    }
                    else
                    {
                        deltas[i] += push * 0.5f;
                        deltas[j] -= push * 0.5f;
                    }
                }
            }

            if (!any) break;

            // Subtract mean displacement to avoid global drift.
            var sum = Vector2.Zero;
            var count = 0;
            for (var i = 0; i < deltas.Length; i++)
            {
                if (deltas[i].LengthSquared() > 0.0001f)
                {
                    sum += deltas[i];
                    count++;
                }
            }
            var mean = count > 0 ? (sum / count) : Vector2.Zero;
            if (count <= 1) mean = Vector2.Zero;

            for (var i = 0; i < Diagram.Nodes.Count; i++)
            {
                var d = deltas[i];
                if (d.LengthSquared() < 0.0001f) continue;

                d = (d - mean) * portHeaderPushDamping;
                var len = d.Length();
                if (len > portHeaderMaxMovePerUpdate)
                {
                    d = d / MathF.Max(0.0001f, len) * portHeaderMaxMovePerUpdate;
                }

                currentPos[i] += d;
            }
        }

        // Apply final positions.
        var movedAny = false;
        for (var i = 0; i < Diagram.Nodes.Count; i++)
        {
            var d = currentPos[i] - Diagram.Nodes[i].Position;
            if (d.LengthSquared() < 0.0001f) continue;
            Diagram.Nodes[i].Position = currentPos[i];
            Diagram.Nodes[i].Velocity = Vector2.Zero;
            movedAny = true;
        }

        return movedAny;
    }

    private void DrawArcs(DrawingContext context, IReadOnlyList<RoutedArc> routed)
    {
        var arcsOrdered = Diagram.Arcs.OrderBy(a => a.Id.Value, StringComparer.Ordinal).ToArray();
        var arcIndexById = arcsOrdered
            .Select((a, i) => (a.Id, i))
            .ToDictionary(x => x.Id, x => x.i);

        foreach (var r in routed)
        {
            arcIndexById.TryGetValue(r.Arc.Id, out var idx);
            var color = RainbowColor(idx);
            var pen = new Pen(new SolidColorBrush(color), 2.2);
            var arrowFill = pen.Brush ?? Brushes.Gray;

            DrawPolyline(context, pen, r.Polyline);
            DrawArrowHead(context, arrowFill, pen, r.Polyline);
        }
    }

    private IReadOnlyList<RoutedArc> ComputeRoutedArcs(IReadOnlyList<RectF> labelObstacles)
    {
        _arcExtraLaneShiftById.Clear();

        var nodesById = Diagram.Nodes.ToDictionary(n => n.Id, n => n);
        var portsById = Diagram.Ports.ToDictionary(p => p.Id, p => p);
        var arcsOrdered = Diagram.Arcs.OrderBy(a => a.Id.Value, StringComparer.Ordinal).ToArray();

        var routed = new List<RoutedArc>(Diagram.Arcs.Count);
        foreach (var arc in arcsOrdered)
        {
            if (!portsById.TryGetValue(arc.FromPortId, out var fromPort)) continue;
            if (!portsById.TryGetValue(arc.ToPortId, out var toPort)) continue;
            if (!nodesById.TryGetValue(fromPort.Ref.NodeId, out var fromNode)) continue;
            if (!nodesById.TryGetValue(toPort.Ref.NodeId, out var toNode)) continue;

            var a = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref);
            var b = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref);

            var poly = new List<Vector2>(2 + arc.InternalPoints.Count)
            {
                a,
            };
            for (var i = 0; i < arc.InternalPoints.Count; i++)
                poly.Add(arc.InternalPoints[i]);
            poly.Add(b);

            // Remove adjacent duplicates (can happen after merges).
            for (var i = poly.Count - 2; i >= 0; i--)
            {
                if (Vector2.DistanceSquared(poly[i], poly[i + 1]) < 0.0001f)
                    poly.RemoveAt(i + 1);
            }

            // Avoid tiny "tails" close to port points (common when internal points merge).
            TrimShortEndpointSegments(poly, minLen: 6f);

            var (labelPos, labelNormal) = GetPolylineMidpointWithNormal(poly);
            routed.Add(new RoutedArc(arc, poly, labelPos, labelNormal));
        }

        return routed;
    }

    private static void TrimShortEndpointSegments(List<Vector2> poly, float minLen)
    {
        if (poly.Count < 3) return;
        var minLenSq = minLen * minLen;

        // Trim near start.
        while (poly.Count >= 3)
        {
            var d = poly[1] - poly[0];
            if (d.LengthSquared() >= minLenSq) break;
            poly.RemoveAt(1);
        }

        // Trim near end.
        while (poly.Count >= 3)
        {
            var last = poly.Count - 1;
            var d = poly[last] - poly[last - 1];
            if (d.LengthSquared() >= minLenSq) break;
            poly.RemoveAt(last - 1);
        }
    }

    private static void DrawArrowHead(DrawingContext context, IBrush fill, Pen outline, List<Vector2> poly)
    {
        if (poly.Count < 2) return;
        var tip = poly[^1];
        // Use the last sufficiently-long segment for direction so arrowhead doesn't look broken
        // when the final into-port segment is extremely short.
        var dir = Vector2.Zero;
        for (var i = poly.Count - 1; i > 0; i--)
        {
            var d = poly[i] - poly[i - 1];
            var len = d.Length();
            if (len >= 6f)
            {
                dir = d / len;
                break;
            }
        }
        if (dir.LengthSquared() < 0.0001f)
        {
            var d = tip - poly[^2];
            var len = d.Length();
            if (len < 0.001f) return;
            dir = d / len;
        }
        var perp = new Vector2(-dir.Y, dir.X);

        const float arrowLen = 12f;
        const float arrowWidth = 8f;

        // Keep arrow size reasonable when the last segment is very short.
        var l = arrowLen;
        var baseCenter = tip - dir * l;
        var left = baseCenter + perp * (arrowWidth * 0.5f);
        var right = baseCenter - perp * (arrowWidth * 0.5f);

        var geom = new StreamGeometry();
        using (var g = geom.Open())
        {
            g.BeginFigure(new Point(tip.X, tip.Y), isFilled: true);
            g.LineTo(new Point(left.X, left.Y));
            g.LineTo(new Point(right.X, right.Y));
            g.EndFigure(isClosed: true);
        }
        context.DrawGeometry(fill, outline, geom);
    }

    private static void DrawPolyline(DrawingContext context, Pen pen, List<Vector2> points)
    {
        const float portDotRadius = 4f;
        for (var i = 0; i + 1 < points.Count; i++)
        {
            var a = points[i];
            var b = points[i + 1];

            // Trim at endpoints so the line doesn't visibly "stick out" of the port dot.
            if (i == 0)
            {
                var d = b - a;
                var len = d.Length();
                if (len > 0.001f)
                {
                    var trim = MathF.Min(portDotRadius, len * 0.49f);
                    a += d / len * trim;
                }
            }
            if (i + 2 == points.Count)
            {
                var d = b - a;
                var len = d.Length();
                if (len > 0.001f)
                {
                    var trim = MathF.Min(portDotRadius, len * 0.49f);
                    b -= d / len * trim;
                }
            }

            context.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));
        }
    }

    private static List<Vector2> RouteArcPolyline(
        Vector2 from,
        RectSide fromSide,
        Vector2 to,
        RectSide toSide,
        float laneOffset,
        float outDistance,
        float nodeClearance,
        System.Collections.ObjectModel.ReadOnlyCollection<RectNode> allNodes,
        RectNode fromNode,
        RectNode toNode,
        float lengthWeight,
        float bendWeight,
        IReadOnlyList<RectF> labelObstacles)
    {
        var outA = from + ArcRoutingGeometry.SideDir(fromSide) * outDistance;
        var outB = to + ArcRoutingGeometry.SideDir(toSide) * outDistance;

        var cand1 = BuildOrthogonalCandidate(from, outA, outB, to, preferHorizontalFirst: true, laneOffset);
        var cand2 = BuildOrthogonalCandidate(from, outA, outB, to, preferHorizontalFirst: false, laneOffset);

        var score1 = ScoreCandidate(cand1, allNodes, fromNode, toNode, nodeClearance, lengthWeight, bendWeight, labelObstacles);
        var score2 = ScoreCandidate(cand2, allNodes, fromNode, toNode, nodeClearance, lengthWeight, bendWeight, labelObstacles);
        return score2 < score1 ? cand2 : cand1;
    }

    private static float ScoreCandidate(
        List<Vector2> poly,
        System.Collections.ObjectModel.ReadOnlyCollection<RectNode> nodes,
        RectNode fromNode,
        RectNode toNode,
        float nodeClearance,
        float lengthWeight,
        float bendWeight,
        IReadOnlyList<RectF> labelObstacles)
    {
        var score = 0f;
        for (var i = 0; i + 1 < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[i + 1];

            // Only the first (out of port) and last (into port) segments are allowed to be close to
            // their respective endpoint nodes. All other segments must keep clearance from ALL nodes.
            var isFirst = i == 0;
            var isLast = i + 1 == poly.Count - 1;

            foreach (var n in nodes)
            {
                if (isFirst && ReferenceEquals(n, fromNode))
                    continue;
                if (isLast && ReferenceEquals(n, toNode))
                    continue;

                var r = Expand(n.Bounds, MathF.Max(ArcNodeClearance, nodeClearance));
                if (AxisAlignedSegmentIntersectsRect(a, b, r))
                {
                    // Clearance violations must dominate the score: this is a hard-ish routing requirement.
                    score += 10_000f;
                }
            }

            // Labels are obstacles too: avoid running through text.
            for (var oi = 0; oi < labelObstacles.Count; oi++)
            {
                var rr = Expand(labelObstacles[oi], LabelArcClearance);
                if (AxisAlignedSegmentIntersectsRect(a, b, rr))
                {
                    score += 30f;
                }
            }
        }

        // Prefer shorter routes when crossings are comparable.
        score += PolylineLength(poly) * lengthWeight;

        // Prefer fewer bends (straighter polyline).
        var bends = Math.Max(0, poly.Count - 2);
        score += bends * bendWeight;
        return score;
    }

    private static float ScorePolyline(
        List<Vector2> poly,
        System.Collections.ObjectModel.ReadOnlyCollection<RectNode> nodes,
        RectNode fromNode,
        RectNode toNode,
        float nodeClearance,
        List<List<Vector2>> alreadyRouted,
        float lengthWeight,
        float bendWeight,
        IReadOnlyList<RectF> labelObstacles)
    {
        var score = ScoreCandidate(poly, nodes, fromNode, toNode, nodeClearance, lengthWeight, bendWeight, labelObstacles);

        // Strongly prefer no overlaps with existing routes.
        for (var i = 0; i < alreadyRouted.Count; i++)
        {
            if (HasCollinearOverlap(alreadyRouted[i], poly, ArcOverlapClearance))
            {
                score += 1000f;
            }
        }

        return score;
    }

    private sealed record RoutedArc(Arc Arc, List<Vector2> Polyline, Vector2 LabelBasePoint, Vector2 LabelNormal);

    private sealed record PlacedLabels(
        IReadOnlyDictionary<DiagramId, Rect> PortLabelRects,
        IReadOnlyDictionary<DiagramId, Rect> ArcLabelRects,
        IReadOnlyList<Rect> AllLabelRects);

    private enum SidePortLabelVerticalAlign
    {
        Bottom,
        Center,
        Top,
    }

    private enum BottomPortLabelHorizontalAlign
    {
        Right,
        Center,
        Left,
    }

    // Default placement rules:
    // - Side ports (Left/Right): label is outside horizontally, aligned vertically by this rule.
    // - Bottom ports: label is outside vertically (below), aligned horizontally by this rule.
    private const SidePortLabelVerticalAlign DefaultSidePortLabelVAlign = SidePortLabelVerticalAlign.Bottom;
    private const BottomPortLabelHorizontalAlign DefaultBottomPortLabelHAlign = BottomPortLabelHorizontalAlign.Right;

    private PlacedLabels PlaceAllLabels(IReadOnlyList<RoutedArc> routed)
    {
        var viewW = (float)Math.Max(0, Bounds.Width);
        var viewH = (float)Math.Max(0, Bounds.Height);

        var nodeObstacles = Diagram.Nodes
            .Select(n => Expand(n.Bounds, LabelNodeClearance))
            .ToArray();

        var polylines = routed.Select(r => r.Polyline).ToList();

        // 1) Place PORT labels deterministically and FIXED (no solver, no collision-based movement).
        // Port headers must not drift; nodes will be moved to avoid covering foreign headers.
        var portsOrdered = Diagram.Ports.OrderBy(p => p.Id.Value, StringComparer.Ordinal).ToArray();
        var placedPortRects = new Dictionary<DiagramId, Rect>(portsOrdered.Length);
        var placedPortRectsList = new List<RectF>(portsOrdered.Length);

        foreach (var port in portsOrdered)
        {
            if (string.IsNullOrWhiteSpace(port.Text)) continue;
            var node = Diagram.Nodes.FirstOrDefault(n => n.Id == port.Ref.NodeId);
            if (node is null) continue;

            var p = GravityLayoutEngine.GetPortWorldPosition(node, port.Ref);
            var size = MeasureLabel(port.Text, LabelFontSize, LabelMaxWidth);
            var preferred = GetPortLabelPreferredOrigin(port.Ref.Side, p, size);

            var maxX = MathF.Max(0f, viewW - size.X);
            var maxY = MathF.Max(0f, viewH - size.Y);

            // IMPORTANT: do not round origins here.
            // Rounding creates discontinuities (1px jumps) that can feed into the
            // port-header-aware node spacing pass and prevent pixel-stable auto-stop.
            var origin = new Vector2(
                MathF.Min(MathF.Max(preferred.X, 0f), maxX),
                MathF.Min(MathF.Max(preferred.Y, 0f), maxY));

            var rect = new Rect(origin.X, origin.Y, size.X, size.Y);
            placedPortRects[port.Id] = rect;
            placedPortRectsList.Add(ToRectF(rect));
        }

        // 2) Place ARC labels via iterative solver, avoiding nodes + already placed port labels.
        var arcCandidates = new List<LabelCandidate>(Diagram.Arcs.Count);
        foreach (var r in routed)
        {
            if (string.IsNullOrWhiteSpace(r.Arc.Text)) continue;
            var size = MeasureLabel(r.Arc.Text, LabelFontSize, LabelMaxWidth);
            var preferred = GetArcLabelPreferredOrigin(r.LabelBasePoint, r.LabelNormal, size);
            _arcLabelOriginById.TryGetValue(r.Arc.Id, out var cached);
            var start = cached == Vector2.Zero ? preferred : cached;
            arcCandidates.Add(new LabelCandidate(LabelKind.Arc, r.Arc.Id, r.Arc.Text, start, preferred, size));
        }

        SolveLabelPlacement(arcCandidates, nodeObstacles, polylines, placedPortRectsList.ToArray());

        var arcRects = new Dictionary<DiagramId, Rect>(arcCandidates.Count);
        var allRects = new List<Rect>(placedPortRects.Count + arcCandidates.Count);

        foreach (var kv in placedPortRects)
            allRects.Add(kv.Value);

        for (var i = 0; i < arcCandidates.Count; i++)
        {
            var c = arcCandidates[i];
            var rect = new Rect(c.Origin.X, c.Origin.Y, c.Size.X, c.Size.Y);
            arcRects[c.Id] = rect;
            allRects.Add(rect);
            _arcLabelOriginById[c.Id] = c.Origin;
        }

        return new PlacedLabels(placedPortRects, arcRects, allRects);
    }

    private void DrawLabels(DrawingContext context, PlacedLabels placed)
    {
        var portTextBrush = Brushes.DimGray;
        var arcTextBrush = Brushes.DarkSlateGray;

        foreach (var port in Diagram.Ports)
        {
            if (string.IsNullOrWhiteSpace(port.Text)) continue;
            if (!placed.PortLabelRects.TryGetValue(port.Id, out var r)) continue;
            DrawLabelWithBackground(context, port.Text, r, portTextBrush, LabelFontSize);
        }

        foreach (var arc in Diagram.Arcs)
        {
            if (string.IsNullOrWhiteSpace(arc.Text)) continue;
            if (!placed.ArcLabelRects.TryGetValue(arc.Id, out var r)) continue;
            DrawLabelWithBackground(context, arc.Text, r, arcTextBrush, LabelFontSize);
        }
    }

    private enum LabelKind { Port, Arc }

    private sealed record LabelCandidate(
        LabelKind Kind,
        DiagramId Id,
        string Text,
        Vector2 Origin,
        Vector2 PreferredOrigin,
        Vector2 Size);

    private void SolveLabelPlacement(
        List<LabelCandidate> labels,
        RectF[] nodeObstacles,
        List<List<Vector2>> arcPolylines,
        RectF[] fixedObstacles)
    {
        if (labels.Count == 0) return;

        var viewW = (float)Math.Max(0, Bounds.Width);
        var viewH = (float)Math.Max(0, Bounds.Height);

        static RectF RectOf(LabelCandidate l)
            => new(l.Origin.X, l.Origin.Y, l.Size.X, l.Size.Y);

        static float Pad(LabelKind kind)
            => (kind == LabelKind.Port ? PortLabelPadding : ArcLabelPadding) + LabelBackgroundPadding;

        for (var iter = 0; iter < LabelPlacementIterations; iter++)
        {
            // 1) Gentle spring to preferred positions.
            for (var i = 0; i < labels.Count; i++)
            {
                var l = labels[i];
                var toPref = l.PreferredOrigin - l.Origin;
                var k = l.Kind == LabelKind.Port ? PortLabelSpringK : ArcLabelSpringK;
                l = l with { Origin = l.Origin + toPref * k };

                // Clamp drift so port labels stay close to their ports.
                var maxDrift = l.Kind == LabelKind.Port ? PortLabelMaxDrift : ArcLabelMaxDrift;
                var drift = l.Origin - l.PreferredOrigin;
                var driftLen = drift.Length();
                if (driftLen > maxDrift)
                {
                    l = l with { Origin = l.PreferredOrigin + drift / MathF.Max(0.0001f, driftLen) * maxDrift };
                }
                labels[i] = l;
            }

            // 2) Label-label separation.
            for (var i = 0; i < labels.Count; i++)
            {
                for (var j = i + 1; j < labels.Count; j++)
                {
                    var a = RectOf(labels[i]);
                    var b = RectOf(labels[j]);
                    var pad = MathF.Max(Pad(labels[i].Kind), Pad(labels[j].Kind));
                    if (!RectsOverlap(a, b, pad)) continue;

                    var push = ComputeSeparationVector(a, b, pad);
                    var li = labels[i];
                    var lj = labels[j];
                    var mobI = li.Kind == LabelKind.Port ? 0.25f : 1.0f;
                    var mobJ = lj.Kind == LabelKind.Port ? 0.25f : 1.0f;
                    var sum = MathF.Max(0.0001f, mobI + mobJ);
                    li = li with { Origin = li.Origin + push * (mobI / sum) };
                    lj = lj with { Origin = lj.Origin - push * (mobJ / sum) };
                    labels[i] = li;
                    labels[j] = lj;
                }
            }

            // 3) Avoid nodes.
            for (var i = 0; i < labels.Count; i++)
            {
                var l = labels[i];
                var lr = RectOf(l);
                var pad = Pad(l.Kind);
                for (var ni = 0; ni < nodeObstacles.Length; ni++)
                {
                    var nr = nodeObstacles[ni];
                    if (!RectsOverlap(lr, nr, pad)) continue;
                    var push = ComputeSeparationVector(lr, nr, pad);
                    var mob = l.Kind == LabelKind.Port ? 0.25f : 1.0f;
                    l = l with { Origin = l.Origin + push * mob };
                    lr = RectOf(l);
                }

                // Also avoid fixed obstacles (e.g., placed port labels).
                for (var oi = 0; oi < fixedObstacles.Length; oi++)
                {
                    var or = fixedObstacles[oi];
                    if (!RectsOverlap(lr, or, pad)) continue;
                    var push = ComputeSeparationVector(lr, or, pad);
                    var mob = l.Kind == LabelKind.Port ? 0.25f : 1.0f;
                    l = l with { Origin = l.Origin + push * mob };
                    lr = RectOf(l);
                }
                labels[i] = l;
            }

            // 4) Avoid arc segments.
            for (var i = 0; i < labels.Count; i++)
            {
                var l = labels[i];
                var rr = Expand(RectOf(l), LabelArcClearance);
                for (var pi = 0; pi < arcPolylines.Count; pi++)
                {
                    var poly = arcPolylines[pi];
                    for (var si = 0; si + 1 < poly.Count; si++)
                    {
                        var a = poly[si];
                        var b = poly[si + 1];
                        if (!AxisAlignedSegmentIntersectsRect(a, b, rr)) continue;

                        var c = Center(rr);
                        // Port labels should be stable; arcs must route around them.
                        // Only ARC labels are pushed away from arc polylines.
                        var mob = l.Kind == LabelKind.Port ? 0.0f : 1.0f;
                        if (mob <= 0f)
                        {
                            continue;
                        }
                        if (MathF.Abs(a.X - b.X) < 0.001f)
                        {
                            // Vertical segment: push horizontally.
                            var dir = c.X >= a.X ? 1f : -1f;
                            l = l with { Origin = l.Origin + new Vector2(dir * 3.5f * mob, 0f) };
                        }
                        else
                        {
                            // Horizontal segment: push vertically.
                            var dir = c.Y >= a.Y ? 1f : -1f;
                            l = l with { Origin = l.Origin + new Vector2(0f, dir * 3.5f * mob) };
                        }

                        rr = Expand(RectOf(l), LabelArcClearance);
                    }
                }
                labels[i] = l;
            }

            // 5) Clamp into view.
            for (var i = 0; i < labels.Count; i++)
            {
                var l = labels[i];
                var maxX = Math.Max(0f, viewW - l.Size.X);
                var maxY = Math.Max(0f, viewH - l.Size.Y);
                l = l with
                {
                    Origin = new Vector2(
                        Math.Clamp(l.Origin.X, 0f, maxX),
                        Math.Clamp(l.Origin.Y, 0f, maxY))
                };
                labels[i] = l;
            }
        }
    }

    private Vector2 ChoosePortLabelOrigin(
        RectSide side,
        Vector2 preferred,
        Vector2 cached,
        Vector2 size,
        DiagramId ownerNodeId,
        List<RectF> alreadyPlacedPortRects,
        RectF[] nodeObstacles,
        List<List<Vector2>> arcPolylines,
        float viewW,
        float viewH)
    {
        var maxX = Math.Max(0f, viewW - size.X);
        var maxY = Math.Max(0f, viewH - size.Y);

        Vector2 ClampToView(Vector2 o)
            => new(
                Math.Clamp(o.X, 0f, maxX),
                Math.Clamp(o.Y, 0f, maxY));

        Vector2 Snap(Vector2 o)
            => new(MathF.Round(o.X), MathF.Round(o.Y));

        RectF RectAt(Vector2 o)
            => new(o.X, o.Y, size.X, size.Y);

        var portPad = PortLabelPadding + LabelBackgroundPadding;

        bool IsAcceptable(Vector2 origin)
        {
            var r = RectAt(origin);

            for (var i = 0; i < alreadyPlacedPortRects.Count; i++)
            {
                if (RectsOverlap(r, alreadyPlacedPortRects[i], portPad))
                    return false;
            }

            // Don't overlap nodes (including owner).
            for (var ni = 0; ni < Diagram.Nodes.Count; ni++)
            {
                var n = Diagram.Nodes[ni];
                // Owner node: allow being very close to the border (no extra padding), otherwise
                // tiny rounding changes can flip acceptability and cause jitter.
                var nr = (n.Id == ownerNodeId)
                    ? n.Bounds
                    : Expand(n.Bounds, portPad);

                if (RectsOverlap(r, nr, 0f))
                    return false;
            }

            return true;
        }

        float Score(Vector2 origin)
        {
            var r = RectAt(origin);
            var score = 0f;

            // 1) Avoid other port labels strongly.
            for (var i = 0; i < alreadyPlacedPortRects.Count; i++)
            {
                if (!RectsOverlap(r, alreadyPlacedPortRects[i], portPad)) continue;
                score += 200f;
            }

            // 2) Avoid nodes (other nodes strongly; own node weakly).
            for (var ni = 0; ni < Diagram.Nodes.Count; ni++)
            {
                var n = Diagram.Nodes[ni];
                var nr = Expand(n.Bounds, portPad);
                if (!RectsOverlap(r, nr, 0f)) continue;
                score += (n.Id == ownerNodeId) ? 120f : 200f;
            }

            // 3) Prefer staying near the preferred origin.
            var drift = (origin - preferred).Length();
            score += drift * 0.05f;

            return score;
        }

        // Generate discrete candidates around preferred.
        var step = 6f;
        var outward = side switch
        {
            RectSide.Left => new Vector2(-1f, 0f),
            RectSide.Right => new Vector2(1f, 0f),
            RectSide.Top => new Vector2(0f, -1f),
            RectSide.Bottom => new Vector2(0f, 1f),
            _ => Vector2.Zero
        };
        var tangent = (MathF.Abs(outward.X) > 0.5f)
            ? new Vector2(0f, 1f)
            : new Vector2(1f, 0f);

        var candidates = new List<Vector2>(24)
        {
            preferred,
            preferred + outward * step,
            preferred + outward * (2 * step),
        };

        // Move mostly along tangent (for Left/Right vary Y; for Top/Bottom vary X).
        candidates.Add(preferred + tangent * step);
        candidates.Add(preferred + tangent * (-step));
        candidates.Add(preferred + tangent * (2 * step));
        candidates.Add(preferred + tangent * (-2 * step));

        // Small diagonals (tangent + outward) to escape tight corners.
        candidates.Add(preferred + outward * step + tangent * step);
        candidates.Add(preferred + outward * step + tangent * (-step));
        candidates.Add(preferred + outward * (2 * step) + tangent * step);
        candidates.Add(preferred + outward * (2 * step) + tangent * (-step));
        candidates.Add(preferred + outward * (-step) + tangent * step);
        candidates.Add(preferred + outward * (-step) + tangent * (-step));

        Vector2 ClampDrift(Vector2 o, float driftLimit)
        {
            var drift = o - preferred;
            var driftLen = drift.Length();
            if (driftLen <= driftLimit) return o;
            return preferred + drift / MathF.Max(0.0001f, driftLen) * driftLimit;
        }

        Vector2 PickBest(float driftLimit)
        {
            // Prefer any collision-free candidate; otherwise, fall back to best scored.
            var bestAny = Snap(ClampToView(ClampDrift(preferred, driftLimit)));
            var bestAnyScore = Score(bestAny);

            var bestOk = bestAny;
            var bestOkScore = float.PositiveInfinity;
            var hasOk = false;

            // Keep cached position if it's collision-free.
            if (cached != Vector2.Zero)
            {
                var cur = Snap(ClampToView(ClampDrift(cached, driftLimit)));
                if (IsAcceptable(cur))
                {
                    return cur;
                }
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var cand = Snap(ClampToView(ClampDrift(candidates[i], driftLimit)));
                var s = Score(cand);
                if (s < bestAnyScore)
                {
                    bestAnyScore = s;
                    bestAny = cand;
                }

                if (!IsAcceptable(cand))
                    continue;

                if (!hasOk || s < bestOkScore)
                {
                    hasOk = true;
                    bestOkScore = s;
                    bestOk = cand;
                }
            }

            return hasOk ? bestOk : bestAny;
        }

        // Try within tight drift first; if impossible, allow more drift to actually resolve collisions.
        var best = PickBest(PortLabelMaxDrift);
        if (IsAcceptable(best))
            return best;

        var bestWide = PickBest(MathF.Max(PortLabelMaxDrift, 60f));
        return bestWide;
    }

    private static RectF UnionRects(RectF a, RectF b)
    {
        var left = MathF.Min(a.Left, b.Left);
        var top = MathF.Min(a.Top, b.Top);
        var right = MathF.Max(a.Right, b.Right);
        var bottom = MathF.Max(a.Bottom, b.Bottom);
        return new RectF(left, top, right - left, bottom - top);
    }

    private static Vector2 MeasureLabel(string text, float fontSize, float maxWidth)
    {
        var ft = CreateFormattedText(text, Brushes.Black, fontSize, maxWidth, TextAlignment.Left);
        return new Vector2((float)Math.Ceiling(ft.Width), (float)Math.Ceiling(ft.Height));
    }

    private static Vector2 GetPortLabelPreferredOrigin(RectSide side, Vector2 portPoint, Vector2 size)
    {
        const float dist = 6f;
        const float extraDown = 6f;

        static float AlignY(Vector2 portPoint, Vector2 size, SidePortLabelVerticalAlign align)
            => align switch
            {
                SidePortLabelVerticalAlign.Top => portPoint.Y - size.Y,
                SidePortLabelVerticalAlign.Center => portPoint.Y - size.Y * 0.5f,
                SidePortLabelVerticalAlign.Bottom => portPoint.Y,
                _ => portPoint.Y - size.Y * 0.5f,
            };

        static float AlignX(Vector2 portPoint, Vector2 size, BottomPortLabelHorizontalAlign align)
            => align switch
            {
                BottomPortLabelHorizontalAlign.Left => portPoint.X - size.X,
                BottomPortLabelHorizontalAlign.Center => portPoint.X - size.X * 0.5f,
                BottomPortLabelHorizontalAlign.Right => portPoint.X,
                _ => portPoint.X - size.X * 0.5f,
            };

        // Requested defaults:
        // - Side ports: Bottom
        // - Bottom ports: Right
        var origin = side switch
        {
            RectSide.Left => new Vector2(
                portPoint.X - dist - size.X,
                AlignY(portPoint, size, DefaultSidePortLabelVAlign)),
            RectSide.Right => new Vector2(
                portPoint.X + dist,
                AlignY(portPoint, size, DefaultSidePortLabelVAlign)),
            RectSide.Bottom => new Vector2(
                AlignX(portPoint, size, DefaultBottomPortLabelHAlign),
                portPoint.Y + dist),
            RectSide.Top => new Vector2(
                portPoint.X - size.X * 0.5f,
                portPoint.Y - dist - size.Y),
            _ => new Vector2(portPoint.X, portPoint.Y),
        };

        // Visual tweak: keep port text slightly lower.
        origin.Y += extraDown;
        return origin;
    }

    private static Vector2 GetArcLabelPreferredOrigin(Vector2 mid, Vector2 normal, Vector2 size)
    {
        var basePoint = mid + normal * ArcLabelDistanceFromArc;
        var origin = basePoint;

        if (normal.Y < -0.5f) origin.Y -= size.Y;
        if (normal.Y > 0.5f) { /* below */ }
        if (normal.X < -0.5f) origin.X -= size.X;
        if (normal.X > 0.5f) { /* right */ }

        // If normal is purely vertical (common), center horizontally.
        if (MathF.Abs(normal.X) < 0.001f)
        {
            origin.X -= size.X * 0.5f;
        }
        // If normal is purely horizontal, center vertically.
        if (MathF.Abs(normal.Y) < 0.001f)
        {
            origin.Y -= size.Y * 0.5f;
        }

        return origin;
    }

    private static bool RectsOverlap(in RectF a, in RectF b, float padding)
    {
        var aa = Expand(a, padding);
        var bb = Expand(b, padding);
        return !(aa.Right <= bb.Left || aa.Left >= bb.Right || aa.Bottom <= bb.Top || aa.Top >= bb.Bottom);
    }

    private static Vector2 ComputeSeparationVector(in RectF a, in RectF b, float padding)
    {
        var aa = Expand(a, padding);
        var bb = Expand(b, padding);

        var overlapX = MathF.Min(aa.Right, bb.Right) - MathF.Max(aa.Left, bb.Left);
        var overlapY = MathF.Min(aa.Bottom, bb.Bottom) - MathF.Max(aa.Top, bb.Top);
        if (overlapX <= 0 || overlapY <= 0) return Vector2.Zero;

        var ac = Center(aa);
        var bc = Center(bb);
        if (overlapX < overlapY)
        {
            var dir = ac.X >= bc.X ? 1f : -1f;
            return new Vector2(dir * (overlapX + 0.5f), 0f);
        }
        else
        {
            var dir = ac.Y >= bc.Y ? 1f : -1f;
            return new Vector2(0f, dir * (overlapY + 0.5f));
        }
    }

    private static Vector2 Center(in RectF r) => new(r.X + r.Width * 0.5f, r.Y + r.Height * 0.5f);

    private static RectF ToRectF(Rect r) => new((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height);

    private static void DrawLabelWithBackground(DrawingContext context, string text, Rect rect, IBrush brush, double fontSize)
    {
        var bg = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
        var outline = new Pen(new SolidColorBrush(Color.FromArgb(120, 200, 200, 200)), 1);
        var pad = LabelBackgroundPadding;
        var rr = new Rect(rect.X - pad, rect.Y - pad, rect.Width + pad * 2, rect.Height + pad * 2);
        context.DrawRectangle(bg, outline, rr, 4);

        var ft = CreateFormattedText(text, brush, fontSize, rect.Width, TextAlignment.Left);
        context.DrawText(ft, rect.TopLeft);
    }

    private static float PolylineLength(List<Vector2> poly)
    {
        var len = 0f;
        for (var i = 0; i + 1 < poly.Count; i++)
        {
            len += Vector2.Distance(poly[i], poly[i + 1]);
        }
        return len;
    }

    private static List<Vector2> BuildOrthogonalCandidate(
        Vector2 start,
        Vector2 startOut,
        Vector2 endOut,
        Vector2 end,
        bool preferHorizontalFirst,
        float laneOffset)
    {
        var main = endOut - startOut;
        var mainIsHorizontal = MathF.Abs(main.X) >= MathF.Abs(main.Y);
        var laneShift = mainIsHorizontal ? new Vector2(0, laneOffset) : new Vector2(laneOffset, 0);
        laneShift = ArcRoutingGeometry.ClampLaneShiftAgainstExit(start, startOut, end, endOut, laneShift);

        Vector2 mid;
        if (preferHorizontalFirst)
        {
            mid = new Vector2(endOut.X, startOut.Y);
        }
        else
        {
            mid = new Vector2(startOut.X, endOut.Y);
        }

        // Keep all segments axis-aligned:
        // start -> startOut (out of port)
        // startOut -> startOut+laneShift (perpendicular shift)
        // ... route in the shifted "lane" ...
        // endOut+laneShift -> endOut (shift back)
        // endOut -> end (into port)
        var pts = new List<Vector2>(8)
        {
            start,
            startOut,
            startOut + laneShift,
            mid + laneShift,
            endOut + laneShift,
            endOut,
            end,
        };

        return SimplifyCollinear(pts);
    }

    private static List<Vector2> SimplifyCollinear(List<Vector2> pts)
    {
        static bool IsCollinear(Vector2 a, Vector2 b, Vector2 c)
        {
            // For axis-aligned routing, collinear means same X or same Y.
            return (MathF.Abs(a.X - b.X) < 0.001f && MathF.Abs(b.X - c.X) < 0.001f)
                || (MathF.Abs(a.Y - b.Y) < 0.001f && MathF.Abs(b.Y - c.Y) < 0.001f);
        }

        // Preserve the explicit out-of-port and into-port points (index 1 and Count-2).
        // They define the minimum perpendicular exit/enter distance.
        for (var i = pts.Count - 2; i > 0; i--)
        {
            if (i == 1 || i == pts.Count - 2) continue;
            if (IsCollinear(pts[i - 1], pts[i], pts[i + 1]))
            {
                pts.RemoveAt(i);
            }
        }

        // Remove duplicates that can appear when endpoints align.
        for (var i = pts.Count - 2; i >= 0; i--)
        {
            if (Vector2.DistanceSquared(pts[i], pts[i + 1]) < 0.0001f)
            {
                pts.RemoveAt(i + 1);
            }
        }

        return pts;
    }

    private static RectF Expand(in RectF r, float margin)
        => new(r.X - margin, r.Y - margin, r.Width + 2 * margin, r.Height + 2 * margin);

    private static bool AxisAlignedSegmentIntersectsRect(Vector2 a, Vector2 b, RectF r)
    {
        return ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, r);
    }

    private static bool HasCollinearOverlap(List<Vector2> a, List<Vector2> b, float clearance)
    {
        for (var i = 0; i + 1 < a.Count; i++)
        {
            var a0 = a[i];
            var a1 = a[i + 1];
            for (var j = 0; j + 1 < b.Count; j++)
            {
                var b0 = b[j];
                var b1 = b[j + 1];
                if (SegmentsCollinearOverlap(a0, a1, b0, b1, clearance))
                    return true;
            }
        }
        return false;
    }

    private static bool SegmentsCollinearOverlap(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, float clearance)
    {
        var aVertical = MathF.Abs(a0.X - a1.X) < 0.001f;
        var aHorizontal = MathF.Abs(a0.Y - a1.Y) < 0.001f;
        var bVertical = MathF.Abs(b0.X - b1.X) < 0.001f;
        var bHorizontal = MathF.Abs(b0.Y - b1.Y) < 0.001f;

        if (aHorizontal && bHorizontal)
        {
            if (MathF.Abs(a0.Y - b0.Y) > clearance) return false;
            var ax0 = MathF.Min(a0.X, a1.X);
            var ax1 = MathF.Max(a0.X, a1.X);
            var bx0 = MathF.Min(b0.X, b1.X);
            var bx1 = MathF.Max(b0.X, b1.X);
            return MathF.Min(ax1, bx1) - MathF.Max(ax0, bx0) > clearance;
        }

        if (aVertical && bVertical)
        {
            if (MathF.Abs(a0.X - b0.X) > clearance) return false;
            var ay0 = MathF.Min(a0.Y, a1.Y);
            var ay1 = MathF.Max(a0.Y, a1.Y);
            var by0 = MathF.Min(b0.Y, b1.Y);
            var by1 = MathF.Max(b0.Y, b1.Y);
            return MathF.Min(ay1, by1) - MathF.Max(ay0, by0) > clearance;
        }

        return false;
    }

    private static (Vector2 point, Vector2 normal) GetPolylineMidpointWithNormal(List<Vector2> poly)
    {
        var total = 0f;
        for (var i = 0; i + 1 < poly.Count; i++)
        {
            total += Vector2.Distance(poly[i], poly[i + 1]);
        }

        if (total < 0.001f)
        {
            return (poly.Count > 0 ? poly[0] : Vector2.Zero, new Vector2(0, -1));
        }

        var half = total / 2f;
        var acc = 0f;
        for (var i = 0; i + 1 < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[i + 1];
            var seg = Vector2.Distance(a, b);
            if (acc + seg >= half)
            {
                var t = (half - acc) / MathF.Max(0.0001f, seg);
                var p = Vector2.Lerp(a, b, t);
                var d = b - a;
                Vector2 n;
                if (MathF.Abs(d.X) >= MathF.Abs(d.Y))
                {
                    n = new Vector2(0, -1);
                }
                else
                {
                    n = new Vector2(1, 0);
                }
                return (p, n);
            }
            acc += seg;
        }

        return (poly[^1], new Vector2(0, -1));
    }

    private static Point GetOutsideTextAnchor(RectSide side, Point portPoint, double distance)
    {
        return side switch
        {
            RectSide.Top => new Point(portPoint.X, portPoint.Y - distance - 12),
            RectSide.Bottom => new Point(portPoint.X, portPoint.Y + distance),
            RectSide.Left => new Point(portPoint.X - distance - 30, portPoint.Y - 8),
            RectSide.Right => new Point(portPoint.X + distance, portPoint.Y - 8),
            _ => portPoint,
        };
    }

    private static void DrawCenteredLabel(DrawingContext context, string text, Rect rect, IBrush brush, double fontSize)
    {
        DrawRectLabel(context, text, rect, brush, fontSize, RectTextHAlign.Center, RectTextVAlign.Center);
    }

    private static void DrawRectLabel(
        DrawingContext context,
        string text,
        Rect rect,
        IBrush brush,
        double fontSize,
        RectTextHAlign hAlign,
        RectTextVAlign vAlign)
    {
        var maxWidth = Math.Max(0, rect.Width - 2 * NodeTextPadding);

        var textAlignment = hAlign switch
        {
            RectTextHAlign.Left => TextAlignment.Left,
            RectTextHAlign.Center => TextAlignment.Center,
            RectTextHAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Center,
        };

        var ft = CreateFormattedText(text, brush, fontSize, maxWidth, textAlignment);

        var x = rect.X + NodeTextPadding;
        var y = vAlign switch
        {
            RectTextVAlign.Top => rect.Y + NodeTextPadding,
            RectTextVAlign.Center => rect.Y + (rect.Height - ft.Height) / 2.0,
            RectTextVAlign.Bottom => rect.Bottom - NodeTextPadding - ft.Height,
            _ => rect.Y + (rect.Height - ft.Height) / 2.0,
        };

        // Keep origin in-bounds (avoid tiny negative values due to rounding).
        y = Math.Max(rect.Y + NodeTextPadding, y);
        context.DrawText(ft, new Point(x, y));
    }

    private static void DrawLabel(DrawingContext context, string text, Point origin, IBrush brush, double fontSize)
    {
        var ft = CreateFormattedText(text, brush, fontSize, 800, TextAlignment.Left);
        context.DrawText(ft, origin);
    }

    private static FormattedText CreateFormattedText(string text, IBrush brush, double fontSize, double maxWidth, TextAlignment alignment)
    {
        var ft = new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            fontSize,
            brush);

        ft.MaxTextWidth = Math.Max(0, maxWidth);
        ft.TextAlignment = alignment;
        return ft;
    }
}
