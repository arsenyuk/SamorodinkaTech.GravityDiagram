namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class LayoutSettings
{
	// Mass used for all nodes (uniform). This influences how strongly forces affect motion:
	// acceleration = force / mass.
	public float NodeMass { get; set; } = 12.8f;

	public float Softening { get; set; } = 40f;
	// Strength of pairwise attraction between nodes (gravity-like).
	// NOTE: This value is intentionally small because the force also scales with node masses.
	public float BackgroundPairGravity { get; set; } = 0.12f;
	public float EdgeSpringRestLength { get; set; } = 220f;

	// Mutual attraction strength between rectangles that are connected by an arc.
	// Implemented as a "spring-like" pull along arcs (only attracts, never pushes).
	public float ConnectedArcAttractionK { get; set; } = 2.2f;

	// --- Arc point physics ---
	// Internal (polyline) arc points are treated as massless: they do not accumulate velocity.
	// Forces are computed between adjacent points and applied directly to positions.
	public float ArcPointAttractionK { get; set; } = 6.0f;

	// Scales how fast arc points move per step (positionDelta = force * ArcPointMoveFactor * dt).
	public float ArcPointMoveFactor { get; set; } = 0.035f;

	// Repulsion strength from node bounds expanded by (MinNodeSpacing/2 + ArcPointExtraClearance).
	// Force magnitude is proportional to the penetration depth (zone violation).
	public float ArcPointNodeRepulsionK { get; set; } = 1200f;

	// When two adjacent internal arc points get closer than this, they are merged.
	public float ArcPointMergeDistance { get; set; } = 2.0f;

	// How many constraint iterations to run to keep arc points outside node clearance rectangles.
	public int ArcPointConstraintIterations { get; set; } = 6;

	// Extra clearance margin used for arc points and arc segments against nodes.
	// This is in addition to MinNodeSpacing.
	public float ArcPointExtraClearance { get; set; } = 0f;

	// Upper bound to prevent runaway insertion/repair.
	public int MaxArcInternalPoints { get; set; } = 64;

	// If true, arcs try to be as short as possible (spring rest length is treated as 0).
	public bool MinimizeArcLength { get; set; } = false;

	// Minimum edge-to-edge distance between rectangles.
	public float MinNodeSpacing { get; set; } = 40f;

	// If true, applies a hard post-step constraint solver that enforces MinNodeSpacing.
	public bool UseHardMinSpacing { get; set; } = true;
	public int HardMinSpacingIterations { get; set; } = 4;
	public float HardMinSpacingSlop { get; set; } = 0.5f;

	public float OverlapRepulsionK { get; set; } = 90f;

	// Extra multiplier for overlap repulsion when UseHardMinSpacing=false.
	// In that mode, repulsion is the primary mechanism to resolve intersections fast.
	public float SoftOverlapBoostWhenHardDisabled { get; set; } = 4f;
	public float Drag { get; set; } = 2.2f;
	public float MaxSpeed { get; set; } = 2500f;
}
