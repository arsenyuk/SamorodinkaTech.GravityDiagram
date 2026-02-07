using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class Arc
{
	public required DiagramId Id { get; init; }
	public required DiagramId FromPortId { get; init; }
	public required DiagramId ToPortId { get; init; }
	public required string Text { get; set; }

	// Internal polyline points (excluding endpoints on ports).
	// These points are updated by the gravity model and can be merged.
	public List<Vector2> InternalPoints { get; } = new();
}
