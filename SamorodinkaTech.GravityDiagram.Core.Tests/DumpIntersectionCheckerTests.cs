using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

public sealed class DumpIntersectionCheckerTests
{
    [Fact]
    public void CheckLatestDump_ForInteriorArcSegmentIntersections()
    {
        var (dump, _) = LoadDump("gravity-dump-latest-20260207-065638.json");

        var diagram = BuildDiagram(dump);
        var settings = BuildSettings(dump);
        var engine = new GravityLayoutEngine(settings);

        // Run a few engine steps to populate arc internal points.
        for (var i = 0; i < 60; i++)
            engine.Step(diagram, 1f / 60f);

        var violations = 0;
        foreach (var arc in diagram.Arcs)
        {
            var fromPort = diagram.TryGetPort(arc.FromPortId);
            var toPort = diagram.TryGetPort(arc.ToPortId);
            if (fromPort is null || toPort is null) continue;
            var fromNode = diagram.Nodes.Single(n => n.Id == fromPort.Ref.NodeId);
            var toNode = diagram.Nodes.Single(n => n.Id == toPort.Ref.NodeId);

            var a = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref);
            var b = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref);

            var poly = new System.Collections.Generic.List<Vector2> { a };
            foreach (var p in arc.InternalPoints) poly.Add(p);
            poly.Add(b);

            for (var si = 0; si + 1 < poly.Count; si++)
            {
                var p0 = poly[si];
                var p1 = poly[si + 1];
                foreach (var node in diagram.Nodes)
                {
                    if (node.Id == fromNode.Id || node.Id == toNode.Id) continue;
                    if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(p0, p1, node.Bounds))
                    {
                        violations++;
                        Console.WriteLine($"VIOLATION: arc {arc.Id} seg {si} intersects node {node.Id} -- [{p0}] -> [{p1}] node={node.Bounds}");
                    }
                }
            }
        }

        Assert.Equal(0, violations);
    }

    [Fact]
    public void CheckFreshV5Dump_ForInteriorArcSegmentIntersections()
    {
        var (dump, _) = LoadDump("gravity-dump-20260519-034538.410-a78b5194031b40578f6ce2ab2862c5a5.json");

        var diagram = BuildDiagram(dump);
        Assert.Equal(2, diagram.Nodes.Count);
        Assert.Single(diagram.Arcs);

        var settings = BuildSettings(dump);
        var engine = new GravityLayoutEngine(settings);

        for (var i = 0; i < 120; i++)
            engine.Step(diagram, 1f / 60f);

        var violations = 0;
        foreach (var arc in diagram.Arcs)
        {
            var fromPort = diagram.TryGetPort(arc.FromPortId);
            var toPort = diagram.TryGetPort(arc.ToPortId);
            if (fromPort is null || toPort is null) continue;
            var fromNode = diagram.Nodes.Single(n => n.Id == fromPort.Ref.NodeId);
            var toNode = diagram.Nodes.Single(n => n.Id == toPort.Ref.NodeId);

            var a = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref);
            var b = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref);

            var poly = new System.Collections.Generic.List<Vector2> { a };
            foreach (var p in arc.InternalPoints) poly.Add(p);
            poly.Add(b);

            for (var si = 0; si + 1 < poly.Count; si++)
            {
                var p0 = poly[si];
                var p1 = poly[si + 1];
                foreach (var node in diagram.Nodes)
                {
                    if (node.Id == fromNode.Id || node.Id == toNode.Id) continue;
                    if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(p0, p1, node.Bounds))
                    {
                        violations++;
                        Console.WriteLine($"VIOLATION(fresh dump): arc {arc.Id} seg {si} intersects node {node.Id} -- [{p0}] -> [{p1}] node={node.Bounds}");
                    }
                }
            }
        }

        Assert.Equal(0, violations);
    }

    [Fact]
    public void CheckUserLatestAppDataDump_ForInteriorArcSegmentIntersections()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SamorodinkaTech.GravityDiagram", "dumps");
        if (!Directory.Exists(folder))
        {
            // No user dumps — pass trivially.
            return;
        }

        var file = Directory.GetFiles(folder, "gravity-dump-*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .FirstOrDefault();

        if (file is null) return;

        var json = File.ReadAllText(file);
        var dump = JsonSerializer.Deserialize<DumpRoot>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(dump);

        var diagram = BuildDiagram(dump!);
        var settings = BuildSettings(dump!);
        var engine = new GravityLayoutEngine(settings);

        for (var i = 0; i < 120; i++)
            engine.Step(diagram, 1f / 60f);

        var violations = 0;
        foreach (var arc in diagram.Arcs)
        {
            var fromPort = diagram.TryGetPort(arc.FromPortId);
            var toPort = diagram.TryGetPort(arc.ToPortId);
            if (fromPort is null || toPort is null) continue;
            var fromNode = diagram.Nodes.Single(n => n.Id == fromPort.Ref.NodeId);
            var toNode = diagram.Nodes.Single(n => n.Id == toPort.Ref.NodeId);

            var a = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref);
            var b = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref);

            var poly = new System.Collections.Generic.List<Vector2> { a };
            foreach (var p in arc.InternalPoints) poly.Add(p);
            poly.Add(b);

            for (var si = 0; si + 1 < poly.Count; si++)
            {
                var p0 = poly[si];
                var p1 = poly[si + 1];
                foreach (var node in diagram.Nodes)
                {
                    if (node.Id == fromNode.Id || node.Id == toNode.Id) continue;
                    if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(p0, p1, node.Bounds))
                    {
                        violations++;
                        Console.WriteLine($"VIOLATION(user dump): arc {arc.Id} seg {si} intersects node {node.Id} -- [{p0}] -> [{p1}] node={node.Bounds}");
                    }
                }
            }
        }

        Assert.Equal(0, violations);
    }

    private static (DumpRoot dump, float dt) LoadDump(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
        var json = File.ReadAllText(path);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var dt = root.TryGetProperty("dt", out var dtProp) ? dtProp.GetSingle() : 0.016666668f;

        var dump = JsonSerializer.Deserialize<DumpRoot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        Assert.NotNull(dump);
        return (dump!, dt);
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
            ArcPointAttractionK = s.ArcPointAttractionK,
            ArcPointMoveFactor = s.ArcPointMoveFactor,
            ArcPointNodeRepulsionK = s.ArcPointNodeRepulsionK,
            ArcPointMergeDistance = s.ArcPointMergeDistance,
            ArcPointConstraintIterations = s.ArcPointConstraintIterations,
            ArcPointExtraClearance = s.ArcPointExtraClearance,
            MaxArcInternalPoints = s.MaxArcInternalPoints,
        };
    }
}
