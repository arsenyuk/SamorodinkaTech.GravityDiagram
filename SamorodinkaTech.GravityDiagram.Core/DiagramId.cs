namespace SamorodinkaTech.GravityDiagram.Core;

public sealed record DiagramId(string Value)
{
	public static DiagramId New() => new(Guid.NewGuid().ToString("N"));
	public override string ToString() => Value;
}
