namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Уникальный идентификатор элемента диаграммы (ноды, порта, дуги).
/// Обёртка над строковым значением с фабричным методом для генерации GUID.
/// </summary>
public sealed record DiagramId(string Value)
{
	/// <summary>Генерирует новый уникальный идентификатор на основе GUID.</summary>
	public static DiagramId New() => new(Guid.NewGuid().ToString("N"));
	public override string ToString() => Value;
}
