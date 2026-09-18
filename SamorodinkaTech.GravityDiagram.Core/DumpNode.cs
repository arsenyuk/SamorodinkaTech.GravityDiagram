namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Снимок одной ноды (прямоугольника) в момент создания дампа.
/// Позиция задаётся центром — так же, как и в основной модели.
/// </summary>
public sealed record DumpNode(
	/// <summary>Уникальный идентификатор ноды.</summary>
	string Id,
	/// <summary>Отображаемый текст (заголовок ноды). Может быть null.</summary>
	string? Text,
	/// <summary>Текущая позиция центра ноды.</summary>
	DumpVec2 Position,
	/// <summary>Текущая скорость ноды (для воспроизведения динамики).</summary>
	DumpVec2 Velocity,
	/// <summary>Ширина прямоугольника в пикселях.</summary>
	float Width,
	/// <summary>Высота прямоугольника в пикселях.</summary>
	float Height,
	/// <summary>Последнее перемещение ноды за шаг (для автостопа симуляции).</summary>
	DumpVec2? LastMovementDelta = null);
