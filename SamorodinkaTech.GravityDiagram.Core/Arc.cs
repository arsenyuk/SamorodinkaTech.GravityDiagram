namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class Arc
{
	public required DiagramId Id { get; init; }
	public required DiagramId FromPortId { get; init; }
	public required DiagramId ToPortId { get; init; }
	public required string Text { get; set; }
}
