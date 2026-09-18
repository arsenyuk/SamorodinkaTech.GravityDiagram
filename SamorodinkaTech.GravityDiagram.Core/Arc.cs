using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Дуга, соединяющая два порта. Содержит промежуточные точки полилинии,
/// которые обновляются физическим движком (пружинные силы, repulsion и т.д.).
/// </summary>
public sealed class Arc
{
	/// <summary>Уникальный идентификатор дуги.</summary>
	public required DiagramId Id { get; init; }
	/// <summary>Идентификатор порта-источника (откуда исходит дуга).</summary>
	public required DiagramId FromPortId { get; init; }
	/// <summary>Идентификатор порта-приёмника (куда входит дуга).</summary>
	public required DiagramId ToPortId { get; init; }
	/// <summary>Отображаемое имя дуги (подпись на рисунке).</summary>
	public required string Text { get; set; }

	/// <summary>Зафиксировать ли первую внутреннюю точку по нормали порта-источника.</summary>
	public bool FixFirstPointToNormal { get; set; } = true;
	/// <summary>Зафиксировать ли последнюю внутреннюю точку по нормали порта-приёмника.</summary>
	public bool FixLastPointToNormal { get; set; } = true;

	/// <summary>
	/// Промежуточные точки ортогональной полилинии (не включая конечные точки на портах).
	/// Обновляются движком гравитационной компоновки и могут сливаться при устранении коллинеарности.
	/// </summary>
	public List<Vector2> InternalPoints { get; } = new();
}
