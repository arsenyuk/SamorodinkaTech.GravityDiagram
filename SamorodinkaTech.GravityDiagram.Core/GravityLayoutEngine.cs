using System.Collections.ObjectModel;
using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;
public enum ArcType
{
	Polyline,
	Straight
}

public class ArcLayoutOptions
{
	public ArcType Type { get; set; } = ArcType.Polyline;
	public bool FixFirstPointToNormal { get; set; } = true;
	public bool FixLastPointToNormal { get; set; } = true;
}

public sealed class GravityLayoutEngine
{
	// Static method for orthogonal polyline generation.
	private static void MakeOrthogonalPolyline(Vector2 start, Vector2 end, RectSide startSide, RectSide endSide, List<Vector2> internalPoints)
	{
		const float portArcOffset = 24f;
		var startDir = ArcRoutingGeometry.SideDir(startSide);
		var endDir = ArcRoutingGeometry.SideDir(endSide);
		var startOut = start + startDir * portArcOffset;
		var endOut = end + endDir * portArcOffset;

		internalPoints.Clear();
		internalPoints.Add(startOut);

		// Direct axis-aligned connection from the port exits.
		if (MathF.Abs(startOut.X - endOut.X) < 0.001f || MathF.Abs(startOut.Y - endOut.Y) < 0.001f)
		{
			var delta = endOut - startOut;
			if (IsDirectionCompatible(delta, startDir) && IsDirectionCompatible(-delta, endDir))
			{
				internalPoints.Add(endOut);
				return;
			}
		}

		// Try both L-shaped candidates first.
		var firstCandidate = GetOrthogonalCorner(startOut, endOut, startAxisFirst: true);
		if (IsValidOrthogonalCorner(startOut, firstCandidate, endOut, startDir, endDir))
		{
			internalPoints.Add(firstCandidate);
			internalPoints.Add(endOut);
			return;
		}

		var secondCandidate = GetOrthogonalCorner(startOut, endOut, startAxisFirst: false);
		if (IsValidOrthogonalCorner(startOut, secondCandidate, endOut, startDir, endDir))
		{
			internalPoints.Add(secondCandidate);
			internalPoints.Add(endOut);
			return;
		}

		// If a simple L-shaped path would require reversing direction, fall back to a three-bend route.
		if (MathF.Abs(startDir.X) > 0.001f)
		{
			var detourX = startOut.X + startDir.X * portArcOffset;
			var approachY = endOut.Y + (MathF.Abs(endDir.Y) > 0.001f
				? endDir.Y * portArcOffset
				: (startOut.Y <= endOut.Y ? -portArcOffset : portArcOffset));
			internalPoints.Add(new Vector2(detourX, startOut.Y));
			internalPoints.Add(new Vector2(detourX, approachY));
			internalPoints.Add(new Vector2(endOut.X, approachY));
		}
		else
		{
			var detourY = startOut.Y + startDir.Y * portArcOffset;
			var approachX = endOut.X + (MathF.Abs(endDir.X) > 0.001f
				? endDir.X * portArcOffset
				: (startOut.X <= endOut.X ? -portArcOffset : portArcOffset));
			internalPoints.Add(new Vector2(startOut.X, detourY));
			internalPoints.Add(new Vector2(approachX, detourY));
			internalPoints.Add(new Vector2(approachX, endOut.Y));
		}

		internalPoints.Add(endOut);
	}

	private static Vector2 GetOrthogonalCorner(Vector2 startOut, Vector2 endOut, bool startAxisFirst)
	{
		if (startAxisFirst)
		{
			return startOut.X != endOut.X
				? new Vector2(endOut.X, startOut.Y)
				: new Vector2(startOut.X, endOut.Y);
		}

		return startOut.Y != endOut.Y
			? new Vector2(startOut.X, endOut.Y)
			: new Vector2(endOut.X, startOut.Y);
	}

	private static bool IsValidOrthogonalCorner(Vector2 startOut, Vector2 corner, Vector2 endOut, Vector2 startDir, Vector2 endDir)
	{
		var firstSegment = corner - startOut;
		var secondSegment = endOut - corner;
		return IsDirectionCompatible(firstSegment, startDir) && IsDirectionCompatible(secondSegment, -endDir);
	}

	private static bool IsDirectionCompatible(Vector2 delta, Vector2 direction)
	{
		if (MathF.Abs(direction.X) > 0.001f)
			return MathF.Abs(delta.Y) < 0.001f && delta.X * direction.X >= 0f;
		return MathF.Abs(delta.X) < 0.001f && delta.Y * direction.Y >= 0f;
	}
	private readonly LayoutSettings _settings;
	private readonly Dictionary<DiagramId, Vector2> _lastForcesByNodeId = new();
	private readonly Dictionary<DiagramId, Vector2[]> _lastArcPointForcesByArcId = new();

	private void NormalizeForcesByMaxMagnitude(Vector2[] total, Vector2[]? arcPointEndpoint = null)
	{
		if (total.Length == 0) return;

		var maxLength = 0f;
		for (var i = 0; i < total.Length; i++)
		{
			maxLength = MathF.Max(maxLength, total[i].Length());
		}

		var threshold = MathF.Max(0f, _settings.ForceNormalizationThreshold);
		if (maxLength <= threshold) return;

		for (var i = 0; i < total.Length; i++)
		{
			total[i] /= maxLength;
			if (arcPointEndpoint is not null)
				arcPointEndpoint[i] /= maxLength;
		}
	}

	public GravityLayoutEngine(LayoutSettings? settings = null)
	{
		_settings = settings ?? new LayoutSettings();
	}

	public LayoutSettings Settings => _settings;
	public IReadOnlyDictionary<DiagramId, Vector2> LastForcesByNodeId => _lastForcesByNodeId;
	public IReadOnlyDictionary<DiagramId, Vector2[]> LastArcPointForcesByArcId => _lastArcPointForcesByArcId;

	public LayoutStepPreview PreviewStep(Diagram diagram, float dt)
	{
		ArgumentNullException.ThrowIfNull(diagram);
		if (dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt), "dt must be > 0.");

		var nodes = diagram.Nodes;
		if (nodes.Count == 0)
		{
			return new LayoutStepPreview(
				CreatedAtUtc: DateTimeOffset.UtcNow,
				Dt: dt,
				Nodes: Array.Empty<NodeStepPreview>(),
				Arcs: Array.Empty<ArcStepPreview>(),
				SumBackgroundGravityForce: Vector2.Zero,
				SumOverlapRepulsionForce: Vector2.Zero,
				SumConnectedArcAttractionForce: Vector2.Zero,
				SumArcPointEndpointForce: Vector2.Zero,
				SumTotalForce: Vector2.Zero);
		}

		var nodeIndexById = new Dictionary<DiagramId, int>(nodes.Count);
		for (var i = 0; i < nodes.Count; i++)
		{
			nodeIndexById[nodes[i].Id] = i;
		}

		var total = new Vector2[nodes.Count];
		var background = new Vector2[nodes.Count];
		var overlap = new Vector2[nodes.Count];
		var arcSprings = new Vector2[nodes.Count];
		var arcPointEndpoint = new Vector2[nodes.Count];

