namespace SamorodinkaTech.GravityDiagram.Core;

public readonly record struct PortRef(DiagramId NodeId, RectSide Side, float Offset)
{
	public float ClampedOffset => Math.Clamp(Offset, 0f, 1f);
}
