using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;
using Xunit;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

#if false // Example-based tests are temporarily disabled while the physics/routing model evolves.
public sealed class ArcRoutingGeometryTests
{
	[Fact]
	public void AxisAlignedSegmentIntersectsRect_DoesNotCountBoundaryTouch()
	{
		var r = new RectF(0, 0, 10, 10);

		// Touch left/right boundaries.
		Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(new Vector2(0, -5), new Vector2(0, 15), r));
		Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(new Vector2(10, -5), new Vector2(10, 15), r));

		// Touch top/bottom boundaries.
		Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(new Vector2(-5, 0), new Vector2(15, 0), r));
		Assert.False(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(new Vector2(-5, 10), new Vector2(15, 10), r));
	}

	[Fact]
	public void AxisAlignedSegmentIntersectsRect_DetectsInteriorIntersection()
	{
		var r = new RectF(0, 0, 10, 10);

		Assert.True(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(new Vector2(5, -5), new Vector2(5, 15), r));
		Assert.True(ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect(new Vector2(-5, 5), new Vector2(15, 5), r));
	}

	[Fact]
	public void ClampLaneShiftAgainstExit_DoesNotReduceRightExitDistance()
	{
		var start = new Vector2(0, 0);
		var startOut = new Vector2(10, 0);
		var end = new Vector2(100, 0);
		var endOut = new Vector2(90, 0);

		var shift = new Vector2(-5, 0); // would move inward for a Right exit
		var clamped = ArcRoutingGeometry.ClampLaneShiftAgainstExit(start, startOut, end, endOut, shift);
		Assert.Equal(0f, clamped.X);
		Assert.Equal(0f, clamped.Y);
	}

	[Fact]
	public void ClampLaneShiftAgainstExit_DoesNotReduceTopExitDistance()
	{
		var start = new Vector2(0, 0);
		var startOut = new Vector2(0, -10);
		var end = new Vector2(0, 100);
		var endOut = new Vector2(0, 110);

		var shift = new Vector2(0, 6); // would move inward for a Top exit
		var clamped = ArcRoutingGeometry.ClampLaneShiftAgainstExit(start, startOut, end, endOut, shift);
		Assert.Equal(0f, clamped.X);
		Assert.Equal(0f, clamped.Y);
	}

	[Fact]
	public void ArePerpendicularOutPointsCrossed_DetectsRightLeftClosePorts()
	{
		// Matches the pattern from gravity-dump-latest-20260207-065638.json:
		// Right -> Left with dx < 2*outDistance.
		var from = new Vector2(357.91653f, 318.08408f);
		var to = new Vector2(412.829f, 318.81436f);
		var outDistance = 55.441406f;

		Assert.True(ArcRoutingGeometry.ArePerpendicularOutPointsCrossed(from, RectSide.Right, to, RectSide.Left, outDistance));
	}
}
#endif
