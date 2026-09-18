namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Тип маршрута дуги между двумя портами.
/// </summary>
public enum ArcType
{
	/// <summary>
	/// Ортогональная ломаная из горизонтальных и вертикальных сегментов.
	/// </summary>
	Polyline,

	/// <summary>
	/// Прямая линия от порта к порту без промежуточных точек.
	/// </summary>
	Straight
}