		var arcPreviews = ComputeForces(diagram, nodes, nodeIndexById, total, background, overlap, arcSprings, arcPointEndpoint);

		// Arc internal points are massless and their reaction forces are not applied back to nodes.
		// Normalize the force vectors relative to the largest magnitude so strong forces remain dominant.
		NormalizeForcesByMaxMagnitude(total, arcPointEndpoint);

		// Clone nodes for a non-mutating prediction.
		var predicted = new List<RectNode>(nodes.Count);
		for (var i = 0; i < nodes.Count; i++)
		{
			var n = nodes[i];
			predicted.Add(new RectNode
			{
				Id = n.Id,
				Text = n.Text,
				Position = n.Position,
				Velocity = n.Velocity,
				Width = n.Width,
				Height = n.Height,
			});
		}

		// Clone nodes for a "no forces" prediction (only inertia + drag + constraints).
		var predictedNoForces = new List<RectNode>(nodes.Count);
		for (var i = 0; i < nodes.Count; i++)
		{
			var n = nodes[i];
			predictedNoForces.Add(new RectNode
			{
				Id = n.Id,
				Text = n.Text,
				Position = n.Position,
				Velocity = n.Velocity,
				Width = n.Width,
				Height = n.Height,
			});
		}

		// Predict integration.
		var predictedBeforeConstraints = new Vector2[nodes.Count];
		var predictedVelocityBeforeConstraints = new Vector2[nodes.Count];
		Integrate(predicted.AsReadOnly(), total, dt);

		// Predict with zero forces.
		var zeros = new Vector2[nodes.Count];
		Integrate(predictedNoForces.AsReadOnly(), zeros, dt);
		ApplyHardMinSpacing(predictedNoForces.AsReadOnly());
		var predictedNoForcesPos = new Vector2[nodes.Count];
		var predictedNoForcesVel = new Vector2[nodes.Count];
		for (var i = 0; i < predictedNoForces.Count; i++)
		{
			predictedNoForcesPos[i] = predictedNoForces[i].Position;
			predictedNoForcesVel[i] = predictedNoForces[i].Velocity;
		}

		for (var i = 0; i < predicted.Count; i++)
		{
			predictedBeforeConstraints[i] = predicted[i].Position;
			predictedVelocityBeforeConstraints[i] = predicted[i].Velocity;
		}

		// Predict hard constraints.
		ApplyHardMinSpacing(predicted.AsReadOnly());

		var previews = new NodeStepPreview[nodes.Count];
		var usedMass = MathF.Max(0.000001f, _settings.NodeMass);
		var sumTotal = Vector2.Zero;
		var sumBackground = Vector2.Zero;
		var sumOverlap = Vector2.Zero;
		var sumArc = Vector2.Zero;
		var sumArcPointEndpoint = Vector2.Zero;
		for (var i = 0; i < nodes.Count; i++)
		{
			var n = nodes[i];
			var p = predicted[i];
			sumTotal += total[i];
			sumBackground += background[i];
			sumOverlap += overlap[i];
			sumArc += arcSprings[i];
			sumArcPointEndpoint += arcPointEndpoint[i];
			previews[i] = new NodeStepPreview(
				Id: n.Id,
				Position: n.Position,
				Velocity: n.Velocity,
				Mass: usedMass,
				ForceBackgroundGravity: background[i],
				ForceOverlapRepulsion: overlap[i],
				ForceConnectedArcAttraction: arcSprings[i],
				ForceArcPointEndpoint: arcPointEndpoint[i],
				ForceTotal: total[i],
				PredictedPositionIfNoForces: predictedNoForcesPos[i],
				PredictedVelocityIfNoForces: predictedNoForcesVel[i],
				DeltaPositionIfNoForces: predictedNoForcesPos[i] - n.Position,
				PredictedPositionBeforeConstraints: predictedBeforeConstraints[i],
				PredictedVelocityBeforeConstraints: predictedVelocityBeforeConstraints[i],
				PredictedPosition: p.Position,
				PredictedVelocity: p.Velocity,
				DeltaPositionBeforeConstraints: predictedBeforeConstraints[i] - n.Position,
				DeltaPosition: p.Position - n.Position);
		}

