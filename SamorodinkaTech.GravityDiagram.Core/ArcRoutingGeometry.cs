using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

public static class ArcRoutingGeometry
{
	public static Vector2 SideDir(RectSide side)
		=> side switch
		{
			RectSide.Top => new Vector2(0, -1),
			RectSide.Right => new Vector2(1, 0),
			RectSide.Bottom => new Vector2(0, 1),
			RectSide.Left => new Vector2(-1, 0),
			_ => new Vector2(1, 0),
		};

	public static float ComputeOutDistance(float baseOutDistance, float minNodeSpacing)
		=> MathF.Max(MathF.Max(0f, baseOutDistance), MathF.Max(0f, minNodeSpacing));

	public static Vector2 ComputeExitPoint(Vector2 portWorldPosition, RectSide side, float baseOutDistance, float minNodeSpacing)
	{
		var d = ComputeOutDistance(baseOutDistance, minNodeSpacing);
		return portWorldPosition + SideDir(side) * d;
	}

	/// <summary>
	/// Returns true when the perpendicular exit points (port + outDistance * side normal) cross over each other
	/// for opposite sides on the same axis (Left/Right or Top/Bottom). This typically happens when the ports are
	/// closer than 2*outDistance along that axis, and it often makes the orthogonal polyline look "wrong"
	/// unless a lane detour is introduced.
	/// </summary>
	public static bool ArePerpendicularOutPointsCrossed(
		Vector2 from,
		RectSide fromSide,
		Vector2 to,
		RectSide toSide,
		float outDistance)
	{
		var outA = from + SideDir(fromSide) * outDistance;
		var outB = to + SideDir(toSide) * outDistance;

		var outDirA = outA - from;
		var outDirB = outB - to;

		var isHorizontalOpposite = MathF.Abs(outDirA.X) > 0.5f
			&& MathF.Abs(outDirB.X) > 0.5f
			&& MathF.Sign(outDirA.X) == -MathF.Sign(outDirB.X);
		if (isHorizontalOpposite)
		{
			var expected = Math.Sign(to.X - from.X);
			if (expected == 0) expected = 1;
			return (outB.X - outA.X) * expected < 0f;
		}

		var isVerticalOpposite = MathF.Abs(outDirA.Y) > 0.5f
			&& MathF.Abs(outDirB.Y) > 0.5f
			&& MathF.Sign(outDirA.Y) == -MathF.Sign(outDirB.Y);
		if (isVerticalOpposite)
		{
			var expected = Math.Sign(to.Y - from.Y);
			if (expected == 0) expected = 1;
			return (outB.Y - outA.Y) * expected < 0f;
		}

		return false;
	}

	/// <summary>
	/// Ensures a lane shift does not move the first/last lane points back towards the node,
	/// which would effectively reduce the perpendicular exit/enter distance (e.g. when a lane shift is collinear
	/// with the port normal and negative).
	/// </summary>
	public static Vector2 ClampLaneShiftAgainstExit(Vector2 start, Vector2 startOut, Vector2 end, Vector2 endOut, Vector2 laneShift)
	{
		static Vector2 UnitAxis(Vector2 v)
		{
			if (MathF.Abs(v.X) >= MathF.Abs(v.Y))
			{
				if (MathF.Abs(v.X) < 0.0001f) return Vector2.Zero;
				return new Vector2(MathF.Sign(v.X), 0f);
			}
			else
			{
				if (MathF.Abs(v.Y) < 0.0001f) return Vector2.Zero;
				return new Vector2(0f, MathF.Sign(v.Y));
			}
		}

		static Vector2 ClampAgainst(Vector2 outDir, Vector2 shift)
		{
			// Only clamp if the shift has a component against the outward direction.
			// For axis-aligned outDir, this becomes a simple sign clamp.
			if (outDir.X > 0.5f) shift = new Vector2(MathF.Max(0f, shift.X), shift.Y);
			else if (outDir.X < -0.5f) shift = new Vector2(MathF.Min(0f, shift.X), shift.Y);
			else if (outDir.Y > 0.5f) shift = new Vector2(shift.X, MathF.Max(0f, shift.Y));
			else if (outDir.Y < -0.5f) shift = new Vector2(shift.X, MathF.Min(0f, shift.Y));
			return shift;
		}

		var startOutDir = UnitAxis(startOut - start);
		var endOutDir = UnitAxis(endOut - end);
		laneShift = ClampAgainst(startOutDir, laneShift);
		laneShift = ClampAgainst(endOutDir, laneShift);
		return laneShift;
	}

	/// <summary>
	/// Checks if an axis-aligned segment intersects a rectangle interior.
	/// Touching the rectangle boundary is NOT considered an intersection.
	/// This matches the intended "clearance" semantics (distance >= clearance is OK).
	/// </summary>
	public static bool AxisAlignedSegmentIntersectsRect(Vector2 a, Vector2 b, RectF r)
	{
		// Vertical segment.
		if (MathF.Abs(a.X - b.X) < 0.001f)
		{
			var x = a.X;
			// Strict: boundary touch doesn't count.
			if (x <= r.Left || x >= r.Right) return false;
			var y0 = MathF.Min(a.Y, b.Y);
			var y1 = MathF.Max(a.Y, b.Y);
			return !(y1 <= r.Top || y0 >= r.Bottom);
		}

		// Horizontal segment.
		if (MathF.Abs(a.Y - b.Y) < 0.001f)
		{
			var y = a.Y;
			// Strict: boundary touch doesn't count.
			if (y <= r.Top || y >= r.Bottom) return false;
			var x0 = MathF.Min(a.X, b.X);
			var x1 = MathF.Max(a.X, b.X);
			return !(x1 <= r.Left || x0 >= r.Right);
		}

		// Should not happen; router is axis-aligned.
		return false;
	}
}
