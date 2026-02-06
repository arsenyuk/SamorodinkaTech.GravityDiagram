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
    private const int WarmStartMaxSteps = 2200;
    private const int WarmStartCheckEvery = 20;
    private const float WarmStartStopSpeed = 18f;
    private const float WarmStartDt = 1f / 60f;

    // Run the simulation faster than real-time so changes are visible.
    // Substep so each physics step remains stable.
    private const float LiveSimSpeed = 60f;
    private const float LiveMaxSubstepDt = 1f / 60f;
    private const int LiveMaxSubstepsPerTick = 240;

    private readonly DispatcherTimer _timer;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastTickAt;

    // Keeps arc routes stable between frames (prevents “blinking” when two candidates have similar scores).
    private readonly Dictionary<DiagramId, float> _arcExtraLaneShiftById = new();

    private RectNode? _dragNode;
    private Vector2 _dragOffset;

    public DiagramView()
    {
        ClipToBounds = true;
        Diagram = SampleDiagram.Create();
        Engine = new GravityLayoutEngine(new LayoutSettings
        {
            // Moderate attraction for compact, but not "glued" layout.
                // Mutual attraction between rectangles (keep small by default).
                BackgroundPairGravity = 0.06f,
            EdgeSpringRestLength = 220f,
                EdgeSpringK = 6.0f,
            // Arcs try to shorten, but stop pulling once MinNodeSpacing is reached.
            MinimizeArcLength = true,
                OverlapRepulsionK = 35f,
            MinNodeSpacing = 40f,
                UseHardMinSpacing = true,
                HardMinSpacingIterations = 6,
                HardMinSpacingSlop = 0.5f,
            Softening = 50f,
            Drag = 2.4f,
            MaxSpeed = 2400f,
        });

        WarmStartLayout();

        _lastTickAt = DateTimeOffset.UtcNow;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
        _timer.Start();
    }

    public Diagram Diagram { get; private set; }
    public GravityLayoutEngine Engine { get; private set; }

    public void ResetVelocities()
    {
        foreach (var n in Diagram.Nodes)
        {
            n.Velocity = Vector2.Zero;
        }
        _arcExtraLaneShiftById.Clear();
    }

    public void StabilizeLayout()
    {
        ResetVelocities();
        WarmStartLayout();
        InvalidateVisual();
    }

    public void SetDiagram(Diagram diagram)
    {
        Diagram = diagram ?? throw new ArgumentNullException(nameof(diagram));
        Diagram.DistributeAllPortsProportionally();
        WarmStartLayout();
        InvalidateVisual();
    }

    private void WarmStartLayout()
    {
        // Make initial presentation stable/compact, without watching the simulation “converge”.
        // This runs fast for small graphs and improves first impression a lot.
        foreach (var n in Diagram.Nodes)
        {
            n.Velocity = Vector2.Zero;
        }

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
                    break;
                }
            }
        }
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
                var steps = (int)Math.Clamp(MathF.Ceiling(simDt / LiveMaxSubstepDt), 1f, LiveMaxSubstepsPerTick);
                var subDt = simDt / steps;
                for (var i = 0; i < steps; i++)
                {
                    Engine.Step(Diagram, subDt);
                }
            }
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        DrawArcs(context);
        DrawNodes(context);
        DrawDebugOverlay(context);

        var t = (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;
        DrawLabel(context, $"drag to move • t={t:0.0}s • nodes={Diagram.Nodes.Count} arcs={Diagram.Arcs.Count}",
            new Point(10, 10), Brushes.DimGray, 12);
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
        var fill = new SolidColorBrush(Color.FromRgb(245, 248, 255));
        var stroke = new Pen(new SolidColorBrush(Color.FromRgb(60, 90, 160)), 1);
        var portBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        var portTextBrush = Brushes.DimGray;

        foreach (var node in Diagram.Nodes)
        {
            var r = node.Bounds;
            var rect = new Rect(r.Left, r.Top, r.Width, r.Height);
            context.DrawRectangle(fill, stroke, rect, 8);
            DrawCenteredLabel(context, node.Text, rect, Brushes.Black, 14);
        }

        // Ports are drawn after nodes.
        foreach (var port in Diagram.Ports)
        {
            var node = Diagram.Nodes.FirstOrDefault(n => n.Id == port.Ref.NodeId);
            if (node is null) continue;

            var p = GravityLayoutEngine.GetPortWorldPosition(node, port.Ref);
            var pr = new Rect(p.X - 4, p.Y - 4, 8, 8);
            context.DrawEllipse(portBrush, null, new Point(p.X, p.Y), 4, 4);

            var textPos = GetOutsideTextAnchor(port.Ref.Side, new Point(p.X, p.Y), 10);
            DrawLabel(context, port.Text, textPos, portTextBrush, 12);
        }
    }

    private void DrawArcs(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(120, 120, 120)), 1.2);
        var textBrush = Brushes.DarkSlateGray;
        var arrowFill = pen.Brush ?? Brushes.Gray;
        var nodesById = Diagram.Nodes.ToDictionary(n => n.Id, n => n);
        var portsById = Diagram.Ports.ToDictionary(p => p.Id, p => p);
        var arcsOrdered = Diagram.Arcs.OrderBy(a => a.Id.Value, StringComparer.Ordinal).ToArray();

        // Lane offsets per (fromNode,fromSide,toNode,toSide) bundle.
        var lanesByKey = arcsOrdered
            .GroupBy(a =>
            {
                var fp = portsById[a.FromPortId];
                var tp = portsById[a.ToPortId];
                return (fp.Ref.NodeId, fp.Ref.Side, tp.Ref.NodeId, tp.Ref.Side);
            })
            .ToDictionary(g => g.Key, g => g.ToArray());

        var routed = new List<List<Vector2>>(Diagram.Arcs.Count);

        foreach (var arc in arcsOrdered)
        {
            if (!portsById.TryGetValue(arc.FromPortId, out var fromPort)) continue;
            if (!portsById.TryGetValue(arc.ToPortId, out var toPort)) continue;
            if (!nodesById.TryGetValue(fromPort.Ref.NodeId, out var fromNode)) continue;
            if (!nodesById.TryGetValue(toPort.Ref.NodeId, out var toNode)) continue;

            var a = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref);
            var b = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref);

            var key = (fromPort.Ref.NodeId, fromPort.Ref.Side, toPort.Ref.NodeId, toPort.Ref.Side);
            var bundle = lanesByKey[key];
            var laneIndex = Array.IndexOf(bundle, arc);
            var centerLane = (bundle.Length - 1) / 2f;
            var baseLaneOffset = (laneIndex - centerLane) * ArcLaneSpacing;
            var lastExtra = _arcExtraLaneShiftById.TryGetValue(arc.Id, out var s) ? s : 0f;

            var outDistance = Engine.Settings.MinimizeArcLength ? 10f : ArcOutDistance;

            // Choose a stable lane: prefer current lane unless there is a clear win.
            var bestExtra = lastExtra;
            var bestPoly = RouteArcPolyline(
                a,
                fromPort.Ref.Side,
                b,
                toPort.Ref.Side,
                baseLaneOffset + lastExtra,
                outDistance,
                Diagram.Nodes,
                fromNode,
                toNode);
            var bestScore = ScorePolyline(bestPoly, Diagram.Nodes, fromNode, toNode, routed);

            // Actively try to return to the center lane when possible.
            if (MathF.Abs(lastExtra) > 0.001f)
            {
                var extra = 0f;
                var cand = RouteArcPolyline(
                    a,
                    fromPort.Ref.Side,
                    b,
                    toPort.Ref.Side,
                    baseLaneOffset + extra,
                    outDistance,
                    Diagram.Nodes,
                    fromNode,
                    toNode);
                var score = ScorePolyline(cand, Diagram.Nodes, fromNode, toNode, routed);

                // Prefer smaller extra lane shifts, so offsets decay back to 0.
                score += MathF.Abs(extra) * 3.0f;
                // Keep some hysteresis.
                score += MathF.Abs(extra - lastExtra) * 0.8f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestExtra = extra;
                    bestPoly = cand;
                }
            }

            for (var attempt = 1; attempt <= 8; attempt++)
            {
                var dir = (attempt % 2 == 1) ? 1f : -1f;
                var extra = lastExtra + dir * ArcLaneSpacing * ((attempt + 1) / 2);
                var cand = RouteArcPolyline(
                    a,
                    fromPort.Ref.Side,
                    b,
                    toPort.Ref.Side,
                    baseLaneOffset + extra,
                    outDistance,
                    Diagram.Nodes,
                    fromNode,
                    toNode);
                var score = ScorePolyline(cand, Diagram.Nodes, fromNode, toNode, routed);

                // Prefer smaller extra lane shifts, so offsets decay back to 0.
                score += MathF.Abs(extra) * 3.0f;

                // Small penalty for changing lanes (hysteresis).
                score += MathF.Abs(extra - lastExtra) * 0.8f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestExtra = extra;
                    bestPoly = cand;
                }
            }

            _arcExtraLaneShiftById[arc.Id] = bestExtra;
            var poly = bestPoly;

            routed.Add(poly);
            DrawPolyline(context, pen, poly);
            DrawArrowHead(context, arrowFill, pen, poly);

            var (labelPos, labelNormal) = GetPolylineMidpointWithNormal(poly);
            var labelPoint = new Point(labelPos.X + labelNormal.X * 10, labelPos.Y + labelNormal.Y * 10);
            DrawLabel(context, arc.Text, labelPoint, textBrush, 12);
        }
    }

    private static void DrawArrowHead(DrawingContext context, IBrush fill, Pen outline, List<Vector2> poly)
    {
        if (poly.Count < 2) return;
        var tip = poly[^1];
        var prev = poly[^2];
        var d = tip - prev;
        var len = d.Length();
        if (len < 0.001f) return;
        var dir = d / len;
        var perp = new Vector2(-dir.Y, dir.X);

        const float arrowLen = 12f;
        const float arrowWidth = 8f;

        // Keep arrow size reasonable when the last segment is very short.
        var l = MathF.Min(arrowLen, MathF.Max(4f, len * 0.8f));
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
        for (var i = 0; i + 1 < points.Count; i++)
        {
            var a = points[i];
            var b = points[i + 1];
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
        System.Collections.ObjectModel.ReadOnlyCollection<RectNode> allNodes,
        RectNode fromNode,
        RectNode toNode)
    {
        var outA = from + SideDir(fromSide) * outDistance;
        var outB = to + SideDir(toSide) * outDistance;

        var cand1 = BuildOrthogonalCandidate(from, outA, outB, to, preferHorizontalFirst: true, laneOffset);
        var cand2 = BuildOrthogonalCandidate(from, outA, outB, to, preferHorizontalFirst: false, laneOffset);

        var score1 = ScoreCandidate(cand1, allNodes, fromNode, toNode);
        var score2 = ScoreCandidate(cand2, allNodes, fromNode, toNode);
        return score2 < score1 ? cand2 : cand1;
    }

    private static float ScoreCandidate(List<Vector2> poly, System.Collections.ObjectModel.ReadOnlyCollection<RectNode> nodes, RectNode fromNode, RectNode toNode)
    {
        var score = 0f;
        for (var i = 0; i + 1 < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[i + 1];
            foreach (var n in nodes)
            {
                // Ignore the two endpoint nodes: we intentionally go out of ports.
                if (ReferenceEquals(n, fromNode) || ReferenceEquals(n, toNode))
                    continue;

                var r = Expand(n.Bounds, ArcNodeClearance);
                if (AxisAlignedSegmentIntersectsRect(a, b, r))
                {
                    score += 10f;
                }
            }
        }

        // Prefer shorter routes when crossings are comparable.
        score += PolylineLength(poly) * 0.06f;

        // Prefer fewer bends (straighter polyline).
        var bends = Math.Max(0, poly.Count - 2);
        score += bends * 1.2f;
        return score;
    }

    private static float ScorePolyline(
        List<Vector2> poly,
        System.Collections.ObjectModel.ReadOnlyCollection<RectNode> nodes,
        RectNode fromNode,
        RectNode toNode,
        List<List<Vector2>> alreadyRouted)
    {
        var score = ScoreCandidate(poly, nodes, fromNode, toNode);

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

        for (var i = pts.Count - 2; i > 0; i--)
        {
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

    private static Vector2 SideDir(RectSide side)
        => side switch
        {
            RectSide.Top => new Vector2(0, -1),
            RectSide.Right => new Vector2(1, 0),
            RectSide.Bottom => new Vector2(0, 1),
            RectSide.Left => new Vector2(-1, 0),
            _ => new Vector2(1, 0),
        };

    private static RectF Expand(in RectF r, float margin)
        => new(r.X - margin, r.Y - margin, r.Width + 2 * margin, r.Height + 2 * margin);

    private static bool AxisAlignedSegmentIntersectsRect(Vector2 a, Vector2 b, RectF r)
    {
        if (MathF.Abs(a.X - b.X) < 0.001f)
        {
            // Vertical
            var x = a.X;
            if (x < r.Left || x > r.Right) return false;
            var y0 = MathF.Min(a.Y, b.Y);
            var y1 = MathF.Max(a.Y, b.Y);
            return !(y1 < r.Top || y0 > r.Bottom);
        }

        if (MathF.Abs(a.Y - b.Y) < 0.001f)
        {
            // Horizontal
            var y = a.Y;
            if (y < r.Top || y > r.Bottom) return false;
            var x0 = MathF.Min(a.X, b.X);
            var x1 = MathF.Max(a.X, b.X);
            return !(x1 < r.Left || x0 > r.Right);
        }

        // Should not happen; route is axis-aligned.
        return false;
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
        var ft = CreateFormattedText(text, brush, fontSize, rect.Width, TextAlignment.Center);
        var origin = new Point(rect.X + (rect.Width - ft.Width) / 2.0, rect.Y + (rect.Height - ft.Height) / 2.0);
        context.DrawText(ft, origin);
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
