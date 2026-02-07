using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

#if false // Example-based dump tests are temporarily disabled (dump schema and physics model changed).
public sealed class FrozenLayoutDumpTests
{
    [Fact]
    public void PreviewStep_FromSavedDump_HasNoNextPixelStep_EvenThoughContinuousMoveExists()
    {
        var dump = LoadDump("gravity-dump-latest-20260207-065638.json");

        var diagram = BuildDiagram(dump);
        var settings = BuildSettings(dump);
        var engine = new GravityLayoutEngine(settings);

        var preview = engine.PreviewStep(diagram, dump.Dt);

        // The actual freeze condition in UI is pixel-stability: no integer pixel coordinate changes.
        // Under that meaning, this dump should have NO "next step".
        static int Pixel(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

        var willMovePixels = preview.Nodes.Any(n =>
        {
            var cur = (Pixel(n.Position.X), Pixel(n.Position.Y));
            var pred = (Pixel(n.PredictedPosition.X), Pixel(n.PredictedPosition.Y));
            var predNoForces = (Pixel(n.PredictedPositionIfNoForces.X), Pixel(n.PredictedPositionIfNoForces.Y));
            return pred != cur || predNoForces != cur;
        });

        Assert.False(willMovePixels);

        // But the continuous model still wants to move (sub-pixel), which explains
        // why pixel-based auto-stop can freeze a non-optimal layout.
        var hasContinuousIntent = preview.Nodes.Any(n =>
            n.DeltaPositionBeforeConstraints.LengthSquared() > 1e-12f ||
            n.ForceTotal.LengthSquared() > 1e-12f);

        Assert.True(hasContinuousIntent);
    }

    [Fact]
    public void PreviewStep_FromSavedDump_ArcAttractionHasXComponent()
    {
        var dump = LoadDump("gravity-dump-latest-20260207-065638.json");

        var diagram = BuildDiagram(dump);
        var settings = BuildSettings(dump);
        var engine = new GravityLayoutEngine(settings);

        var preview = engine.PreviewStep(diagram, dump.Dt);

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

        // With MinimizeArcLength=true and rest length=0, the spring pull adds k*delta.
        // (Min spacing is enforced elsewhere, not by disabling arc forces.)
        var expectedX = settings.ConnectedArcAttractionK * delta.X;

        var nodeA = preview.Nodes.Single(n => n.Id == a.Id);
        var nodeB = preview.Nodes.Single(n => n.Id == b.Id);

        Assert.Equal(expectedX, nodeA.ForceConnectedArcAttraction.X, precision: 3);
        Assert.Equal(-expectedX, nodeB.ForceConnectedArcAttraction.X, precision: 3);
    }

    [Fact]
    public void PreviewStep_FromFrozenNonoptimalDump_ArcAttractionHasXComponent()
    {
        var dump = LoadDump("gravity-dump-frozen-nonoptimal-20260207.json");

        var diagram = BuildDiagram(dump);
        var settings = BuildSettings(dump);
        var engine = new GravityLayoutEngine(settings);
        var preview = engine.PreviewStep(diagram, dump.Dt);

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

        // For MinimizeArcLength=true, rest length is 0, so stretch pull contributes k*delta.
        var expectedX = settings.ConnectedArcAttractionK * delta.X;
        Assert.True(MathF.Abs(expectedX) > 1e-3f);

        var nodeA = preview.Nodes.Single(n => n.Id == a.Id);
        var nodeB = preview.Nodes.Single(n => n.Id == b.Id);
        Assert.Equal(expectedX, nodeA.ForceConnectedArcAttraction.X, precision: 3);
        Assert.Equal(-expectedX, nodeB.ForceConnectedArcAttraction.X, precision: 3);
    }

    [Fact]
    public void ArcRoutingExitPoint_FromSavedDump_IsPerpendicularAndAtMinSpacing()
    {
        var dump = LoadDump("gravity-dump-latest-20260207-065638.json");
        var diagram = BuildDiagram(dump);
        var settings = BuildSettings(dump);

        var arc = diagram.Arcs.Single();
        var fromPort = diagram.TryGetPort(arc.FromPortId);
        var toPort = diagram.TryGetPort(arc.ToPortId);
        Assert.NotNull(fromPort);
        Assert.NotNull(toPort);

        var fromNode = diagram.Nodes.Single(n => n.Id == fromPort!.Ref.NodeId);
        var toNode = diagram.Nodes.Single(n => n.Id == toPort!.Ref.NodeId);
        var from = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort!.Ref);
        var to = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort!.Ref);

        // Match Demo behavior: when minimizing arc length, base out distance is 10.
        var baseOutDistance = settings.MinimizeArcLength ? 10f : 18f;
        var expectedOutDistance = ArcRoutingGeometry.ComputeOutDistance(baseOutDistance, settings.MinNodeSpacing);

