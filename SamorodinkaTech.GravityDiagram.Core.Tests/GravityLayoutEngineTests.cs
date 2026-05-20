using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

public sealed class GravityLayoutEngineTests
{
	[Fact]
	public void Step_CreatesOrthogonalInternalPoints_FromRightToTop()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(220, -80), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Top, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Top, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 4,
			ConnectedArcAttractionK = 0f,
			BackgroundPairGravity = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		engine.Step(diagram, 0.001f);

		var arc = Assert.Single(diagram.Arcs);
		var points = arc.InternalPoints;
		Assert.True(points.Count >= 2);

		var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
		var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);
		Assert.True(points[0].X > start.X, "First internal point should move right from the source right port.");
		Assert.True(points[^1].Y < end.Y + 0.01f, "Last internal point should be above the destination top port.");
		AssertOrthogonalRoute(points, start, end, RectSide.Right, RectSide.Top);
	}

	[Fact]
	public void Step_RoutesSavedDump_WithoutCrossingNodeInteriors()
	{
		var dump = LoadFreshDump("gravity-dump-20260519-034538.410-a78b5194031b40578f6ce2ab2862c5a5.json");
		var diagram = BuildDiagram(dump);
		Assert.Equal(2, diagram.Nodes.Count);
		Assert.Single(diagram.Arcs);

		var settings = BuildSettings(dump);
		var engine = new GravityLayoutEngine(settings);

		for (var i = 0; i < 120; i++)
		{
			engine.Step(diagram, 1f / 60f);
		}

		var arc = Assert.Single(diagram.Arcs);
		var fromPort = diagram.TryGetPort(arc.FromPortId);
		var toPort = diagram.TryGetPort(arc.ToPortId);
		Assert.NotNull(fromPort);
		Assert.NotNull(toPort);

		var fromNode = diagram.Nodes.Single(n => n.Id == fromPort!.Ref.NodeId);
		var toNode = diagram.Nodes.Single(n => n.Id == toPort!.Ref.NodeId);

		var start = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort!.Ref);
		var end = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort!.Ref);
		var points = new List<Vector2> { start };
		points.AddRange(arc.InternalPoints);
		points.Add(end);

		Console.WriteLine($"From bounds: {fromNode.Bounds}");
		Console.WriteLine($"To bounds: {toNode.Bounds}");
		Console.WriteLine($"Start: {start}");
		Console.WriteLine($"End: {end}");
		Assert.True(points.Count >= 2, "Route must contain at least the source and target endpoint.");

		for (var i = 0; i < points.Count; i++)
		{
			Console.WriteLine($"Pt[{i}] = {points[i]}");
		}
		
		for (var i = 0; i + 1 < points.Count; i++)
		{
			var a = points[i];
			var b = points[i + 1];
			Assert.False(SegmentIntersectsRectInterior(a, b, fromNode.Bounds), $"Segment [{i}] intersects source node interior: {a} -> {b}");
			Assert.False(SegmentIntersectsRectInterior(a, b, toNode.Bounds), $"Segment [{i}] intersects target node interior: {a} -> {b}");
		}
	}

	[Fact]
	public void Step_CreatesHorizontalInternalPoints_ForRightToLeft()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(180, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 4,
			ConnectedArcAttractionK = 0f,
			BackgroundPairGravity = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		engine.Step(diagram, 0.001f);

		var arc = Assert.Single(diagram.Arcs);
		var points = arc.InternalPoints;
		Assert.True(points.Count >= 2);

		var first = points[0];
		var second = points[1];
		Assert.InRange(MathF.Abs(first.Y - second.Y), 0f, 0.05f);
		Assert.True(first.X < second.X, "Internal points should progress rightward for a Right->Left port connection.");
	}

	[Fact]
	public void Step_RoutesAroundTargetOnLeft_WithoutCrossingInterior()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(-180, -40), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 6,
			MaxArcInternalPoints = 8,
			ConnectedArcAttractionK = 0f,
			BackgroundPairGravity = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		for (var i = 0; i < 4; i++)
		{
			engine.Step(diagram, 0.001f);
		}

		var arc = Assert.Single(diagram.Arcs);
		var points = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref) };
		points.AddRange(arc.InternalPoints);
		points.Add(GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref));

		for (var i = 0; i + 1 < points.Count; i++)
		{
			var a = points[i];
			var b = points[i + 1];
			Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, n1.Bounds), $"Segment [{i}] intersects source node interior: {a} -> {b}");
			Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, n2.Bounds), $"Segment [{i}] intersects target node interior: {a} -> {b}");
		}
	}

	[Fact]
	public void Step_CreatesThreeBendInternalPoints_WhenRightToTopTargetIsLeftOfSource()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(-180, -80), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Top, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Top, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 6,
			ConnectedArcAttractionK = 0f,
			BackgroundPairGravity = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		engine.Step(diagram, 0.001f);

		var arc = Assert.Single(diagram.Arcs);
		var points = arc.InternalPoints;
		var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
		var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);
		Assert.True(points.Count >= 4, "A broken Right->Top route should use a three-bend connector when the target lies behind the source.");

		Assert.True(points[0].X > start.X, "First internal point should move right from the source right port.");
		Assert.True(points[^1].Y < end.Y, "Last internal point should be above the destination top port.");
	}

	[Fact]
	public void Step_RoutesAroundBlockingNode_WithoutCrossingInterior()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var blocker = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "Block", Position = new Vector2(130, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(260, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			ConnectedArcAttractionK = 0f,
			BackgroundPairGravity = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		engine.Step(diagram, 0.001f);

		var arc = Assert.Single(diagram.Arcs);
		var points = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref) };
		points.AddRange(arc.InternalPoints);
		points.Add(GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref));

		Assert.True(points.Count > 2, "Route should not remain a direct crossing connection.");

		for (var i = 0; i + 1 < points.Count; i++)
		{
			Assert.False(
				ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(points[i], points[i + 1], blocker.Bounds),
				$"Segment [{i}] intersects blocker interior: {points[i]} -> {points[i+1]}.");
		}
	}

	private static void AssertOrthogonalRoute(IReadOnlyList<Vector2> internalPoints, Vector2 start, Vector2 end, RectSide startSide, RectSide endSide)
	{
		var points = new List<Vector2>(internalPoints.Count + 2) { start };
		points.AddRange(internalPoints);
		points.Add(end);

		var startDir = ArcRoutingGeometry.SideDir(startSide);
		var endDir = ArcRoutingGeometry.SideDir(endSide);

		for (var i = 0; i + 1 < points.Count; i++)
		{
			var delta = points[i + 1] - points[i];
			Assert.True(MathF.Abs(delta.X) < 0.5f || MathF.Abs(delta.Y) < 0.5f, "Route segments should be approximately axis-aligned.");
		}

		var firstDelta = points[1] - points[0];
		Assert.True(firstDelta.X * startDir.X >= 0f && firstDelta.Y * startDir.Y >= 0f, "Route should start in the source port direction.");

		var lastDelta = points[^1] - points[^2];
		Assert.True(lastDelta.X * -endDir.X >= 0f && lastDelta.Y * -endDir.Y >= 0f, "Route should approach the destination port from the correct direction.");
	}
