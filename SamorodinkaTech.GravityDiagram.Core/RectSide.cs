namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Сторона прямоугольника, к которой привязан порт.
/// Определяет, с какой грани ноды выходит или входит дуга.
/// </summary>
public enum RectSide
{
	/// <summary>Верхняя сторона.</summary>
	Top,

	/// <summary>Правая сторона.</summary>
	Right,

	/// <summary>Нижняя сторона.</summary>
	Bottom,

	/// <summary>Левая сторона.</summary>
	Left,
}
