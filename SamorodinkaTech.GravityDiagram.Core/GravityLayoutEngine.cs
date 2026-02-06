using System.Collections.ObjectModel;
using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class GravityLayoutEngine
{
	private readonly LayoutSettings _settings;
	private readonly Dictionary<DiagramId, Vector2> _lastForcesByNodeId = new();

	public GravityLayoutEngine(LayoutSettings? settings = null)
	{
		_settings = settings ?? new LayoutSettings();
	}

	public LayoutSettings Settings => _settings;
	public IReadOnlyDictionary<DiagramId, Vector2> LastForcesByNodeId => _lastForcesByNodeId;

	public void Step(Diagram diagram, float dt)
	{
		ArgumentNullException.ThrowIfNull(diagram);
		if (dt <= 0) return;

		var nodes = diagram.Nodes;
		if (nodes.Count == 0) return;

		var forces = new Vector2[nodes.Count];
		var nodeIndexById = new Dictionary<DiagramId, int>(nodes.Count);
		for (var i = 0; i < nodes.Count; i++)
		{
			nodeIndexById[nodes[i].Id] = i;
		}

		ApplyBackgroundGravity(nodes, forces);
		ApplyOverlapRepulsion(nodes, forces);
		ApplyArcSprings(diagram, nodes, nodeIndexById, forces);

		_lastForcesByNodeId.Clear();
		for (var i = 0; i < nodes.Count; i++)
		{
			_lastForcesByNodeId[nodes[i].Id] = forces[i];
		}

		Integrate(nodes, forces, dt);
		ApplyHardMinSpacing(nodes);
	}

	private void ApplyHardMinSpacing(ReadOnlyCollection<RectNode> nodes)
	{
		if (!_settings.UseHardMinSpacing) return;
		var spacing = MathF.Max(0f, _settings.MinNodeSpacing);
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

	private void ApplyBackgroundGravity(ReadOnlyCollection<RectNode> nodes, Vector2[] forces)
	{
		var g = _settings.BackgroundPairGravity;
		var soft2 = _settings.Softening * _settings.Softening;

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
				var f = g * (a.Mass * b.Mass) / r2;
				var fa = dir * f;

				forces[i] += fa;
				forces[j] -= fa;
			}
		}
	}

	private void ApplyOverlapRepulsion(ReadOnlyCollection<RectNode> nodes, Vector2[] forces)
	{
		var k = _settings.OverlapRepulsionK;
		var spacing = MathF.Max(0f, _settings.MinNodeSpacing);

		// When the hard MinNodeSpacing solver is disabled, repulsion becomes the primary mechanism
		// to resolve overlaps. Boost and make it non-linear so intersecting rectangles separate
		// faster (without needing extreme global tuning).
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
				forces[i] -= f;
				forces[j] += f;
			}
		}
	}

	private static RectF Expand(in RectF rect, float margin)
		=> new(rect.X - margin, rect.Y - margin, rect.Width + 2 * margin, rect.Height + 2 * margin);

	private void ApplyArcSprings(
		Diagram diagram,
		ReadOnlyCollection<RectNode> nodes,
		Dictionary<DiagramId, int> nodeIndexById,
		Vector2[] forces)
	{
		var rest = _settings.MinimizeArcLength ? 0f : _settings.EdgeSpringRestLength;
		var k = _settings.EdgeSpringK;
		var spacing = MathF.Max(0f, _settings.MinNodeSpacing);

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

			// When minimizing arc length, don't keep pulling if nodes are already at (or inside)
			// the minimal allowed spacing. Repulsion will handle the exact separation.
			if (_settings.MinimizeArcLength && spacing > 0f)
			{
				var ra = Expand(a.Bounds, spacing * 0.5f);
				var rb = Expand(b.Bounds, spacing * 0.5f);
				if (ra.Intersects(rb))
				{
					continue;
				}
			}

			var pa = GetPortWorldPosition(a, fromPort.Ref);
			var pb = GetPortWorldPosition(b, toPort.Ref);
			var delta = pb - pa;
			var dist = delta.Length();
			if (dist < 0.001f)
			{
				continue;
			}

			var dir = delta / dist;
			var stretch = dist - rest;
			var f = dir * (k * stretch);

			forces[ia] += f;
			forces[ib] -= f;
		}
	}

	private void Integrate(ReadOnlyCollection<RectNode> nodes, Vector2[] forces, float dt)
	{
		var drag = _settings.Drag;
		var maxSpeed = _settings.MaxSpeed;

		for (var i = 0; i < nodes.Count; i++)
		{
			var n = nodes[i];
			var a = forces[i] / n.Mass;
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
