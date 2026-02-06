namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class Port
{
	public required DiagramId Id { get; init; }
	public required PortRef Ref { get; set; }
	public required string Text { get; set; }
}
