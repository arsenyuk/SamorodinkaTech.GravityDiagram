using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Снимок состояния одной промежуточной точки дуги на шаге симуляции.
/// Точка является вершиной ортогональной полилинии, соединяющей два порта.
/// </summary>
/// <param name="Index">Индекс точки в последовательности промежуточных точек дуги (начиная с 0).</param>
/// <param name="Position">Текущая позиция промежуточной точки в мировых координатах.</param>
/// <param name="Force">Сила, действующая на данную промежуточную точку (притяжение к портам, отталкивание от узлов).</param>
public sealed record ArcPointStepPreview(
	int Index,
	Vector2 Position,
	Vector2 Force);