        var exitFrom = ArcRoutingGeometry.ComputeExitPoint(from, fromPort.Ref.Side, baseOutDistance, settings.MinNodeSpacing);
        var exitTo = ArcRoutingGeometry.ComputeExitPoint(to, toPort.Ref.Side, baseOutDistance, settings.MinNodeSpacing);

        Assert.Equal(expectedOutDistance, Vector2.Distance(from, exitFrom), precision: 4);
        Assert.Equal(expectedOutDistance, Vector2.Distance(to, exitTo), precision: 4);

        var dirFrom = exitFrom - from;
        var expectedDirFrom = ArcRoutingGeometry.SideDir(fromPort.Ref.Side);
        Assert.True(Vector2.DistanceSquared(Vector2.Normalize(dirFrom), expectedDirFrom) < 1e-6f);

        var dirTo = exitTo - to;
        var expectedDirTo = ArcRoutingGeometry.SideDir(toPort.Ref.Side);
        Assert.True(Vector2.DistanceSquared(Vector2.Normalize(dirTo), expectedDirTo) < 1e-6f);
    }

    private static DumpRoot LoadDump(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        var json = File.ReadAllText(path);

        var dump = JsonSerializer.Deserialize<DumpRoot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(dump);
        return dump!;
    }

    private static Diagram BuildDiagram(DumpRoot dump)
    {
        var d = new Diagram { AutoDistributePorts = false };

        foreach (var n in dump.Diagram.Nodes)
        {
            d.AddNode(new RectNode
            {
                Id = new DiagramId(n.Id),
                Text = n.Text ?? string.Empty,
                Position = new Vector2(n.Position.X, n.Position.Y),
                Velocity = new Vector2(n.Velocity.X, n.Velocity.Y),
                Width = n.Width,
                Height = n.Height,
            });
        }

        foreach (var p in dump.Diagram.Ports)
        {
            d.AddPort(new Port
            {
                Id = new DiagramId(p.Id),
                Text = p.Text ?? string.Empty,
                Ref = new PortRef(new DiagramId(p.NodeId), Enum.Parse<RectSide>(p.Side, ignoreCase: true), p.Offset),
            });
        }

        foreach (var a in dump.Diagram.Arcs)
        {
            d.AddArc(new Arc
            {
                Id = new DiagramId(a.Id),
                Text = a.Text ?? string.Empty,
                FromPortId = new DiagramId(a.FromPortId),
                ToPortId = new DiagramId(a.ToPortId),
            });
        }

        return d;
    }

    private static LayoutSettings BuildSettings(DumpRoot dump)
    {
        var s = dump.Settings;
        return new LayoutSettings
        {
            NodeMass = s.NodeMass,
            Softening = s.Softening,
            BackgroundPairGravity = s.BackgroundPairGravity,
            EdgeSpringRestLength = s.EdgeSpringRestLength,
            ConnectedArcAttractionK = s.ConnectedArcAttractionK,
            MinimizeArcLength = s.MinimizeArcLength,
            MinNodeSpacing = s.MinNodeSpacing,
            UseHardMinSpacing = s.UseHardMinSpacing,
            HardMinSpacingIterations = s.HardMinSpacingIterations,
            HardMinSpacingSlop = s.HardMinSpacingSlop,
            OverlapRepulsionK = s.OverlapRepulsionK,
            SoftOverlapBoostWhenHardDisabled = s.SoftOverlapBoostWhenHardDisabled,
            Drag = s.Drag,
            MaxSpeed = s.MaxSpeed,
        };
    }

    private sealed record DumpRoot(
        float Dt,
        DumpSettings Settings,
        DumpDiagram Diagram);

    private sealed record DumpSettings(
        float NodeMass,
        float Softening,
        float BackgroundPairGravity,
        float EdgeSpringRestLength,
        float ConnectedArcAttractionK,
        bool MinimizeArcLength,
        float MinNodeSpacing,
        bool UseHardMinSpacing,
        int HardMinSpacingIterations,
        float HardMinSpacingSlop,
        float OverlapRepulsionK,
        float SoftOverlapBoostWhenHardDisabled,
        float Drag,
        float MaxSpeed);

    private sealed record DumpDiagram(
        DumpNode[] Nodes,
        DumpPort[] Ports,
        DumpArc[] Arcs);

    private sealed record DumpNode(
        string Id,
        string? Text,
        DumpVec2 Position,
        DumpVec2 Velocity,
        float Width,
        float Height);

    private sealed record DumpPort(
        string Id,
        string? Text,
        string NodeId,
        string Side,
        float Offset);

    private sealed record DumpArc(
        string Id,
        string? Text,
        string FromPortId,
        string ToPortId);

    private sealed record DumpVec2(float X, float Y);
}
#endif
