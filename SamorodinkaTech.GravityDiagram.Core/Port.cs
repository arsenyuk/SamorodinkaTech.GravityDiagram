namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Порт ноды — точка подключения дуги. Порт привязан к определённой стороне
/// прямоугольника ноды и имеет смещение (0..1) вдоль этой стороны.
/// </summary>
public sealed class Port
{
	/// <summary>Уникальный идентификатор порта.</summary>
	public required DiagramId Id { get; init; }
	/// <summary>Ссылка на ноду: идентификатор ноды, сторона и смещение.</summary>
	public required PortRef Ref { get; set; }
	/// <summary>Отображаемое имя порта.</summary>
	public required string Text { get; set; }
}