		return new LayoutStepPreview(
			CreatedAtUtc: DateTimeOffset.UtcNow,
			Dt: dt,
			Nodes: previews,
			Arcs: arcPreviews,
			SumBackgroundGravityForce: sumBackground,
			SumOverlapRepulsionForce: sumOverlap,
			SumConnectedArcAttractionForce: sumArc,
			SumArcPointEndpointForce: sumArcPointEndpoint,
			SumTotalForce: sumTotal);
	}

	public void Step(Diagram diagram, float dt)
	{
		ArgumentNullException.ThrowIfNull(diagram);
		if (dt <= 0) return;

		var nodes = diagram.Nodes;
		if (nodes.Count == 0) return;

		var forces = new Vector2[nodes.Count];
		var arcPointEndpoint = new Vector2[nodes.Count];
		var nodeIndexById = new Dictionary<DiagramId, int>(nodes.Count);
		for (var i = 0; i < nodes.Count; i++)
		{
			nodeIndexById[nodes[i].Id] = i;
		}

		// Cache arc endpoint positions before node integration.
		var endpointsBefore = new Dictionary<DiagramId, (Vector2 Start, Vector2 End)>(diagram.Arcs.Count);
		for (var ai = 0; ai < diagram.Arcs.Count; ai++)
		{
			var arc = diagram.Arcs[ai];
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			if (fromPort is null || toPort is null) continue;
			if (!nodeIndexById.TryGetValue(fromPort.Ref.NodeId, out var ia)) continue;
			if (!nodeIndexById.TryGetValue(toPort.Ref.NodeId, out var ib)) continue;
			var start = GetPortWorldPosition(nodes[ia], fromPort.Ref);
			var end = GetPortWorldPosition(nodes[ib], toPort.Ref);
			endpointsBefore[arc.Id] = (start, end);
		}

		ComputeForces(diagram, nodes, nodeIndexById, forces, arcPointEndpoint: arcPointEndpoint);

		// Normalize force magnitudes so large forces stay dominant and small noise does not drive the system.
		NormalizeForcesByMaxMagnitude(forces, arcPointEndpoint);

		// Update arc internal points (massless points, no velocity).
		// This step can apply additional reaction forces back to nodes (e.g. arc-point vs node repulsion),
		// which may reintroduce a small non-zero net translation component.
		_lastArcPointForcesByArcId.Clear();
		StepArcInternalPoints(diagram, nodes, nodeIndexById, forces, dt);
		NormalizeForcesByMaxMagnitude(forces);

		_lastForcesByNodeId.Clear();
		for (var i = 0; i < nodes.Count; i++)
		{
			_lastForcesByNodeId[nodes[i].Id] = forces[i];
		}

		Integrate(nodes, forces, dt);
		ApplyHardMinSpacing(nodes);

		// Nodes moved after arc points were stepped; keep internal points consistent with updated endpoints.
		// Without this, a tiny segment can appear near ports due to endpoint motion.
		for (var ai = 0; ai < diagram.Arcs.Count; ai++)
		{
			var arc = diagram.Arcs[ai];
			var internalPoints = arc.InternalPoints;
			if (internalPoints.Count == 0) continue;

			if (!endpointsBefore.TryGetValue(arc.Id, out var before))
				continue;

			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			if (fromPort is null || toPort is null) continue;
			if (!nodeIndexById.TryGetValue(fromPort.Ref.NodeId, out var ia)) continue;
			if (!nodeIndexById.TryGetValue(toPort.Ref.NodeId, out var ib)) continue;

			var startAfter = GetPortWorldPosition(nodes[ia], fromPort.Ref);
			var endAfter = GetPortWorldPosition(nodes[ib], toPort.Ref);
			var deltaStart = startAfter - before.Start;
			var deltaEnd = endAfter - before.End;

			if (deltaStart.LengthSquared() < 0.000001f && deltaEnd.LengthSquared() < 0.000001f)
			{
				CleanupEndpointTails(internalPoints, startAfter, endAfter);
				continue;
			}

			var n = internalPoints.Count;
			for (var i = 0; i < n; i++)
			{
				var t = (i + 1f) / (n + 1f);
				internalPoints[i] += deltaStart * (1f - t) + deltaEnd * t;
			}

			RepairArcAgainstNodes(arc, nodes, ia, ib, startAfter, endAfter, GetArcPointClearance(), Math.Clamp(_settings.MaxArcInternalPoints, 0, 512));
			EnsureOrthogonalInternalPoints(internalPoints, nodes);
			CleanupEndpointTails(internalPoints, startAfter, endAfter);
		}
	}

	private IReadOnlyList<ArcStepPreview> ComputeForces(
		Diagram diagram,
		ReadOnlyCollection<RectNode> nodes,
		Dictionary<DiagramId, int> nodeIndexById,
		Vector2[] total,
		Vector2[]? background = null,
		Vector2[]? overlap = null,
		Vector2[]? arcSprings = null,
		Vector2[]? arcPointEndpoint = null)
	{
		ApplyBackgroundGravity(nodes, total, background);
		ApplyOverlapRepulsion(nodes, total, overlap);
		// Restore node-to-node arc spring attraction to prevent nodes from flying apart.
		ApplyArcSprings(diagram, nodes, nodeIndexById, total, arcSprings);
		var arcPreviews = ApplyArcPointForces(diagram, nodes, nodeIndexById, total, arcPointEndpoint);
		return arcPreviews;
	}

	private float GetArcPointClearance()
	{
		// Arc internal points have no thickness; clearance is controlled by explicit arc point margin only.
		return MathF.Max(0f, _settings.ArcPointExtraClearance);
	}

	private IReadOnlyList<ArcStepPreview> ApplyArcPointForces(
		Diagram diagram,
		ReadOnlyCollection<RectNode> nodes,
		Dictionary<DiagramId, int> nodeIndexById,
		Vector2[] total,
		Vector2[]? arcPointEndpoint)
	{
		var previews = new List<ArcStepPreview>(diagram.Arcs.Count);
		var k = MathF.Max(0f, _settings.ArcPointAttractionK);
		var repulseK = MathF.Max(0f, _settings.ArcPointNodeRepulsionK);
		var clearance = GetArcPointClearance();
		if (k <= 0f)
		{
			foreach (var a in diagram.Arcs)
			{
				previews.Add(new ArcStepPreview(a.Id, a.FromPortId, a.ToPortId, Array.Empty<ArcPointStepPreview>()));
			}
			return previews;
		}

		foreach (var arc in diagram.Arcs)
		{
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			if (fromPort is null || toPort is null)
			{
				previews.Add(new ArcStepPreview(arc.Id, arc.FromPortId, arc.ToPortId, Array.Empty<ArcPointStepPreview>()));
				continue;
			}

			if (!nodeIndexById.TryGetValue(fromPort.Ref.NodeId, out var ia) || !nodeIndexById.TryGetValue(toPort.Ref.NodeId, out var ib))
			{
				previews.Add(new ArcStepPreview(arc.Id, arc.FromPortId, arc.ToPortId, Array.Empty<ArcPointStepPreview>()));
				continue;
			}

			var fromNode = nodes[ia];
			var toNode = nodes[ib];
			var start = GetPortWorldPosition(fromNode, fromPort.Ref);
			var end = GetPortWorldPosition(toNode, toPort.Ref);

			var internalPoints = arc.InternalPoints;
			var forces = new Vector2[internalPoints.Count];

			Vector2 GetPoint(int index)
			{
				if (index == -1) return start;
				if (index == internalPoints.Count) return end;
				return internalPoints[index];
			}

			// Adjacent attraction along the polyline chain.
			for (var segIndex = -1; segIndex < internalPoints.Count; segIndex++)
			{
				var aPos = GetPoint(segIndex);
				var bPos = GetPoint(segIndex + 1);
				var delta = bPos - aPos;
				var dist = delta.Length();
				if (dist < 0.0001f) continue;
				var dir = delta / dist;
				var f = dir * (k * dist);

				// Force on A towards B is +f, on B towards A is -f.
				if (segIndex >= 0)
				{
					forces[segIndex] += f;
				}
				else
				{
					total[ia] += f;
					if (arcPointEndpoint is not null) arcPointEndpoint[ia] += f;
				}

				if (segIndex + 1 < internalPoints.Count)
				{
					forces[segIndex + 1] -= f;
				}
				else
				{
					total[ib] -= f;
					if (arcPointEndpoint is not null) arcPointEndpoint[ib] -= f;
				}
			}

			// Repulsion from node bounds (expanded by clearance) proportional to zone violation.
			if (repulseK > 0f)
			{
				for (var i = 0; i < internalPoints.Count; i++)
				{
					var p = internalPoints[i];
					for (var n = 0; n < nodes.Count; n++)
					{
						var r = Expand(nodes[n].Bounds, clearance);
						if (!r.Contains(p)) continue;
						var f = ComputePointRepulsionFromRect(p, r) * repulseK;
						forces[i] += f;
						// Action-reaction: node experiences equal and opposite.
						total[n] -= f;
					}
				}
			}

			var ptsPreview = new ArcPointStepPreview[internalPoints.Count];
			for (var i = 0; i < internalPoints.Count; i++)
			{
				ptsPreview[i] = new ArcPointStepPreview(Index: i, Position: internalPoints[i], Force: forces[i]);
			}
			previews.Add(new ArcStepPreview(arc.Id, arc.FromPortId, arc.ToPortId, ptsPreview));
		}

		return previews;
	}

	private static Vector2 ComputePointRepulsionFromRect(Vector2 p, RectF r)
	{
		// p is assumed inside r. Return a vector pointing outward with magnitude equal
		// to the minimal distance to exit the rectangle (zone violation).
		var toLeft = p.X - r.Left;
		var toRight = r.Right - p.X;
		var toTop = p.Y - r.Top;
		var toBottom = r.Bottom - p.Y;

		var min = toLeft;
		var side = 0; // 0=left 1=right 2=top 3=bottom
		if (toRight < min) { min = toRight; side = 1; }
		if (toTop < min) { min = toTop; side = 2; }
		if (toBottom < min) { min = toBottom; side = 3; }

		return side switch
		{
			0 => new Vector2(-(min + 0.001f), 0f),
			1 => new Vector2((min + 0.001f), 0f),
			2 => new Vector2(0f, -(min + 0.001f)),
			3 => new Vector2(0f, (min + 0.001f)),
			_ => Vector2.Zero,
		};
	}

	private void StepArcInternalPoints(
		Diagram diagram,
		ReadOnlyCollection<RectNode> nodes,
		Dictionary<DiagramId, int> nodeIndexById,
		Vector2[] nodeForces,
		float dt,
		ArcLayoutOptions? arcOptions = null)
	{
		var k = MathF.Max(0f, _settings.ArcPointAttractionK);
		var repulseK = MathF.Max(0f, _settings.ArcPointNodeRepulsionK);
		var moveFactor = MathF.Max(0f, _settings.ArcPointMoveFactor);
		if (k <= 0f || moveFactor <= 0f) return;

		var clearance = GetArcPointClearance();
		var constraintIterations = Math.Clamp(_settings.ArcPointConstraintIterations, 0, 50);
		var mergeDistance = MathF.Max(0f, _settings.ArcPointMergeDistance);
		var mergeDistance2 = mergeDistance * mergeDistance;
		var maxInternal = Math.Clamp(_settings.MaxArcInternalPoints, 0, 512);
		var epsilon = 0.001f;

		foreach (var arc in diagram.Arcs)
		{
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			if (fromPort is null || toPort is null) continue;
			if (!nodeIndexById.TryGetValue(fromPort.Ref.NodeId, out var ia) || !nodeIndexById.TryGetValue(toPort.Ref.NodeId, out var ib))
				continue;
			var fromNode = nodes[ia];
			var toNode = nodes[ib];
			var start = GetPortWorldPosition(fromNode, fromPort.Ref);
			var end = GetPortWorldPosition(toNode, toPort.Ref);
			var options = arcOptions ?? new ArcLayoutOptions();
			var internalPoints = arc.InternalPoints;
			const float portArcOffset = 24f;
			var startSide = fromPort.Ref.Side;
			var endSide = toPort.Ref.Side;
			Vector2 startNormal = startSide switch
			{
				RectSide.Top => new Vector2(0f, -1f),
				RectSide.Right => new Vector2(1f, 0f),
				RectSide.Bottom => new Vector2(0f, 1f),
				RectSide.Left => new Vector2(-1f, 0f),
				_ => Vector2.Zero
			};
			Vector2 endNormal = endSide switch
			{
				RectSide.Top => new Vector2(0f, -1f),
				RectSide.Right => new Vector2(1f, 0f),
				RectSide.Bottom => new Vector2(0f, 1f),
				RectSide.Left => new Vector2(-1f, 0f),
				_ => Vector2.Zero
			};
			if (options.Type == ArcType.Straight)
			{
				internalPoints.Clear();
				if (options.FixFirstPointToNormal)
					internalPoints.Add(start + startNormal * portArcOffset);
				if (options.FixLastPointToNormal)
					internalPoints.Add(end + endNormal * portArcOffset);
				continue;
			}

			// Ортогональная ломаная: только горизонтальные и вертикальные сегменты
			internalPoints.Clear();
			MakeOrthogonalPolyline(start, end, startSide, endSide, internalPoints);
			if (internalPoints.Count > maxInternal)
				internalPoints.RemoveRange(maxInternal, internalPoints.Count - maxInternal);
			// Убрана фиксация крайних точек по нормали

			// IMMEDIATE REPAIR: Fix violations in the initial polyline before applying forces
			RepairArcAgainstNodes(arc, nodes, ia, ib, start, end, clearance, maxInternal);
			PushInternalPointsOutOfNodes(internalPoints, nodes, clearance);

			var forces = new Vector2[internalPoints.Count];

			Vector2 GetPoint(int index)
			{
				if (index == -1) return start;
				if (index == internalPoints.Count) return end;
				return internalPoints[index];
			}

			for (var segIndex = -1; segIndex < internalPoints.Count; segIndex++)
			{
				var aPos = GetPoint(segIndex);
				var bPos = GetPoint(segIndex + 1);
				var delta = bPos - aPos;
				var dist = delta.Length();
				if (dist < 0.0001f) continue;
				var dir = delta / dist;
				var f = dir * (k * dist);
				if (segIndex >= 0) forces[segIndex] += f;
				if (segIndex + 1 < internalPoints.Count) forces[segIndex + 1] -= f;
			}



			// Repulsion from node bounds expanded by clearance, proportional to penetration.
			if (repulseK > 0f)
			{
				for (var i = 0; i < internalPoints.Count; i++)
				{
					var p = internalPoints[i];
					for (var n = 0; n < nodes.Count; n++)
					{
						var r = Expand(nodes[n].Bounds, clearance);
						if (!r.Contains(p)) continue;
						var f = ComputePointRepulsionFromRect(p, r) * repulseK;
						forces[i] += f;
						// Action-reaction: node experiences equal and opposite.
						nodeForces[n] -= f;
					}
				}
			}

			// Apply movement (massless: position += force * factor * dt).
			for (var i = 0; i < internalPoints.Count; i++)
			{
				internalPoints[i] += forces[i] * (moveFactor * dt);
			}

			// Hard constraints: keep internal points outside clearance rectangles.
			for (var it = 0; it < constraintIterations; it++)
			{
				var any = false;
				for (var i = 0; i < internalPoints.Count; i++)
				{
					var p = internalPoints[i];
					for (var n = 0; n < nodes.Count; n++)
					{
						var r = Expand(nodes[n].Bounds, clearance);
						if (!r.Contains(p)) continue;
						any = true;
						internalPoints[i] = PushPointOutOfRect(p, r, epsilon);
						p = internalPoints[i];
					}
				}
				if (!any) break;
			}

			// Repair segments that cross clearance rectangles by inserting a waypoint.
			RepairArcAgainstNodes(arc, nodes, ia, ib, start, end, clearance, maxInternal);
			SimplifyInternalPoints(internalPoints);
			PushInternalPointsOutOfNodes(internalPoints, nodes, clearance);

			// Reinforce axis alignment after force-based movement.
			EnsureOrthogonalInternalPoints(internalPoints, nodes);
			SimplifyInternalPoints(internalPoints);
			PushInternalPointsOutOfNodes(internalPoints, nodes, clearance);

			// Repair again after orthogonalization to catch any new endpoint intersections.
			RepairArcAgainstNodes(arc, nodes, ia, ib, start, end, clearance, maxInternal);
			SimplifyInternalPoints(internalPoints);
			PushInternalPointsOutOfNodes(internalPoints, nodes, clearance);

			// Ensure the final route remains axis-aligned after the last repair.
			EnsureOrthogonalInternalPoints(internalPoints, nodes);
			SimplifyInternalPoints(internalPoints);
			PushInternalPointsOutOfNodes(internalPoints, nodes, clearance);

			// Final repair pass in case orthogonalization introduced a new clearance violation.
			RepairArcAgainstNodes(arc, nodes, ia, ib, start, end, clearance, maxInternal);
			SimplifyInternalPoints(internalPoints);
			PushInternalPointsOutOfNodes(internalPoints, nodes, clearance);
			EnsureOrthogonalInternalPoints(internalPoints, nodes);
			SimplifyInternalPoints(internalPoints);
			PushInternalPointsOutOfNodes(internalPoints, nodes, clearance);

			// Merge adjacent internal points.
			if (mergeDistance > 0f)
			{
				var i = 0;
				while (i + 1 < internalPoints.Count)
				{
					if (Vector2.DistanceSquared(internalPoints[i], internalPoints[i + 1]) <= mergeDistance2)
					{
						internalPoints[i] = (internalPoints[i] + internalPoints[i + 1]) * 0.5f;
						internalPoints.RemoveAt(i + 1);
						continue;
					}
					i++;
				}
			}

			// Align the first and last internal points with the source and target port directions.
			EnsureOrthogonalEndpoints(start, end, startSide, endSide, internalPoints);

			// Remove redundant endpoint-adjacent points that create a visible "tail" near ports.
			CleanupEndpointTails(internalPoints, start, end);

			// Keep debug force array shape consistent with the final internal point list.
			_lastArcPointForcesByArcId[arc.Id] = (forces.Length == internalPoints.Count)
				? forces
				: new Vector2[internalPoints.Count];
		}
	}

	private static void CleanupEndpointTails(List<Vector2> internalPoints, Vector2 start, Vector2 end)
	{
		if (internalPoints.Count == 0) return;

		// Remove exact duplicates / extremely tiny segments.
		for (var i = internalPoints.Count - 2; i >= 0; i--)
		{
			if (Vector2.DistanceSquared(internalPoints[i], internalPoints[i + 1]) < 0.0001f)
				internalPoints.RemoveAt(i + 1);
		}

		// Don't remove endpoint offsets for the simplest minimal routed paths.
		// These port offsets are required to keep the arc from collapsing to a straight line.
		if (internalPoints.Count <= 2)
		{
			return;
		}

		// If the first/last internal point sits almost on the straight segment to its neighbor and is
		// very short, it becomes a tiny visible stub; drop it.
		// Heuristics:
		// - If the first internal point is axis-aligned from `start` and lies on the same axis
		//   as the next point, and its distance from the `start` is small, then it's likely a
		//   negligible tail introduced by routing refinement and can be removed.
		// - Same logic applies symmetrically for the last internal point.

		const float portArcOffset = 24f;
		const float maxTailKeep = portArcOffset * 0.5f + 0.1f;
		const float axisTol = 0.5f; // tolerance to consider axis-aligned

		// Remove first tail if it is a small axis-aligned stub near start.
		if (internalPoints.Count > 0)
		{
			var p0 = internalPoints[0];
			var next = internalPoints.Count > 1 ? internalPoints[1] : end;
			var d0 = p0 - start;
			if ((MathF.Abs(d0.X) < axisTol && MathF.Abs(d0.Y) > axisTol) || (MathF.Abs(d0.Y) < axisTol && MathF.Abs(d0.X) > axisTol))
			{
				var dist = d0.Length();
				// Check colinearity with next: the next point should share the same axis coordinate as p0.
				// Only remove the stub if start->next remains axis-aligned.
				if (dist <= maxTailKeep)
				{
					var colinearWithNext = MathF.Abs(next.X - p0.X) < axisTol || MathF.Abs(next.Y - p0.Y) < axisTol;
					var directToNextAxisAligned = MathF.Abs(start.X - next.X) < axisTol || MathF.Abs(start.Y - next.Y) < axisTol;
					if (colinearWithNext && directToNextAxisAligned)
					{
						internalPoints.RemoveAt(0);
					}
				}
			}
		}

		// Remove last tail if it is a small axis-aligned stub near end.
		if (internalPoints.Count > 0)
		{
			var ln = internalPoints.Count;
			var plast = internalPoints[ln - 1];
			var prev = ln > 1 ? internalPoints[ln - 2] : start;
			var d1 = plast - end;
			if ((MathF.Abs(d1.X) < axisTol && MathF.Abs(d1.Y) > axisTol) || (MathF.Abs(d1.Y) < axisTol && MathF.Abs(d1.X) > axisTol))
			{
				var dist = d1.Length();
				if (dist <= maxTailKeep)
				{
					var colinearWithPrev = MathF.Abs(prev.X - plast.X) < axisTol || MathF.Abs(prev.Y - plast.Y) < axisTol;
					var directToEndAxisAligned = MathF.Abs(prev.X - end.X) < axisTol || MathF.Abs(prev.Y - end.Y) < axisTol;
					if (colinearWithPrev && directToEndAxisAligned)
					{
						internalPoints.RemoveAt(ln - 1);
					}
				}
			}
		}
	}

	private static bool IsPointOnSegmentApprox(Vector2 a, Vector2 b, Vector2 p, float tol)
	{
		var ab = b - a;
		var ab2 = ab.LengthSquared();
		if (ab2 < 1e-8f) return (p - a).LengthSquared() <= tol * tol;
		var t = Vector2.Dot(p - a, ab) / ab2;
		if (t < 0f || t > 1f) return false;
		var closest = a + ab * t;
		return Vector2.DistanceSquared(closest, p) <= tol * tol;
	}

	private static Vector2 PushPointOutOfRect(Vector2 p, RectF r, float epsilon)
	{
		// Minimal translation to move the point just outside the rectangle.
		var leftDist = MathF.Abs(p.X - r.Left);
		var rightDist = MathF.Abs(r.Right - p.X);
		var topDist = MathF.Abs(p.Y - r.Top);
		var bottomDist = MathF.Abs(r.Bottom - p.Y);

		var min = leftDist;
		var side = 0; // 0=left 1=right 2=top 3=bottom
		if (rightDist < min) { min = rightDist; side = 1; }
		if (topDist < min) { min = topDist; side = 2; }
		if (bottomDist < min) { min = bottomDist; side = 3; }

		return side switch
		{
			0 => new Vector2(r.Left - epsilon, p.Y),
			1 => new Vector2(r.Right + epsilon, p.Y),
			2 => new Vector2(p.X, r.Top - epsilon),
			3 => new Vector2(p.X, r.Bottom + epsilon),
			_ => p,
		};
	}

	private static bool SegmentIntersectsRect(Vector2 a, Vector2 b, RectF r)
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

	private static bool ArcSegmentIntersectsRect(Vector2 a, Vector2 b, RectF r)
	{
		return SegmentIntersectsRect(a, b, r);
	}

	private static void RepairArcAgainstNodes(
		Arc arc,
		ReadOnlyCollection<RectNode> nodes,
		int fromNodeIndex,
		int toNodeIndex,
		Vector2 start,
		Vector2 end,
		float clearance,
		int maxInternal)
	{
		if (maxInternal <= 0) return;

		// Iterate once over the chain and insert at most a few points.
		var inserted = 0;
		var maxInsert = arc.InternalPoints.Count >= maxInternal
			? 16
			: Math.Clamp(maxInternal - arc.InternalPoints.Count, 0, 16);
		if (maxInsert == 0) return;

		Vector2 GetPoint(int idx)
		{
			if (idx == -1) return start;
			if (idx == arc.InternalPoints.Count) return end;
			return arc.InternalPoints[idx];
		}

		var i = -1;
		while (i < arc.InternalPoints.Count && inserted < maxInsert)
		{
			var a = GetPoint(i);
			var b = GetPoint(i + 1);
			var hit = false;
			RectF hitRect = default;
			for (var n = 0; n < nodes.Count; n++)
			{
				var r = Expand(nodes[n].Bounds, clearance);
				if (!ArcSegmentIntersectsRect(a, b, r))
				{
					continue;
				}



				hit = true;
				hitRect = r;
				break;
			}

			if (!hit)
			{
				i++;
				continue;
			}

			static int CountIntersections(Vector2 a, Vector2 b, ReadOnlyCollection<RectNode> nodes)
			{
				var count = 0;
				foreach (var node in nodes)
				{
					if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, node.Bounds))
						count++;
				}
				return count;
			}

			var mid = (a + b) * 0.5f;
			var dLeft = MathF.Abs(mid.X - hitRect.Left);
			var dRight = MathF.Abs(hitRect.Right - mid.X);
			var dTop = MathF.Abs(mid.Y - hitRect.Top);
			var dBottom = MathF.Abs(hitRect.Bottom - mid.Y);
			var margin = 1.0f;
			var insertAt = Math.Clamp(i + 1, 0, arc.InternalPoints.Count);

			if (MathF.Abs(a.X - b.X) < 0.001f)
			{
				var leftX = hitRect.Left - margin;
				var rightX = hitRect.Right + margin;
				var candidateLeftA = new Vector2(leftX, a.Y);
				var candidateLeftB = new Vector2(leftX, b.Y);
				var candidateRightA = new Vector2(rightX, a.Y);
				var candidateRightB = new Vector2(rightX, b.Y);

				var leftCost = CountIntersections(a, candidateLeftA, nodes)
					+ CountIntersections(candidateLeftA, candidateLeftB, nodes)
					+ CountIntersections(candidateLeftB, b, nodes);
				var rightCost = CountIntersections(a, candidateRightA, nodes)
					+ CountIntersections(candidateRightA, candidateRightB, nodes)
					+ CountIntersections(candidateRightB, b, nodes);

				if (leftCost <= rightCost)
				{
					arc.InternalPoints.Insert(insertAt, candidateLeftB);
					arc.InternalPoints.Insert(insertAt, candidateLeftA);
				}
				else
				{
					arc.InternalPoints.Insert(insertAt, candidateRightB);
					arc.InternalPoints.Insert(insertAt, candidateRightA);
				}
				inserted += 2;
			}
			else if (MathF.Abs(a.Y - b.Y) < 0.001f)
			{
				var topY = hitRect.Top - margin;
				var bottomY = hitRect.Bottom + margin;
				var candidateTopA = new Vector2(a.X, topY);
				var candidateTopB = new Vector2(b.X, topY);
				var candidateBottomA = new Vector2(a.X, bottomY);
				var candidateBottomB = new Vector2(b.X, bottomY);

				var topCost = CountIntersections(a, candidateTopA, nodes)
					+ CountIntersections(candidateTopA, candidateTopB, nodes)
					+ CountIntersections(candidateTopB, b, nodes);
				var bottomCost = CountIntersections(a, candidateBottomA, nodes)
					+ CountIntersections(candidateBottomA, candidateBottomB, nodes)
					+ CountIntersections(candidateBottomB, b, nodes);

				if (topCost <= bottomCost)
				{
					arc.InternalPoints.Insert(insertAt, candidateTopB);
					arc.InternalPoints.Insert(insertAt, candidateTopA);
				}
				else
				{
					arc.InternalPoints.Insert(insertAt, candidateBottomB);
					arc.InternalPoints.Insert(insertAt, candidateBottomA);
				}
				inserted += 2;
			}
			else
			{
				var side = 0;
				var min = dLeft;
				if (dRight < min) { min = dRight; side = 1; }
				if (dTop < min) { min = dTop; side = 2; }
				if (dBottom < min) { min = dBottom; side = 3; }

				var waypoint = side switch
				{
					0 => new Vector2(hitRect.Left - margin, Math.Clamp(mid.Y, hitRect.Top - margin, hitRect.Bottom + margin)),
					1 => new Vector2(hitRect.Right + margin, Math.Clamp(mid.Y, hitRect.Top - margin, hitRect.Bottom + margin)),
					2 => new Vector2(Math.Clamp(mid.X, hitRect.Left - margin, hitRect.Right + margin), hitRect.Top - margin),
					3 => new Vector2(Math.Clamp(mid.X, hitRect.Left - margin, hitRect.Right + margin), hitRect.Bottom + margin),
					_ => mid,
				};

				arc.InternalPoints.Insert(insertAt, waypoint);
				inserted++;
			}

			// Re-check starting from previous segment.
			if (i > -1) i--;
		}
	}

	private static void EnsureOrthogonalInternalPoints(List<Vector2> internalPoints, ReadOnlyCollection<RectNode> nodes)
	{
		if (internalPoints.Count < 2) return;

		static bool IsSegmentClear(Vector2 a, Vector2 b, ReadOnlyCollection<RectNode> nodes)
		{
			foreach (var node in nodes)
			{
				if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, node.Bounds))
				{
					return false;
				}
			}
			return true;
		}

		var corrected = new List<Vector2>(internalPoints.Count * 2);
		corrected.Add(internalPoints[0]);

		static int BlockedSegmentCount(Vector2 a, Vector2 b, ReadOnlyCollection<RectNode> nodes)
		{
			var count = 0;
			foreach (var node in nodes)
			{
				if (ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(a, b, node.Bounds))
				{
					count++;
				}
			}
			return count;
		}

		for (var i = 1; i < internalPoints.Count; i++)
		{
			var prev = corrected[^1];
			var current = internalPoints[i];
			if (MathF.Abs(prev.X - current.X) < 0.001f || MathF.Abs(prev.Y - current.Y) < 0.001f)
			{
				corrected.Add(current);
				continue;
			}

			var cornerA = new Vector2(prev.X, current.Y);
			var cornerB = new Vector2(current.X, prev.Y);
			var useA = IsSegmentClear(prev, cornerA, nodes) && IsSegmentClear(cornerA, current, nodes);
			var useB = IsSegmentClear(prev, cornerB, nodes) && IsSegmentClear(cornerB, current, nodes);

			if (useA || useB)
			{
				corrected.Add(useA ? cornerA : cornerB);
			}
			else
			{
				var blockedA = BlockedSegmentCount(prev, cornerA, nodes) + BlockedSegmentCount(cornerA, current, nodes);
				var blockedB = BlockedSegmentCount(prev, cornerB, nodes) + BlockedSegmentCount(cornerB, current, nodes);
				corrected.Add(blockedA <= blockedB ? cornerA : cornerB);
			}

			corrected.Add(current);
		}

		internalPoints.Clear();
		internalPoints.AddRange(corrected);
	}

	private static void EnsureOrthogonalEndpoints(Vector2 start, Vector2 end, RectSide startSide, RectSide endSide, List<Vector2> internalPoints)
	{
		if (internalPoints.Count == 0) return;

		var first = internalPoints[0];
		if (startSide == RectSide.Left || startSide == RectSide.Right)
		{
			if (MathF.Abs(first.Y - start.Y) > 0.001f)
			{
				internalPoints.Insert(0, new Vector2(first.X, start.Y));
			}
		}
		else
		{
			if (MathF.Abs(first.X - start.X) > 0.001f)
			{
				internalPoints.Insert(0, new Vector2(start.X, first.Y));
			}
		}

		var last = internalPoints[^1];
		if (endSide == RectSide.Left || endSide == RectSide.Right)
		{
			if (MathF.Abs(last.Y - end.Y) > 0.001f)
			{
				internalPoints.Add(new Vector2(last.X, end.Y));
			}
		}
		else
		{
			if (MathF.Abs(last.X - end.X) > 0.001f)
			{
				internalPoints.Add(new Vector2(end.X, last.Y));
			}
		}
	}

	private static void SimplifyInternalPoints(List<Vector2> internalPoints)
	{
		if (internalPoints.Count < 2) return;

		const float tolerance = 0.5f;
		const float toleranceSq = tolerance * tolerance;

		for (var i = internalPoints.Count - 2; i >= 0; i--)
		{
			if (Vector2.DistanceSquared(internalPoints[i], internalPoints[i + 1]) <= toleranceSq)
			{
				internalPoints.RemoveAt(i + 1);
			}
		}

		var j = 0;
		while (j + 2 < internalPoints.Count)
		{
			var a = internalPoints[j];
			var b = internalPoints[j + 1];
			var c = internalPoints[j + 2];

			var colinearX = MathF.Abs(a.X - b.X) < 0.001f && MathF.Abs(b.X - c.X) < 0.001f;
			var colinearY = MathF.Abs(a.Y - b.Y) < 0.001f && MathF.Abs(b.Y - c.Y) < 0.001f;

			if (colinearX)
			{
				if ((b.Y >= MathF.Min(a.Y, c.Y) - tolerance && b.Y <= MathF.Max(a.Y, c.Y) + tolerance))
				{
					internalPoints.RemoveAt(j + 1);
					continue;
				}
			}
			else if (colinearY)
			{
				if ((b.X >= MathF.Min(a.X, c.X) - tolerance && b.X <= MathF.Max(a.X, c.X) + tolerance))
				{
					internalPoints.RemoveAt(j + 1);
					continue;
				}
			}

			if (Vector2.DistanceSquared(a, c) <= toleranceSq)
			{
				internalPoints.RemoveAt(j + 1);
				continue;
			}

			j++;
		}
	}

	private static void PushInternalPointsOutOfNodes(List<Vector2> internalPoints, ReadOnlyCollection<RectNode> nodes, float clearance)
	{
		if (internalPoints.Count == 0) return;
	
		const float epsilon = 0.001f;
		for (var i = 0; i < internalPoints.Count; i++)
		{
			var p = internalPoints[i];
			for (var n = 0; n < nodes.Count; n++)
			{
				var r = Expand(nodes[n].Bounds, clearance);
				if (!r.Contains(p)) continue;
				p = PushPointOutOfRect(p, r, epsilon);
			}
			internalPoints[i] = p;
		}
	}

	private void ApplyHardMinSpacing(ReadOnlyCollection<RectNode> nodes)
	{
		if (!_settings.UseHardMinSpacing) return;
		var spacing = Math.Max(0f, _settings.MinNodeSpacing);
		if (spacing <= 0f) return;

		var iterations = Math.Clamp(_settings.HardMinSpacingIterations, 0, 50);
		if (iterations == 0) return;

		var margin = spacing * 0.5f;
		var slop = MathF.Max(0f, _settings.HardMinSpacingSlop);

		for (var it = 0; it < iterations; it++)
		{
			var any = false;
			for (var i = 0; i < nodes.Count; i++)
			{
				var a = nodes[i];
				var ra = Expand(a.Bounds, margin);
				for (var j = i + 1; j < nodes.Count; j++)
				{
					var b = nodes[j];
					var rb = Expand(b.Bounds, margin);
					if (!ra.Intersects(rb))
					{
						continue;
					}

					var (ox, oy) = ra.Overlap(rb);
					if (ox <= 0 || oy <= 0)
					{
						continue;
					}

					any = true;
					var delta = b.Position - a.Position;
					Vector2 dir;
					float penetration;

					// Separate along minimum-penetration axis.
					if (ox < oy)
					{
						var sx = (float)MathF.Sign(delta.X);
						if (MathF.Abs(sx) < 0.001f)
						{
							// Deterministic tie-break.
							sx = string.CompareOrdinal(a.Id.Value, b.Id.Value) < 0 ? -1f : 1f;
						}
						dir = new Vector2(sx, 0);
						penetration = ox;
					}
					else
					{
						var sy = (float)MathF.Sign(delta.Y);
						if (MathF.Abs(sy) < 0.001f)
						{
							sy = string.CompareOrdinal(a.Id.Value, b.Id.Value) < 0 ? -1f : 1f;
						}
						dir = new Vector2(0, sy);
						penetration = oy;
					}

					var push = (penetration + slop) * 0.5f;
					a.Position -= dir * push;
					b.Position += dir * push;

					// Kill velocity along the separating axis to reduce jitter.
					if (dir.X != 0)
					{
						a.Velocity = new Vector2(0, a.Velocity.Y);
						b.Velocity = new Vector2(0, b.Velocity.Y);
					}
					else
					{
						a.Velocity = new Vector2(a.Velocity.X, 0);
						b.Velocity = new Vector2(b.Velocity.X, 0);
					}
				}
			}

			if (!any)
			{
				break;
			}
		}
	}

	private void ApplyBackgroundGravity(ReadOnlyCollection<RectNode> nodes, Vector2[] total, Vector2[]? background)
	{
		var g = _settings.BackgroundPairGravity;
		var soft2 = _settings.Softening * _settings.Softening;
		var m = MathF.Max(0.000001f, _settings.NodeMass);

		for (var i = 0; i < nodes.Count; i++)
		{
			for (var j = i + 1; j < nodes.Count; j++)
			{
				var a = nodes[i];
				var b = nodes[j];
				var delta = b.Position - a.Position;
				var r2 = delta.LengthSquared() + soft2;
				var invR = 1f / MathF.Sqrt(r2);
				var dir = delta * invR;

				// Inverse-square attraction with softening.
				var f = g * (m * m) / r2;
				var fa = dir * f;

				total[i] += fa;
				total[j] -= fa;
				if (background is not null)
				{
					background[i] += fa;
					background[j] -= fa;
				}
			}
		}
	}

	private void ApplyOverlapRepulsion(ReadOnlyCollection<RectNode> nodes, Vector2[] total, Vector2[]? overlap)
	{
		var k = _settings.OverlapRepulsionK;
		var spacing = 0f;

		// When the hard MinNodeSpacing solver is disabled, repulsion is the primary mechanism
		// to resolve overlaps. For rectangle-only layout, we do not expand node bounds by spacing.
		var overlapBoost = _settings.UseHardMinSpacing
			? 1f
			: MathF.Max(0f, _settings.SoftOverlapBoostWhenHardDisabled);

		for (var i = 0; i < nodes.Count; i++)
		{
			var a = nodes[i];
			var ra = Expand(a.Bounds, spacing * 0.5f);
			for (var j = i + 1; j < nodes.Count; j++)
			{
				var b = nodes[j];
				var rb = Expand(b.Bounds, spacing * 0.5f);
				if (!ra.Intersects(rb))
				{
					continue;
				}

				var (ox, oy) = ra.Overlap(rb);
				if (ox <= 0 || oy <= 0)
				{
					continue;
				}

				var delta = b.Position - a.Position;
				Vector2 dir;
				float penetration;

				// Push along the minimum-penetration axis.
				if (ox < oy)
				{
					dir = new Vector2(MathF.Sign(delta.X) == 0 ? 1 : MathF.Sign(delta.X), 0);
					penetration = ox;
				}
				else
				{
					dir = new Vector2(0, MathF.Sign(delta.Y) == 0 ? 1 : MathF.Sign(delta.Y));
					penetration = oy;
				}

				var baseForce = k * overlapBoost;
				// Non-linear: deeper intersections push much harder.
				var mag = baseForce * penetration * (penetration + 1f);
				var f = dir * mag;
				total[i] -= f;
				total[j] += f;
				if (overlap is not null)
				{
					overlap[i] -= f;
					overlap[j] += f;
				}
			}
		}
	}

	private static RectF Expand(in RectF rect, float margin)
		=> new(rect.X - margin, rect.Y - margin, rect.Width + 2 * margin, rect.Height + 2 * margin);

	private void ApplyArcSprings(
		Diagram diagram,
		ReadOnlyCollection<RectNode> nodes,
		Dictionary<DiagramId, int> nodeIndexById,
		Vector2[] total,
		Vector2[]? arcForces)
	{
		var rest = _settings.MinimizeArcLength ? 0f : _settings.EdgeSpringRestLength;
		var k = _settings.ConnectedArcAttractionK;

		foreach (var arc in diagram.Arcs)
		{
			var fromPort = diagram.TryGetPort(arc.FromPortId);
			var toPort = diagram.TryGetPort(arc.ToPortId);
			if (fromPort is null || toPort is null) continue;

			if (!nodeIndexById.TryGetValue(fromPort.Ref.NodeId, out var ia)) continue;
			if (!nodeIndexById.TryGetValue(toPort.Ref.NodeId, out var ib)) continue;

			if (ia == ib)
			{
				continue;
			}

			var a = nodes[ia];
			var b = nodes[ib];

			// NOTE: We do NOT gate/disable arc forces based on MinNodeSpacing.
			// Min spacing is enforced by overlap repulsion and/or hard constraints during application.

			var pa = GetPortWorldPosition(a, fromPort.Ref);
			var pb = GetPortWorldPosition(b, toPort.Ref);
			var delta = pb - pa;
			var dist = delta.Length();
			if (dist < 0.001f)
			{
				continue;
			}

			// For orthogonal diagrams, minimizing visible arc length is closer to minimizing
			// Manhattan distance (|dx|+|dy|) than Euclidean. A simple spring along the port-to-port
			// direction often makes Y-alignment too weak when dx >> dy.
			// Add a dedicated axis-alignment force:
			// - Left/Right connections: strongly align Y
			// - Top/Bottom connections: strongly align X
			var align = GetOrthogonalAlignmentDelta(fromPort.Ref.Side, toPort.Ref.Side, delta);

			var dir = delta / dist;
			var f = Vector2.Zero;

			// Axis alignment (always active when k>0).
			// Uses a fraction of k to avoid excessive oscillations.
			const float alignKFactor = 0.6f;
			f += align * (k * alignKFactor);

			// Only attract (pull together) when the arc is longer than the desired rest length.
			// Repulsion and/or constraints handle "too close" cases.
			var stretch = dist - rest;
			if (stretch > 0f)
			{
				f += dir * (k * stretch);
			}

			if (f.LengthSquared() < 0.000001f)
			{
				continue;
			}

			total[ia] += f;
			total[ib] -= f;
			if (arcForces is not null)
			{
				arcForces[ia] += f;
				arcForces[ib] -= f;
			}
		}
	}

	private static Vector2 GetOrthogonalAlignmentDelta(RectSide fromSide, RectSide toSide, Vector2 delta)
	{
		static bool IsHorizontal(RectSide s) => s is RectSide.Left or RectSide.Right;
		static bool IsVertical(RectSide s) => s is RectSide.Top or RectSide.Bottom;

		// Left/Right: align Y (drive delta.Y -> 0)
		if (IsHorizontal(fromSide) && IsHorizontal(toSide))
		{
			return new Vector2(0f, delta.Y);
		}

		// Top/Bottom: align X (drive delta.X -> 0)
		if (IsVertical(fromSide) && IsVertical(toSide))
		{
			return new Vector2(delta.X, 0f);
		}

		return Vector2.Zero;
	}

	private void Integrate(ReadOnlyCollection<RectNode> nodes, Vector2[] forces, float dt)
	{
		var drag = _settings.Drag;
		var maxSpeed = _settings.MaxSpeed;
		var m = MathF.Max(0.000001f, _settings.NodeMass);

		for (var i = 0; i < nodes.Count; i++)
		{
			var n = nodes[i];
			var a = forces[i] / m;
			n.Velocity += a * dt;

			// Exponential drag.
			var dragFactor = MathF.Exp(-drag * dt);
			n.Velocity *= dragFactor;

			var speed = n.Velocity.Length();
			if (speed > maxSpeed)
			{
				n.Velocity = n.Velocity / speed * maxSpeed;
			}

			n.Position += n.Velocity * dt;
		}
	}

	public static Vector2 GetPortWorldPosition(RectNode node, PortRef port)
	{
		var offset = Math.Clamp(port.Offset, 0f, 1f);
		var left = node.Position.X - node.Width / 2f;
		var right = node.Position.X + node.Width / 2f;
		var top = node.Position.Y - node.Height / 2f;
		var bottom = node.Position.Y + node.Height / 2f;

		return port.Side switch
		{
			RectSide.Top => new Vector2(left + offset * node.Width, top),
			RectSide.Right => new Vector2(right, top + offset * node.Height),
			RectSide.Bottom => new Vector2(left + offset * node.Width, bottom),
			RectSide.Left => new Vector2(left, top + offset * node.Height),
			_ => node.Position,
		};
	}
}
