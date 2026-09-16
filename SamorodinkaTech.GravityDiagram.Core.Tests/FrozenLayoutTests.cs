using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;
using Xunit.Abstractions;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

/// <summary>
/// Тесты «замороженного» состояния и физических свойств — без привязки к дампам.
/// Создают ноды и дуги программно, проверяют pixel stability, силы, exit points.
/// </summary>
public sealed class FrozenLayoutTests
{
    private readonly ITestOutputHelper _output;

    public FrozenLayoutTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Две ноды рядом, дуга между ними. После стабилизации:
    /// - ноды не двигаются на уровне пикселей (pixel stability)
    /// - но физическая модель всё ещё имеет ненулевые силы (sub-pixel движение)
    /// Проверяет что pixel-based auto-stop корректно определяет заморозку.
    /// </summary>
    [Fact]
    public void StabilizedLayout_HasNoPixelMovement_ButHasContinuousForces()
    {
        var (diagram, engine) = CreateStabilizedDiagram();

        var preview = engine.PreviewStep(diagram, 1f / 60f);

        static int Pixel(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

        var willMovePixels = preview.Nodes.Any(n =>
        {
            var cur = (Pixel(n.Position.X), Pixel(n.Position.Y));
            var pred = (Pixel(n.PredictedPosition.X), Pixel(n.PredictedPosition.Y));
            var predNoForces = (Pixel(n.PredictedPositionIfNoForces.X), Pixel(n.PredictedPositionIfNoForces.Y));
            return pred != cur || predNoForces != cur;
        });

        Assert.False(willMovePixels);

        var hasContinuousIntent = preview.Nodes.Any(n =>
            n.DeltaPositionBeforeConstraints.LengthSquared() > 1e-12f ||
            n.ForceTotal.LengthSquared() > 1e-12f);

        Assert.True(hasContinuousIntent);
    }

    /// <summary>
    /// Проверяет что сила притяжения по дуге имеет горизонтальную компоненту (X).
    /// Для MinimizeArcLength=true сила = k * delta, где delta — расстояние между портами.
    /// </summary>
    [Fact]
    public void ArcAttraction_HasCorrectXComponent()
    {
        var (diagram, engine) = CreateTwoNodeDiagram(
            posA: new Vector2(0, 0), posB: new Vector2(250, 0),
            sideA: RectSide.Right, sideB: RectSide.Left);

        var preview = engine.PreviewStep(diagram, 1f / 60f);

        var arc = diagram.Arcs.Single();
        var fromPort = diagram.TryGetPort(arc.FromPortId);
        var toPort = diagram.TryGetPort(arc.ToPortId);
        Assert.NotNull(fromPort);
        Assert.NotNull(toPort);

        var a = diagram.Nodes.Single(n => n.Id == fromPort!.Ref.NodeId);
        var b = diagram.Nodes.Single(n => n.Id == toPort!.Ref.NodeId);

        var pa = GravityLayoutEngine.GetPortWorldPosition(a, fromPort!.Ref);
        var pb = GravityLayoutEngine.GetPortWorldPosition(b, toPort!.Ref);
        var delta = pb - pa;
        var dist = delta.Length();
        Assert.True(dist > 0.001f);

        var settings = new LayoutSettings { MinimizeArcLength = true, ConnectedArcAttractionK = 6f };
        var expectedX = settings.ConnectedArcAttractionK * delta.X;

        var nodeA = preview.Nodes.Single(n => n.Id == a.Id);
        var nodeB = preview.Nodes.Single(n => n.Id == b.Id);

        Assert.Equal(expectedX, nodeA.ForceConnectedArcAttraction.X, precision: 3);
        Assert.Equal(-expectedX, nodeB.ForceConnectedArcAttraction.X, precision: 3);
    }

    /// <summary>
    /// Проверяет что точка выхода дуги из порта находится перпендикулярно стороне ноды
    /// на расстоянии, вычисленном с учётом MinNodeSpacing.
    /// </summary>
    [Fact]
    public void ArcExitPoint_IsPerpendicularAndAtCorrectDistance()
    {
        var fromNode = new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 120, Height = 70 };
        var toNode = new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(300, 0), Width = 120, Height = 70 };

        var fromPort = new PortRef(fromNode.Id, RectSide.Right, 0.5f);
        var toPort = new PortRef(toNode.Id, RectSide.Left, 0.5f);

        var from = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort);
        var to = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort);

        var settings = new LayoutSettings { MinimizeArcLength = true, MinNodeSpacing = 20f };
        var baseOutDistance = settings.MinimizeArcLength ? 10f : 18f;
        var expectedOutDistance = ArcRoutingGeometry.ComputeOutDistance(baseOutDistance, settings.MinNodeSpacing);

        var exitFrom = ArcRoutingGeometry.ComputeExitPoint(from, fromPort.Side, baseOutDistance, settings.MinNodeSpacing);
        var exitTo = ArcRoutingGeometry.ComputeExitPoint(to, toPort.Side, baseOutDistance, settings.MinNodeSpacing);

        Assert.Equal(expectedOutDistance, Vector2.Distance(from, exitFrom), precision: 4);
        Assert.Equal(expectedOutDistance, Vector2.Distance(to, exitTo), precision: 4);

        var dirFrom = exitFrom - from;
        var expectedDirFrom = ArcRoutingGeometry.SideDir(fromPort.Side);
        Assert.True(Vector2.DistanceSquared(Vector2.Normalize(dirFrom), expectedDirFrom) < 1e-6f);

        var dirTo = exitTo - to;
        var expectedDirTo = ArcRoutingGeometry.SideDir(toPort.Side);
        Assert.True(Vector2.DistanceSquared(Vector2.Normalize(dirTo), expectedDirTo) < 1e-6f);
    }

    // --- Вспомогательные методы ---

    private static (Diagram diagram, GravityLayoutEngine engine) CreateStabilizedDiagram()
    {
        var d = new Diagram();
        var nA = d.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 120, Height = 70 });
        var nB = d.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(250, 0), Width = 120, Height = 70 });

        nA.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
        nB.SetSideFlow(RectSide.Left, PortFlow.Incoming);

        var pA = d.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(nA.Id, RectSide.Right, 0.5f) });
        var pB = d.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(nB.Id, RectSide.Left, 0.5f) });
        d.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = pA.Id, ToPortId = pB.Id });

        var engine = new GravityLayoutEngine(new LayoutSettings
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
        });

        for (var i = 0; i < 200; i++)
            engine.Step(d, 1f / 60f);

        return (d, engine);
    }

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

        return (d, new GravityLayoutEngine(new LayoutSettings
        {
            BackgroundPairGravity = 0f,
            ConnectedArcAttractionK = 6f,
            MinimizeArcLength = true,
            OverlapRepulsionK = 35f,
            UseHardMinSpacing = true,
            HardMinSpacingIterations = 6,
            HardMinSpacingSlop = 0.5f,
            MinNodeSpacing = 10f,
            Softening = 50f,
            Drag = 2.4f,
            MaxSpeed = 2400f,
        }));
    }
}
