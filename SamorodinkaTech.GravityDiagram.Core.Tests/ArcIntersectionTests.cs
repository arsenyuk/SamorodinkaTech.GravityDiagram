using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;
using Xunit.Abstractions;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

/// <summary>
/// Универсальные тесты пересечений дуг с нодами — без привязки к дампам.
/// Создают ноды и дуги программно, прогоняют движок и проверяют отсутствие пересечений.
/// </summary>
public sealed class ArcIntersectionTests
{
    private readonly ITestOutputHelper _output;

    public ArcIntersectionTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Две ноды рядом по горизонтали. Дуга из правого порта A в левый порт B.
    /// 120 шагов движка — ни один сегмент дуги не должен пересекать внутренность ноды.
    /// </summary>
    [Fact]
    public void TwoNodes_SideBySide_ArcDoesNotIntersect()
    {
        var (diagram, engine) = CreateTwoNodeDiagram(
            posA: new Vector2(0, 0), posB: new Vector2(250, 0),
            sideA: RectSide.Right, sideB: RectSide.Left);

        for (var i = 0; i < 120; i++)
            engine.Step(diagram, 1f / 60f);

        AssertNoArcIntersections(diagram);
    }

    /// <summary>
    /// Две ноды по диагонали. Дуга из правого порта A в верхний порт B.
    /// 120 шагов движка — ни один сегмент дуги не должен пересекать внутренность ноды.
    /// </summary>
    [Fact]
    public void TwoNodes_Diagonal_ArcDoesNotIntersect()
    {
        var (diagram, engine) = CreateTwoNodeDiagram(
            posA: new Vector2(0, 0), posB: new Vector2(200, -150),
            sideA: RectSide.Right, sideB: RectSide.Top);

        for (var i = 0; i < 120; i++)
            engine.Step(diagram, 1f / 60f);

        AssertNoArcIntersections(diagram);
    }

    /// <summary>
    /// Три ноды в ряд: A — Block — B. Дуга из A в B должна обойти блокирующую ноду.
    /// 120 шагов движка — ни один сегмент дуги не должен пересекать внутренность ни одной ноды.
    /// </summary>
    [Fact]
    public void ThreeNodes_WithBlocker_ArcRoutesAround()
    {
        var d = new Diagram();
        var n1 = d.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(-300, 0), Width = 100, Height = 60 });
        var blocker = d.AddNode(new RectNode { Id = DiagramId.New(), Text = "Block", Position = new Vector2(0, 80), Width = 100, Height = 60 });
        var n2 = d.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(300, 0), Width = 100, Height = 60 });

        n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
        n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

        var p1 = d.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
        var p2 = d.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
        d.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

        var engine = new GravityLayoutEngine(DefaultSettings());

        for (var i = 0; i < 120; i++)
            engine.Step(d, 1f / 60f);

        AssertNoArcIntersections(d);
    }

    /// <summary>
    /// Две ноды рядом, дуга идёт в обратном направлении (B→A при правом порте B и левом порте A).
    /// 120 шагов движка — ни один сегмент дуги не должен пересекать внутренность ноды.
    /// </summary>
    [Fact]
    public void TwoNodes_ReversedDirection_ArcDoesNotIntersect()
    {
        var (diagram, engine) = CreateTwoNodeDiagram(
            posA: new Vector2(0, 0), posB: new Vector2(250, 0),
            sideA: RectSide.Left, sideB: RectSide.Right);

        for (var i = 0; i < 120; i++)
            engine.Step(diagram, 1f / 60f);

        AssertNoArcIntersections(diagram);
    }

    // --- Вспомогательные методы ---

    private static (Diagram diagram, GravityLayoutEngine engine) CreateTwoNodeDiagram(
        Vector2 posA, Vector2 posB, RectSide sideA, RectSide sideB)
    {
        var d = new Diagram();
        var nA = d.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = posA, Width = 120, Height = 70 });
        var nB = d.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = posB, Width = 120, Height = 70 });

        nA.SetSideFlow(sideA, PortFlow.Outgoing);
        nB.SetSideFlow(sideB, PortFlow.Incoming);

        var pA = d.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(nA.Id, sideA, 0.5f) });
        var pB = d.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(nB.Id, sideB, 0.5f) });
        d.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = pA.Id, ToPortId = pB.Id });

        return (d, new GravityLayoutEngine(DefaultSettings()));
    }

    private static LayoutSettings DefaultSettings() => new()
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

    private void AssertNoArcIntersections(Diagram diagram)
    {
        var violations = new List<string>();

        foreach (var arc in diagram.Arcs)
        {
            var fromPort = diagram.TryGetPort(arc.FromPortId);
            var toPort = diagram.TryGetPort(arc.ToPortId);
            if (fromPort is null || toPort is null) continue;

            var fromNode = diagram.Nodes.Single(n => n.Id == fromPort.Ref.NodeId);
            var toNode = diagram.Nodes.Single(n => n.Id == toPort.Ref.NodeId);

            var start = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref);
            var end = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref);

            var poly = new List<Vector2>(arc.InternalPoints.Count + 2) { start };
            poly.AddRange(arc.InternalPoints);
            poly.Add(end);

            for (var i = 0; i + 1 < poly.Count; i++)
            {
                var pa = poly[i];
                var pb = poly[i + 1];
                foreach (var node in diagram.Nodes)
                {
                    if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(pa, pb, node.Bounds))
                    {
                        var msg = $"Arc {arc.Text}: segment [{i}] ({pa.X:F1},{pa.Y:F1})->({pb.X:F1},{pb.Y:F1}) intersects node {node.Text}";
                        violations.Add(msg);
                        _output.WriteLine($"VIOLATION: {msg}");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} arc-node intersections:\n{string.Join("\n", violations)}");
    }
}
