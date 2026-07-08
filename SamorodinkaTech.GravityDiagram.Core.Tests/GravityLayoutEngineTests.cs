using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
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
	public void NormalizeForcesByMaxMagnitude_ScalesTotalAndArcEndpointForcesRelativeToLargestVector()
	{
		var total = new[] { new Vector2(10f, 0f), new Vector2(0f, 3f) };
		var arcPointEndpoint = new[] { new Vector2(20f, 0f), new Vector2(0f, -6f) };

		var engine = new GravityLayoutEngine(new LayoutSettings { ForceNormalizationThreshold = 1f });
		var method = typeof(GravityLayoutEngine).GetMethod("NormalizeForcesByMaxMagnitude", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(method);
		method!.Invoke(engine, new object?[] { total, arcPointEndpoint });

		Assert.Equal(1f, total[0].Length(), 4);
		Assert.Equal(0.3f, total[1].Length(), 4);
		Assert.Equal(2f, arcPointEndpoint[0].Length(), 4);
		Assert.Equal(0.6f, arcPointEndpoint[1].Length(), 4);
		Assert.Equal(new Vector2(1f, 0f), total[0]);
		Assert.Equal(new Vector2(0f, 0.3f), total[1]);
		Assert.Equal(new Vector2(2f, 0f), arcPointEndpoint[0]);
		Assert.Equal(new Vector2(0f, -0.6f), arcPointEndpoint[1]);
	}

	[Fact]
	public void NormalizeForcesByMaxMagnitude_DoesNotModifyVectorsWhenLargestMagnitudeIsAtMostThreshold()
	{
		var total = new[] { new Vector2(0.5f, 0f), new Vector2(0f, -0.8f) };
		var original = total.ToArray();

		var engine = new GravityLayoutEngine(new LayoutSettings { ForceNormalizationThreshold = 1.0f });
		var method = typeof(GravityLayoutEngine).GetMethod("NormalizeForcesByMaxMagnitude", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(method);
		method!.Invoke(engine, new object?[] { total, null });

		Assert.Equal(original, total);
	}

	[Fact]
	public void Step_RoutesSavedDump_WithoutCrossingNodeInteriors()
	{
		var (dump, _) = LoadFreshDump("gravity-dump-20260519-034538.410-a78b5194031b40578f6ce2ab2862c5a5.json");
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
	public void Step_RoutesAppDataDump_WithoutCrossingNodeInteriors()
	{
		var (dump, _) = LoadFreshDump("gravity-dump-20260617-043640.529-bc529a9afaf24dd5a232d88b7a380056.json");
		Assert.NotNull(dump);
		Assert.NotNull(dump.Diagram);
		Assert.NotNull(dump.Diagram.Nodes);
		Assert.NotNull(dump.Diagram.Ports);
		Assert.NotNull(dump.Diagram.Arcs);
		
		var diagram = BuildDiagram(dump);
		var settings = BuildSettings(dump);
		
		// Validate loaded data
		Assert.NotEmpty(diagram.Nodes);
		Assert.NotEmpty(diagram.Ports);
		Assert.NotEmpty(diagram.Arcs);
		
		Console.WriteLine($"Loaded nodes: {diagram.Nodes.Count}");
		foreach (var n in diagram.Nodes)
		{
			Console.WriteLine($"  Node {n.Id}: '{n.Text}' at {n.Position}");
		}
		
		Console.WriteLine($"Loaded arcs: {diagram.Arcs.Count}");
		foreach (var a in diagram.Arcs)
		{
			Console.WriteLine($"  Arc {a.Id}: '{a.Text}'");
		}
		
		var engine = new GravityLayoutEngine(settings);

		for (var i = 0; i < 120; i++)
		{
			engine.Step(diagram, 1f / 60f);
		}

		foreach (var arc in diagram.Arcs)
		{
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			Assert.NotNull(fromPort);
			Assert.NotNull(toPort);
			var fromNode = diagram.Nodes.Single(n => n.Id == fromPort!.Ref.NodeId);
			var toNode = diagram.Nodes.Single(n => n.Id == toPort!.Ref.NodeId);

			var pts = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort!.Ref) };
			pts.AddRange(arc.InternalPoints);
			pts.Add(GravityLayoutEngine.GetPortWorldPosition(toNode, toPort!.Ref));

			for (var i = 0; i + 1 < pts.Count; i++)
			{
				var a = pts[i];
				var b = pts[i + 1];
				foreach (var node in diagram.Nodes)
				{
					Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, node.Bounds),
						$"Segment [{i}] intersects node {node.Text} interior: {a} -> {b}");
				}
			}
		}
	}

	[Fact]
	public void Step_LatestRegressionDump_DetectsCrossings()
	{
		var (dump, _) = LoadFreshDump("gravity-dump-latest-regression.json");
		var diagram = BuildDiagram(dump);
		var settings = BuildSettings(dump);
		var engine = new GravityLayoutEngine(settings);

		// Check state BEFORE first step
		CheckForCrossings(diagram, "BEFORE_FIRST_STEP");

		var maxCrossings = 0;
		var worstStep = 0;
		var worstCrossingDetails = "";

		for (var i = 0; i < 120; i++)
		{
			engine.Step(diagram, 1f / 60f);
			
			// Check after each step
			var stepCrossings = new List<(string arcText, string nodeText)>();
			foreach (var arc in diagram.Arcs)
			{
				var fromPort = diagram.TryGetPort(arc.FromPortId);
				var toPort = diagram.TryGetPort(arc.ToPortId);
				if (fromPort == null || toPort == null) continue;
				
				var fromNode = diagram.Nodes.SingleOrDefault(n => n.Id == fromPort.Ref.NodeId);
				var toNode = diagram.Nodes.SingleOrDefault(n => n.Id == toPort.Ref.NodeId);
				if (fromNode == null || toNode == null) continue;

				var pts = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref) };
				pts.AddRange(arc.InternalPoints);
				pts.Add(GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref));

				for (var j = 0; j + 1 < pts.Count; j++)
				{
					var a = pts[j];
					var b = pts[j + 1];
					foreach (var node in diagram.Nodes)
					{
						if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, node.Bounds))
						{
							stepCrossings.Add((arc.Text, node.Text));
						}
					}
				}
			}
			
			if (stepCrossings.Count > maxCrossings)
			{
				maxCrossings = stepCrossings.Count;
				worstStep = i;
				worstCrossingDetails = string.Join(", ", stepCrossings.Select(x => $"{x.arcText}→{x.nodeText}").Distinct());
			}
		}

		Assert.True(maxCrossings == 0, $"Step {worstStep}: {maxCrossings} crossings ({worstCrossingDetails})");
	}

	private void CheckForCrossings(Diagram diagram, string label)
	{
		foreach (var arc in diagram.Arcs)
		{
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			if (fromPort == null || toPort == null) continue;
			
			var fromNode = diagram.Nodes.SingleOrDefault(n => n.Id == fromPort.Ref.NodeId);
			var toNode = diagram.Nodes.SingleOrDefault(n => n.Id == toPort.Ref.NodeId);
			if (fromNode == null || toNode == null) continue;

			var pts = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref) };
			pts.AddRange(arc.InternalPoints);
			pts.Add(GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref));

			Console.WriteLine($"{label} {arc.Text}: {arc.InternalPoints.Count} internal points");
			if (arc.InternalPoints.Count > 0)
			{
				for (var i = 0; i < arc.InternalPoints.Count; i++)
					Console.WriteLine($"  [{i}] {arc.InternalPoints[i]}");
			}

			for (var i = 0; i + 1 < pts.Count; i++)
			{
				var a = pts[i];
				var b = pts[i + 1];
				foreach (var node in diagram.Nodes)
				{
					if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, node.Bounds))
					{
						Console.WriteLine($"  CROSS segment[{i}] {a} -> {b} crosses node '{node.Text}'");
					}
				}
			}
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
	public void Step_RoutesRightToLeftOnSameLine_WithoutCrossingNodeInteriors()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(-180, 0), Width = 100, Height = 60 });

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

		engine.Step(diagram, 0.001f);

		var arc = Assert.Single(diagram.Arcs);
		var fromPort = diagram.TryGetPort(arc.FromPortId);
		var toPort = diagram.TryGetPort(arc.ToPortId);
		Assert.NotNull(fromPort);
		Assert.NotNull(toPort);

		var fromNode = diagram.Nodes.Single(n => n.Id == fromPort!.Ref.NodeId);
		var toNode = diagram.Nodes.Single(n => n.Id == toPort!.Ref.NodeId);

		var points = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref) };
		points.AddRange(arc.InternalPoints);
		points.Add(GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref));

		for (var i = 0; i + 1 < points.Count; i++)
		{
			var a = points[i];
			var b = points[i + 1];
			Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, fromNode.Bounds), $"Segment [{i}] intersects source node interior: {a} -> {b}");
			Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, toNode.Bounds), $"Segment [{i}] intersects target node interior: {a} -> {b}");
		}
	}

	[Fact]
	public void Step_RoutesSingleLeftTarget_WithoutCrossingNodeInteriors()
	{
		var diagram = new Diagram();
		var nA = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var nB = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(-180, 0), Width = 100, Height = 60 });

		nA.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		nB.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var pA = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(nA.Id, RectSide.Right, 0.5f) });
		var pB = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(nB.Id, RectSide.Left, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = pA.Id, ToPortId = pB.Id });

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

		foreach (var arc in diagram.Arcs)
		{
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			Assert.NotNull(fromPort);
			Assert.NotNull(toPort);
			var fromNode = diagram.Nodes.Single(n => n.Id == fromPort!.Ref.NodeId);
			var toNode = diagram.Nodes.Single(n => n.Id == toPort!.Ref.NodeId);

			var pts = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort!.Ref) };
			pts.AddRange(arc.InternalPoints);
			pts.Add(GravityLayoutEngine.GetPortWorldPosition(toNode, toPort!.Ref));

			for (var i = 0; i + 1 < pts.Count; i++)
			{
				var a = pts[i];
				var b = pts[i + 1];
				Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, fromNode.Bounds), $"Segment [{i}] intersects source node interior: {a} -> {b}");
				Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, toNode.Bounds), $"Segment [{i}] intersects target node interior: {a} -> {b}");
			}
		}
	}

	[Fact]
	public void Step_RoutesTwoLeftTargets_WithoutCrossingNodeInteriors()
	{
		var diagram = new Diagram();
		var nA = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var nB1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B1", Position = new Vector2(-180, -40), Width = 100, Height = 60 });
		var nB2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B2", Position = new Vector2(-180, 40), Width = 100, Height = 60 });

		nA.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		nB1.SetSideFlow(RectSide.Left, PortFlow.Incoming);
		nB2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var pA = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(nA.Id, RectSide.Right, 0.5f) });
		var pB1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in1", Ref = new PortRef(nB1.Id, RectSide.Left, 0.5f) });
		var pB2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in2", Ref = new PortRef(nB2.Id, RectSide.Left, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B1", FromPortId = pA.Id, ToPortId = pB1.Id });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B2", FromPortId = pA.Id, ToPortId = pB2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 12,
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

		foreach (var arc in diagram.Arcs)
		{
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			Assert.NotNull(fromPort);
			Assert.NotNull(toPort);
			var fromNode = diagram.Nodes.Single(n => n.Id == fromPort!.Ref.NodeId);
			var toNode = diagram.Nodes.Single(n => n.Id == toPort!.Ref.NodeId);

			var pts = new List<Vector2> { GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort!.Ref) };
			pts.AddRange(arc.InternalPoints);
			pts.Add(GravityLayoutEngine.GetPortWorldPosition(toNode, toPort!.Ref));

			for (var i = 0; i + 1 < pts.Count; i++)
			{
				var a = pts[i];
				var b = pts[i + 1];
				Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, fromNode.Bounds), $"Segment [{i}] intersects source node interior: {a} -> {b}");
				Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, toNode.Bounds), $"Segment [{i}] intersects target node interior: {a} -> {b}");
			}
		}
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
			var dump = JsonSerializer.Deserialize<DumpRoot>(text, options);
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

	private static (DumpRoot dump, float dt) LoadFreshDump(string fileName)
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
