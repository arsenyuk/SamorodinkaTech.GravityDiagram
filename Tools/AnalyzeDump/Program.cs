using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SamorodinkaTech.GravityDiagram.Core;

// Re-use the unified DumpRoot model from Core

class Program
{
    static int Main(string[] args)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SamorodinkaTech.GravityDiagram", "dumps");
        if (!Directory.Exists(folder))
        {
            Console.WriteLine("No dumps folder found: " + folder);
            return 2;
        }

        var file = Directory.GetFiles(folder, "gravity-dump-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (file is null)
        {
            Console.WriteLine("No dump files found in: " + folder);
            return 2;
        }

        Console.WriteLine("Analyzing dump: " + file);
        var json = File.ReadAllText(file);
        var dump = JsonSerializer.Deserialize<DumpRoot>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (dump is null)
        {
            Console.WriteLine("Failed to parse dump JSON.");
            return 2;
        }

        var diagram = BuildDiagram(dump);
        var settings = BuildSettings(dump);
        var engine = new GravityLayoutEngine(settings);

        for (var i = 0; i < 120; i++) engine.Step(diagram, 1f / 60f);

        var violations = 0;
        var perpTails = 0;

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

            // Detect perpendicular tail segments that start at port and go perpendicular
            if (arc.InternalPoints.Count > 0)
            {
                var first = arc.InternalPoints[0];
                var last = arc.InternalPoints[^1];
                var start = a;
                var end = b;
                var startNormal = GetPortNormal(fromPort.Ref.Side);
                var endNormal = GetPortNormal(toPort.Ref.Side);
                var portArcOffset = 24f;
                var startOut = start + startNormal * portArcOffset;
                var endOut = end + endNormal * portArcOffset;

                if (Vector2.DistanceSquared(first, startOut) < 0.0001f)
                {
                    Console.WriteLine($"PERP_TAIL: arc {arc.Id} has start perpendicular tail -> {first}");
                    perpTails++;
                }
                if (Vector2.DistanceSquared(last, endOut) < 0.0001f)
                {
                    Console.WriteLine($"PERP_TAIL: arc {arc.Id} has end perpendicular tail -> {last}");
                    perpTails++;
                }
            }
        }

        Console.WriteLine($"Summary: violations={violations}, perpTails={perpTails}");
        return (violations == 0) ? 0 : 1;
    }

    private static Vector2 GetPortNormal(RectSide side) => side switch
    {
        RectSide.Top => new Vector2(0f, -1f),
        RectSide.Right => new Vector2(1f, 0f),
        RectSide.Bottom => new Vector2(0f, 1f),
        RectSide.Left => new Vector2(-1f, 0f),
        _ => Vector2.Zero
    };

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