[Fact]
public void TestDataFiles_AreCopiedToOutputAndLoadable()
{
	var testDataFolder = Path.Combine(AppContext.BaseDirectory, "TestData");
	Assert.True(Directory.Exists(testDataFolder), $"TestData output directory missing: {testDataFolder}");

	var jsonFiles = Directory.GetFiles(testDataFolder, "*.json");
	Assert.NotEmpty(jsonFiles);

	foreach (var file in jsonFiles)
	{
		var text = File.ReadAllText(file);
		Assert.False(string.IsNullOrWhiteSpace(text), $"Dump file is empty: {file}");
		using var document = JsonDocument.Parse(text);
		Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
	}
}

	[Fact]
	public void AllDumpFiles_AreParseableAndCanBuildDiagram()
	{
		var testDataFolder = Path.Combine(AppContext.BaseDirectory, "TestData");
		var jsonFiles = Directory.GetFiles(testDataFolder, "*.json");
		Assert.NotEmpty(jsonFiles);

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		foreach (var file in jsonFiles)
		{
			var text = File.ReadAllText(file);
			var dump = JsonSerializer.Deserialize<FreshDumpRoot>(text, options);
			Assert.NotNull(dump);
			var diagram = BuildDiagram(dump!);
			var settings = BuildSettings(dump!);
			Assert.NotNull(diagram);
			Assert.NotNull(settings);
			Assert.NotEmpty(diagram.Nodes);
			Assert.NotEmpty(diagram.Ports);
			Assert.NotEmpty(diagram.Arcs);
		}
	}

	private static bool SegmentIntersectsRectInterior(Vector2 a, Vector2 b, RectF r)
	{
		if (MathF.Abs(a.X - b.X) < 0.001f || MathF.Abs(a.Y - b.Y) < 0.001f)
		{
			return ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, r);
		}

		var dx = b.X - a.X;
		var dy = b.Y - a.Y;
		var t0 = 0f;
		var t1 = 1f;

		static bool Clip(float p, float q, ref float t0, ref float t1)
		{
			if (MathF.Abs(p) < 1e-12f)
			{
				return q >= 0;
			}
			var t = q / p;
			if (p < 0)
			{
				if (t > t1) return false;
				if (t > t0) t0 = t;
			}
			else
			{
				if (t < t0) return false;
				if (t < t1) t1 = t;
			}
			return true;
		}

		if (!Clip(-dx, a.X - r.Left, ref t0, ref t1)) return false;
		if (!Clip(dx, r.Right - a.X, ref t0, ref t1)) return false;
		if (!Clip(-dy, a.Y - r.Top, ref t0, ref t1)) return false;
		if (!Clip(dy, r.Bottom - a.Y, ref t0, ref t1)) return false;

		if (t1 <= t0) return false;

		var midT = (t0 + t1) * 0.5f;
		var mid = new Vector2(a.X + dx * midT, a.Y + dy * midT);
		return mid.X > r.Left && mid.X < r.Right && mid.Y > r.Top && mid.Y < r.Bottom;
	}

	private static FreshDumpRoot LoadFreshDump(string fileName)
	{
		var path = Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
		var json = File.ReadAllText(path);
		var dump = JsonSerializer.Deserialize<FreshDumpRoot>(json, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
		});

		Assert.NotNull(dump);
		return dump!;
	}

	private static Diagram BuildDiagram(FreshDumpRoot dump)
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

	private static LayoutSettings BuildSettings(FreshDumpRoot dump)
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

	private sealed record FreshDumpRoot(DumpSettingsV5 Settings, DumpDiagramV5 Diagram);
	private sealed record DumpSettingsV5(
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
		float MaxSpeed,
		float ArcPointAttractionK,
		float ArcPointMoveFactor,
		float ArcPointNodeRepulsionK,
		float ArcPointMergeDistance,
		int ArcPointConstraintIterations,
		float ArcPointExtraClearance,
		int MaxArcInternalPoints);
	private sealed record DumpDiagramV5(DumpNodeV5[] Nodes, DumpPortV5[] Ports, DumpArcV5[] Arcs);
	private sealed record DumpNodeV5(string Id, string? Text, DumpVec2 Position, DumpVec2 Velocity, float Width, float Height);
	private sealed record DumpPortV5(string Id, string? Text, string NodeId, string Side, float Offset, float ClampedOffset, DumpVec2? WorldPosition);
	private sealed record DumpArcV5(string Id, string? Text, string FromPortId, string ToPortId, DumpVec2[] InternalPoints, DumpVec2[] InternalPointForces);
	private sealed record DumpVec2(float X, float Y);

	[Fact]
	public void Step_RoutesAroundBlockingNode_WithoutCrossingAnyNodeInterior()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var blocker = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "Block", Position = new Vector2(130, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(260, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 1f,
			ArcPointConstraintIterations = 6,
			MaxArcInternalPoints = 16,
			ConnectedArcAttractionK = 0f,
			BackgroundPairGravity = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		engine.Step(diagram, 0.001f);

		var arc = Assert.Single(diagram.Arcs);
		var points = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref) };
		points.AddRange(arc.InternalPoints);
		points.Add(GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref));

		for (var i = 0; i + 1 < points.Count; i++)
		{
			Assert.False(
				SegmentIntersectsRectInterior(points[i], points[i + 1], blocker.Bounds),
				$"Segment [{i}] intersects blocking node interior: {points[i]} -> {points[i + 1]}.");
		}
	}
}
