using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

/// <summary>
/// Тесты свойства RectNode.LastMovementDelta: вектор изменения позиции узла за последний Step().
/// </summary>
public sealed class LastMovementDeltaTests
{
	private static LayoutSettings ZeroForcesSettings => new()
	{
		BackgroundPairGravity = 0f,
		ConnectedArcAttractionK = 0f,
		OverlapRepulsionK = 0f,
		MinNodeSpacing = 0f,
		UseHardMinSpacing = false,
		ArcPointAttractionK = 0f,
		ArcPointMoveFactor = 0f,
		ArcPointNodeRepulsionK = 0f,
		Drag = 0f,
		Softening = 0f,
		MaxSpeed = 10000f,
	};

	/// <summary>
	/// При нулевых силах LastMovementDelta должен быть Zero.
	/// </summary>
	[Fact]
	public void LastMovementDelta_IsZero_WhenNoForces()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(100, 200), Width = 80, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(400, 200), Width = 80, Height = 60 });

		var engine = new GravityLayoutEngine(ZeroForcesSettings);
		engine.Step(diagram, 1f / 60f);

		Assert.Equal(Vector2.Zero, n1.LastMovementDelta);
		Assert.Equal(Vector2.Zero, n2.LastMovementDelta);
	}

	/// <summary>
	/// После Step() LastMovementDelta каждого узла должен равняться разнице позиций до и после.
	/// </summary>
	[Fact]
	public void LastMovementDelta_EqualsPositionChange()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(300, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			ArcPointAttractionK = 0f,
			ArcPointMoveFactor = 0f,
			ArcPointNodeRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		var pos1Before = n1.Position;
		var pos2Before = n2.Position;

		engine.Step(diagram, 1f / 60f);

		Assert.Equal(n1.Position - pos1Before, n1.LastMovementDelta);
		Assert.Equal(n2.Position - pos2Before, n2.LastMovementDelta);
	}

	/// <summary>
	/// При включённом UseHardMinSpacing LastMovementDelta должен учитывать
	/// и интеграцию, и коррекцию от HardMinSpacing.
	/// </summary>
	[Fact]
	public void LastMovementDelta_IncludesHardMinSpacingCorrection()
	{
		var diagram = new Diagram();
		// Две ноды близко друг к другу, чтобы сработал HardMinSpacing.
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(110, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 0f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 100f,
			MinNodeSpacing = 30f,
			UseHardMinSpacing = true,
			HardMinSpacingIterations = 10,
			HardMinSpacingSlop = 0f,
			ArcPointAttractionK = 0f,
			ArcPointMoveFactor = 0f,
			ArcPointNodeRepulsionK = 0f,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		var pos1Before = n1.Position;
		var pos2Before = n2.Position;

		engine.Step(diagram, 1f / 60f);

		// Ноды должны разойтись: delta должен быть ненулевым.
		Assert.NotEqual(Vector2.Zero, n1.LastMovementDelta);
		Assert.NotEqual(Vector2.Zero, n2.LastMovementDelta);

		// Delta = итоговое изменение позиции (Integrate + HardMinSpacing).
		Assert.Equal(n1.Position - pos1Before, n1.LastMovementDelta);
		Assert.Equal(n2.Position - pos2Before, n2.LastMovementDelta);
	}

	/// <summary>
	/// LastMovementDelta записывается на каждый узел, даже если у узла нет дуг.
	/// </summary>
	[Fact]
	public void LastMovementDelta_IsSet_OnDisconnectedNodes()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 80, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(300, 0), Width = 80, Height = 60 });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 100000f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			ArcPointAttractionK = 0f,
			ArcPointMoveFactor = 0f,
			ArcPointNodeRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		engine.Step(diagram, 1f);

		// Обе ноды притягиваются друг к другу — delta должен быть ненулевым.
		Assert.True(n1.LastMovementDelta.LengthSquared() > 1e-10f, "Node A should have non-zero movement delta.");
		Assert.True(n2.LastMovementDelta.LengthSquared() > 1e-10f, "Node B should have non-zero movement delta.");

		// Направление: A движется вправо, B движется влево.
		Assert.True(n1.LastMovementDelta.X > 0f, "Node A should move right toward B.");
		Assert.True(n2.LastMovementDelta.X < 0f, "Node B should move left toward A.");
	}

	/// <summary>
	/// После нескольких Step() LastMovementDelta обновляется на каждом шаге.
	/// </summary>
	[Fact]
	public void LastMovementDelta_UpdatesEachStep()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(300, 0), Width = 100, Height = 60 });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			ArcPointAttractionK = 0f,
			ArcPointMoveFactor = 0f,
			ArcPointNodeRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0.9f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		engine.Step(diagram, 1f / 60f);
		var delta1 = n1.LastMovementDelta;

		engine.Step(diagram, 1f / 60f);
		var delta2 = n1.LastMovementDelta;

		// Дельта на втором шаге должна отличаться от первой
		// (сила притяжения уменьшается по мере сближения).
		Assert.False(delta1 == delta2, "Movement delta should change between steps as positions evolve.");
	}

	/// <summary>
	/// PreviewStep не должен изменять LastMovementDelta у реальных узлов.
	/// </summary>
	[Fact]
	public void PreviewStep_DoesNotModifyLastMovementDelta()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 80, Height = 60 });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 0f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			ArcPointAttractionK = 0f,
			ArcPointMoveFactor = 0f,
			ArcPointNodeRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		var deltaBefore = n1.LastMovementDelta;

		engine.PreviewStep(diagram, 1f / 60f);

		Assert.Equal(deltaBefore, n1.LastMovementDelta);
	}

	/// <summary>
	/// Дуга корректно обновляет первый и последний сегменты
	/// когда узлы двигаются — InternalPoints должны оставаться ортогональными.
	/// </summary>
	[Fact]
	public void ArcInternalPoints_StayOrthogonal_AfterMovement()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(250, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 2f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 0.5f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		for (var step = 0; step < 5; step++)
			engine.Step(diagram, 1f / 60f);

		var arc = Assert.Single(diagram.Arcs);
		var points = arc.InternalPoints;
		var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
		var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);

		var allPoints = new System.Collections.Generic.List<Vector2> { start };
		allPoints.AddRange(points);
		allPoints.Add(end);

		for (var i = 0; i + 1 < allPoints.Count; i++)
		{
			var dx = MathF.Abs(allPoints[i + 1].X - allPoints[i].X);
			var dy = MathF.Abs(allPoints[i + 1].Y - allPoints[i].Y);
			Assert.True(dx < 0.5f || dy < 0.5f,
				$"Segment [{i}] is not axis-aligned: {allPoints[i]} -> {allPoints[i + 1]}");
		}
	}

	// ==================== Tests: LastMovementDelta used for arc recalculation ====================

	/// <summary>
	/// First segment direction matches source port side normal during simulation.
	/// For a Right port, the first segment goes rightward from the port.
	/// </summary>
	[Fact]
	public void FirstSegment_DirectionMatchesSourcePortSide_DuringSimulation()
	{
		var (diagram, engine, arc, n1, n2, p1, p2) = CreateArcDiagram(
			posA: new Vector2(0, 0), posB: new Vector2(250, 0),
			sideA: RectSide.Right, sideB: RectSide.Left);

		for (var i = 0; i < 30; i++)
		{
			engine.Step(diagram, 1f / 60f);

			var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
			var firstDir = arc.InternalPoints[0] - start;

			// Right port: first segment should go rightward.
			Assert.True(firstDir.X > -0.01f,
				$"Step {i}: first segment from Right port should go rightward, dir.X={firstDir.X}.");
		}
	}

	/// <summary>
	/// Last segment direction matches target port side normal during simulation.
	/// For a Left port, the arc approaches from the right.
	/// </summary>
	[Fact]
	public void LastSegment_DirectionMatchesTargetPortSide_DuringSimulation()
	{
		var (diagram, engine, arc, n1, n2, p1, p2) = CreateArcDiagram(
			posA: new Vector2(0, 0), posB: new Vector2(250, 0),
			sideA: RectSide.Right, sideB: RectSide.Left);

		for (var i = 0; i < 30; i++)
		{
			engine.Step(diagram, 1f / 60f);

			var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);
			var lastDir = end - arc.InternalPoints[^1];

			// Left port: arc approaches from the right (end.X > lastPoint.X).
			Assert.True(lastDir.X > -0.01f,
				$"Step {i}: last segment toward Left port should approach from right, dir.X={lastDir.X}.");
		}
	}

	/// <summary>
	/// After many simulation steps with gravity, the first segment
	/// still leaves the source port in the port's normal direction.
	/// </summary>
	[Fact]
	public void FirstSegment_DirectionMaintained_WithGravity()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(250, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		var arc = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		for (var i = 0; i < 50; i++)
			engine.Step(diagram, 1f / 60f);

		var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
		var firstDir = arc.InternalPoints[0] - start;

		// Right port: first segment goes rightward.
		Assert.True(firstDir.X > 0f,
			$"First segment should go rightward from Right port, dir.X={firstDir.X}.");
	}

	/// <summary>
	/// After many simulation steps with gravity, the last segment
	/// still approaches the target port from the correct side.
	/// </summary>
	[Fact]
	public void LastSegment_DirectionMaintained_WithGravity()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(250, 0), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		var arc = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		for (var i = 0; i < 50; i++)
			engine.Step(diagram, 1f / 60f);

		var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);
		var lastDir = end - arc.InternalPoints[^1];

		// Left port: arc approaches from the right.
		Assert.True(lastDir.X > 0f,
			$"Last segment should approach Left port from right, dir.X={lastDir.X}.");
	}

	/// <summary>
	/// After many steps with source node offset vertically,
	/// the first segment still goes in the source port's normal direction.
	/// </summary>
	[Fact]
	public void FirstSegment_DirectionMaintained_WithVerticalOffset()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, -50), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(250, 50), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		var arc = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		for (var i = 0; i < 50; i++)
			engine.Step(diagram, 1f / 60f);

		var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
		var firstDir = arc.InternalPoints[0] - start;

		// Right port: first segment goes rightward.
		Assert.True(firstDir.X > 0f,
			$"First segment should go rightward from Right port, dir.X={firstDir.X}.");
	}

	/// <summary>
	/// After many steps with Top port on source and Bottom port on target,
	/// first and last segments maintain correct port normal directions.
	/// </summary>
	[Fact]
	public void ArcEndpoints_MaintainPortNormalDirection_TopBottom()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(0, 200), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Top, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Bottom, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Top, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Bottom, 0.5f) });
		var arc = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		for (var i = 0; i < 50; i++)
			engine.Step(diagram, 1f / 60f);

		var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
		var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);
		var firstDir = arc.InternalPoints[0] - start;
		var lastDir = end - arc.InternalPoints[^1];

		// Top port: first segment goes upward (negative Y).
		Assert.True(firstDir.Y < 0.01f,
			$"First segment from Top port should go upward, dir.Y={firstDir.Y}.");
		// Bottom port: arc approaches from above (lastPoint above end → lastDir.Y < 0).
		Assert.True(lastDir.Y < 0.01f,
			$"Last segment toward Bottom port should approach from above, dir.Y={lastDir.Y}.");
	}

	// ==================== Tests: Arc internal points recalculation ====================

	/// <summary>
	/// During simulation with gravity, arc internal points remain axis-aligned
	/// after each step — no diagonal segments appear.
	/// </summary>
	[Fact]
	public void InternalPoints_StayOrthogonal_DuringContinuousSimulation()
	{
		var (diagram, engine, arc, n1, n2, p1, p2) = CreateArcDiagram(
			posA: new Vector2(0, 0), posB: new Vector2(250, 0),
			sideA: RectSide.Right, sideB: RectSide.Left);

		// Enable gravity so nodes actually move.
		engine.Settings.BackgroundPairGravity = 1f;
		engine.Settings.Drag = 0.5f;
		engine.Settings.Softening = 1f;

		for (var step = 0; step < 50; step++)
		{
			engine.Step(diagram, 1f / 60f);

			if (arc.InternalPoints.Count < 2) continue;

			var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
			var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);

			var all = new System.Collections.Generic.List<Vector2> { start };
			all.AddRange(arc.InternalPoints);
			all.Add(end);

			for (var i = 0; i + 1 < all.Count; i++)
			{
				var dx = MathF.Abs(all[i + 1].X - all[i].X);
				var dy = MathF.Abs(all[i + 1].Y - all[i].Y);
				Assert.True(dx < 0.5f || dy < 0.5f,
					$"Step {step}, segment [{i}]: not axis-aligned {all[i]} -> {all[i + 1]}");
			}
		}
	}

	/// <summary>
	/// During simulation, no arc segment should cross any node interior.
	/// </summary>
	[Fact]
	public void InternalPoints_NoNodeCrossings_DuringContinuousSimulation()
	{
		var (diagram, engine, arc, n1, n2, p1, p2) = CreateArcDiagram(
			posA: new Vector2(0, 0), posB: new Vector2(250, 0),
			sideA: RectSide.Right, sideB: RectSide.Left);

		engine.Settings.BackgroundPairGravity = 1f;
		engine.Settings.Drag = 0.5f;
		engine.Settings.Softening = 1f;

		for (var step = 0; step < 50; step++)
		{
			engine.Step(diagram, 1f / 60f);

			var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
			var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);

			var all = new System.Collections.Generic.List<Vector2> { start };
			all.AddRange(arc.InternalPoints);
			all.Add(end);

			for (var i = 0; i + 1 < all.Count; i++)
			{
				Assert.False(
					ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(all[i], all[i + 1], n1.Bounds),
					$"Step {step}, segment [{i}] crosses source node: {all[i]} -> {all[i + 1]}");
				Assert.False(
					ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(all[i], all[i + 1], n2.Bounds),
					$"Step {step}, segment [{i}] crosses target node: {all[i]} -> {all[i + 1]}");
			}
		}
	}

	/// <summary>
	/// Arc with 3 internal points: the middle point shifts less than the endpoints
	/// when both nodes move, confirming the weighted blend behavior.
	/// </summary>
	[Fact]
	public void InternalPoints_MiddlePointShiftsLessThanEndpoints()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, -80), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(250, 80), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		var arc = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		// Stabilize to get a multi-point polyline.
		for (var i = 0; i < 30; i++)
			engine.Step(diagram, 1f / 60f);

		if (arc.InternalPoints.Count < 3)
			return; // Not enough points to test — skip gracefully.

		var firstBefore = arc.InternalPoints[0];
		var midIndex = arc.InternalPoints.Count / 2;
		var midBefore = arc.InternalPoints[midIndex];
		var lastBefore = arc.InternalPoints[^1];

		// Move both nodes.
		n1.Position += new Vector2(0, -15);
		n2.Position += new Vector2(0, 15);

		engine.Step(diagram, 1f / 60f);

		var firstShift = (arc.InternalPoints[0] - firstBefore).Length();
		var midShift = (arc.InternalPoints[midIndex] - midBefore).Length();
		var lastShift = (arc.InternalPoints[^1] - lastBefore).Length();

		// Endpoints shift more than the middle (weighted blend).
		Assert.True(firstShift >= midShift - 0.1f,
			$"First point shift ({firstShift}) should be >= middle shift ({midShift}).");
		Assert.True(lastShift >= midShift - 0.1f,
			$"Last point shift ({lastShift}) should be >= middle shift ({midShift}).");
	}

	/// <summary>
	/// Arc with Top→Bottom ports: internal points shift correctly when nodes
	/// move vertically — the polyline tracks port displacement.
	/// </summary>
	[Fact]
	public void InternalPoints_TopBottomPorts_TrackVerticalMovement()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(0, 200), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Top, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Bottom, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Top, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Bottom, 0.5f) });
		var arc = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		for (var step = 0; step < 50; step++)
		{
			engine.Step(diagram, 1f / 60f);

			if (arc.InternalPoints.Count == 0) continue;

			var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
			var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);

			// First segment should go upward from Top port.
			var firstDir = arc.InternalPoints[0] - start;
			Assert.True(firstDir.Y < 0.01f,
				$"Step {step}: first segment from Top should go upward, dir.Y={firstDir.Y}.");

			// Last segment should approach Bottom port from above.
			var lastDir = end - arc.InternalPoints[^1];
			Assert.True(lastDir.Y < 0.01f,
				$"Step {step}: last segment toward Bottom should approach from above, dir.Y={lastDir.Y}.");

			// All segments axis-aligned.
			var all = new System.Collections.Generic.List<Vector2> { start };
			all.AddRange(arc.InternalPoints);
			all.Add(end);
			for (var i = 0; i + 1 < all.Count; i++)
			{
				var dx = MathF.Abs(all[i + 1].X - all[i].X);
				var dy = MathF.Abs(all[i + 1].Y - all[i].Y);
				Assert.True(dx < 0.5f || dy < 0.5f,
					$"Step {step}, segment [{i}] not axis-aligned: {all[i]} -> {all[i + 1]}");
			}
		}
	}

	/// <summary>
	/// When the target node moves far to the right, the last internal point
	/// follows — the arc stretches to track the new port position.
	/// </summary>
	[Fact]
	public void InternalPoints_LastPointFollowsTarget_WhenTargetMovesFar()
	{
		var (diagram, engine, arc, n1, n2, p1, p2) = CreateArcDiagram(
			posA: new Vector2(0, 0), posB: new Vector2(250, 0),
			sideA: RectSide.Right, sideB: RectSide.Left);

		// Stabilize.
		for (var i = 0; i < 30; i++)
			engine.Step(diagram, 1f / 60f);

		// Move target far to the right over several steps.
		for (var step = 0; step < 10; step++)
		{
			n2.Position += new Vector2(20, 0);
			engine.Step(diagram, 1f / 60f);

			var end = GravityLayoutEngine.GetPortWorldPosition(n2, p2.Ref);
			var lastPoint = arc.InternalPoints[^1];

			// The last point should have moved rightward (following the target).
			Assert.True(lastPoint.X > end.X - 30f,
				$"Step {step}: last point X ({lastPoint.X}) should be near target port X ({end.X}).");
		}
	}

	/// <summary>
	/// When the source node moves far upward, the first internal point
	/// follows — the arc stretches to track the new port position.
	/// </summary>
	[Fact]
	public void InternalPoints_FirstPointFollowsSource_WhenSourceMovesFar()
	{
		var (diagram, engine, arc, n1, n2, p1, p2) = CreateArcDiagram(
			posA: new Vector2(0, 0), posB: new Vector2(250, 0),
			sideA: RectSide.Right, sideB: RectSide.Left);

		for (var i = 0; i < 30; i++)
			engine.Step(diagram, 1f / 60f);

		// Move source far upward over several steps.
		for (var step = 0; step < 10; step++)
		{
			n1.Position += new Vector2(0, -20);
			engine.Step(diagram, 1f / 60f);

			var start = GravityLayoutEngine.GetPortWorldPosition(n1, p1.Ref);
			var firstPoint = arc.InternalPoints[0];

			// The first point should be close to the source port in Y.
			var yDist = MathF.Abs(firstPoint.Y - start.Y);
			Assert.True(yDist < 30f,
				$"Step {step}: first point Y ({firstPoint.Y}) should track source port Y ({start.Y}), dist={yDist}.");
		}
	}

	/// <summary>
	/// Multiple arcs on the same diagram: when nodes move, both arcs
	/// maintain orthogonal internal points without mutual interference.
	/// </summary>
	[Fact]
	public void MultipleArcs_BothMaintainOrthogonality()
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(250, -50), Width = 100, Height = 60 });
		var n3 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "C", Position = new Vector2(250, 50), Width = 100, Height = 60 });

		n1.SetSideFlow(RectSide.Right, PortFlow.Outgoing);
		n2.SetSideFlow(RectSide.Left, PortFlow.Incoming);
		n3.SetSideFlow(RectSide.Left, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
		var p2a = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in_a", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
		var p2b = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in_b", Ref = new PortRef(n3.Id, RectSide.Left, 0.5f) });
		var arc1 = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2a.Id });
		var arc2 = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->C", FromPortId = p1.Id, ToPortId = p2b.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 1f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0.5f,
			Softening = 1f,
			MaxSpeed = 10000f,
		});

		for (var step = 0; step < 50; step++)
		{
			engine.Step(diagram, 1f / 60f);

			foreach (var a in new[] { arc1, arc2 })
			{
				if (a.InternalPoints.Count < 2) continue;

				var fromPort = diagram.TryGetPort(a.FromPortId)!;
				var toPort = diagram.TryGetPort(a.ToPortId)!;
				var fromNode = diagram.TryGetNode(fromPort.Ref.NodeId)!;
				var toNode = diagram.TryGetNode(toPort.Ref.NodeId)!;
				var start = GravityLayoutEngine.GetPortWorldPosition(fromNode, fromPort.Ref);
				var end = GravityLayoutEngine.GetPortWorldPosition(toNode, toPort.Ref);

				var all = new System.Collections.Generic.List<Vector2> { start };
				all.AddRange(a.InternalPoints);
				all.Add(end);

				for (var i = 0; i + 1 < all.Count; i++)
				{
					var dx = MathF.Abs(all[i + 1].X - all[i].X);
					var dy = MathF.Abs(all[i + 1].Y - all[i].Y);
					Assert.True(dx < 0.5f || dy < 0.5f,
						$"Arc {a.Text}, step {step}, segment [{i}]: not axis-aligned");

					Assert.False(
						ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(all[i], all[i + 1], fromNode.Bounds),
						$"Arc {a.Text}, step {step}: segment [{i}] crosses source node");
					Assert.False(
						ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(all[i], all[i + 1], toNode.Bounds),
						$"Arc {a.Text}, step {step}: segment [{i}] crosses target node");
				}
			}
		}
	}

	// --- Helper ---

	private static (Diagram diagram, GravityLayoutEngine engine, Arc arc,
		RectNode n1, RectNode n2, Port p1, Port p2) CreateArcDiagram(
		Vector2 posA, Vector2 posB, RectSide sideA, RectSide sideB)
	{
		var diagram = new Diagram();
		var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = posA, Width = 100, Height = 60 });
		var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = posB, Width = 100, Height = 60 });

		n1.SetSideFlow(sideA, PortFlow.Outgoing);
		n2.SetSideFlow(sideB, PortFlow.Incoming);

		var p1 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, sideA, 0.5f) });
		var p2 = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, sideB, 0.5f) });
		var arc = diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A->B", FromPortId = p1.Id, ToPortId = p2.Id });

		var engine = new GravityLayoutEngine(new LayoutSettings
		{
			BackgroundPairGravity = 0f,
			ConnectedArcAttractionK = 0f,
			OverlapRepulsionK = 0f,
			MinNodeSpacing = 0f,
			UseHardMinSpacing = false,
			ArcPointAttractionK = 1f,
			ArcPointMoveFactor = 1f,
			ArcPointNodeRepulsionK = 0f,
			ArcPointConstraintIterations = 0,
			MaxArcInternalPoints = 8,
			Drag = 0f,
			Softening = 0f,
			MaxSpeed = 10000f,
		});

		return (diagram, engine, arc, n1, n2, p1, p2);
	}
}
