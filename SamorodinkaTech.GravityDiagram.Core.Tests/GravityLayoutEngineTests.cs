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
}
