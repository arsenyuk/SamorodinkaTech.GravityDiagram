namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Параметры маршрутизации отдельной дуги.
/// </summary>
public class ArcLayoutOptions
{
	/// <summary>
	/// Тип маршрута: ортогональная ломаная или прямая.
	/// </summary>
	public ArcType Type { get; set; } = ArcType.Polyline;

	/// <summary>
	/// Зафиксировать первую промежуточную точку по нормали порта (отступ от порта).
	/// </summary>
	public bool FixFirstPointToNormal { get; set; } = true;

	/// <summary>
	/// Зафиксировать последнюю промежуточную точку по нормали порта.
	/// </summary>
	public bool FixLastPointToNormal { get; set; } = true;
}
