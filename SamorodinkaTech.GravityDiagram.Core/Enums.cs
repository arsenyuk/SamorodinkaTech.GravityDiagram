namespace SamorodinkaTech.GravityDiagram.Core;

[Flags]
public enum PortFlow
{
	None = 0,
	Incoming = 1,
	Outgoing = 2,
	Both = Incoming | Outgoing,
}

public enum RectSide
{
	Top,
	Right,
	Bottom,
	Left,
}
