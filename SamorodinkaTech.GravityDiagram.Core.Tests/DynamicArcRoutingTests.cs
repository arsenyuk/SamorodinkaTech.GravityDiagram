using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;
using Xunit.Abstractions;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

/// <summary>
/// Динамические тесты маршрутизации дуг: проверяют поведение движка при движении нод.
/// </summary>
public sealed class DynamicArcRoutingTests
{
    private readonly ITestOutputHelper _output;

    public DynamicArcRoutingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Нода B кружит вокруг ноды A по окружности (12 позиций, по 30°).
    /// На каждой позиции проверяется что:
    /// 1) Все сегменты дуги горизонтальные или вертикальные (90° углы).
    /// 2) Нет лишних точек на одной прямой.
    /// 3) Дуга не пересекает внутренность ни одной ноды.
    /// </summary>
    [Fact]
    public void ArcRoutesCorrectly_WhenNodeBCirclesAroundA()
    {
        var d = new Diagram();
        var nodeA = d.AddNode(new RectNode
        {
            Id = new DiagramId("a"),
            Text = "A",
            Position = new Vector2(300, 300),
            Width = 120,
            Height = 70,
        });
        var nodeB = d.AddNode(new RectNode
        {
            Id = new DiagramId("b"),
            Text = "B",
            Position = new Vector2(500, 300),
            Width = 120,
            Height = 70,
        });

        nodeA.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
        nodeB.SetSideFlow(RectSide.Left, PortFlow.Incoming);

        var portA = d.AddPort(new Port
        {
            Id = DiagramId.New(),
            Text = "out",
            Ref = new PortRef(nodeA.Id, RectSide.Right, 0.5f),
        });
        var portB = d.AddPort(new Port
        {
            Id = DiagramId.New(),
            Text = "in",
            Ref = new PortRef(nodeB.Id, RectSide.Left, 0.5f),
        });

        var arc = d.AddArc(new Arc
        {
            Id = DiagramId.New(),
            Text = "A->B",
            FromPortId = portA.Id,
            ToPortId = portB.Id,
        });

        var settings = new LayoutSettings
        {
            NodeMass = 12.8f,
            BackgroundPairGravity = 0f,
            ConnectedArcAttractionK = 6f,
            EdgeSpringRestLength = 220f,
            MinimizeArcLength = true,
            OverlapRepulsionK = 35f,
            UseHardMinSpacing = true,
            HardMinSpacingIterations = 6,
            HardMinSpacingSlop = 0.5f,
            MinNodeSpacing = 10f,
            Softening = 50f,
            Drag = 2.4f,
            MaxSpeed = 2400f,
            ArcPointAttractionK = 6f,
            ArcPointMoveFactor = 0.035f,
            ArcPointNodeRepulsionK = 1200f,
            ArcPointConstraintIterations = 6,
            ArcPointMergeDistance = 2f,
            MaxArcInternalPoints = 16,
        };

        var engine = new GravityLayoutEngine(settings);

        // B circles around A: 12 positions, 30 degrees apart.
        var centerA = nodeA.Position;
        var radius = 250f;
        var steps = 12;
        var violations = new List<string>();

        for (var step = 0; step < steps; step++)
        {
            var angle = step * MathF.PI * 2f / steps;
            var bx = centerA.X + radius * MathF.Cos(angle);
            var by = centerA.Y + radius * MathF.Sin(angle);
            nodeB.Position = new Vector2(bx, by);
            nodeB.Velocity = Vector2.Zero;

            // Run a few engine steps to let the arc settle.
            for (var i = 0; i < 10; i++)
                engine.Step(d, 1f / 60f);

            // Pin node A — physics may nudge it; we only move B.
            nodeA.Position = centerA;
            nodeA.Velocity = Vector2.Zero;

            // Re-run one step so InternalPoints are computed for the pinned positions.
            engine.Step(d, 1f / 60f);

            // --- Checks ---

            // 1) All internal segments must be axis-aligned (90 degree bends).
            // Check internal points only — ports may drift slightly due to physics.
            var internalPoints = arc.InternalPoints;
            for (var i = 0; i + 1 < internalPoints.Count; i++)
            {
                var ax = internalPoints[i].X;
                var ay = internalPoints[i].Y;
                var bx2 = internalPoints[i + 1].X;
                var by2 = internalPoints[i + 1].Y;
                var isH = MathF.Abs(ay - by2) < 0.01f;
                var isV = MathF.Abs(ax - bx2) < 0.01f;
                Assert.True(isH || isV,
                    $"Step {step}: internal segment [{i}] ({ax:F1},{ay:F1})->({bx2:F1},{by2:F1}) is not axis-aligned");
            }

            // 2) No collinear redundant points.
            for (var i = 1; i + 1 < internalPoints.Count; i++)
            {
                var prev = internalPoints[i - 1];
                var curr = internalPoints[i];
                var next = internalPoints[i + 1];
                var sameX = MathF.Abs(prev.X - curr.X) < 0.01f && MathF.Abs(curr.X - next.X) < 0.01f;
                var sameY = MathF.Abs(prev.Y - curr.Y) < 0.01f && MathF.Abs(curr.Y - next.Y) < 0.01f;
                if (sameX || sameY)
                {
                    _output.WriteLine($"COLLINEAR at step {step}: [{i}] prev=({prev.X:F4},{prev.Y:F4}) curr=({curr.X:F4},{curr.Y:F4}) next=({next.X:F4},{next.Y:F4})");
                    _output.WriteLine($"  dx_prev={MathF.Abs(prev.X-curr.X):F4} dx_next={MathF.Abs(curr.X-next.X):F4} dy_prev={MathF.Abs(prev.Y-curr.Y):F4} dy_next={MathF.Abs(curr.Y-next.Y):F4}");
                }
                Assert.False(sameX || sameY,
                    $"Step {step}: collinear point [{i}] ({curr.X:F1},{curr.Y:F1}) on same line as neighbors");
            }

            // 3) No arc segment intersects node interiors.
            for (var i = 0; i + 1 < internalPoints.Count; i++)
            {
                var pa = internalPoints[i];
                var pb = internalPoints[i + 1];
                foreach (var node in d.Nodes)
                {
                    if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(pa, pb, node.Bounds))
                    {
                        var msg = $"Step {step}: segment [{i}] ({pa.X:F1},{pa.Y:F1})->({pb.X:F1},{pb.Y:F1}) intersects node {node.Text} {node.Bounds}";
                        violations.Add(msg);
                        _output.WriteLine($"VIOLATION: {msg}");
                    }
                }
            }

            _output.WriteLine($"Step {step}: B=({bx:F0},{by:F0}) points={internalPoints.Count}");
        }

        // All violations collected — fail if any.
        Assert.True(violations.Count == 0,
            $"Found {violations.Count} violations:\n{string.Join("\n", violations)}");
    }
}
