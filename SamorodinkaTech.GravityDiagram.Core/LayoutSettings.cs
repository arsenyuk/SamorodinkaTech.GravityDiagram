namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class LayoutSettings
{
	public float Softening { get; set; } = 40f;
	// Strength of pairwise attraction between nodes (gravity-like).
	// NOTE: This value is intentionally small because the force also scales with node masses.
	public float BackgroundPairGravity { get; set; } = 0.12f;
	public float EdgeSpringRestLength { get; set; } = 220f;
	public float EdgeSpringK { get; set; } = 2.2f;

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
