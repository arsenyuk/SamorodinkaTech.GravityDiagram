using System.Collections.Generic;
using System.Numerics;
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
		Assert.True(points.Count >= 4, "A broken Right->Top route should use a three-bend connector when the target lies behind the source.");

		var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
		var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);
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
