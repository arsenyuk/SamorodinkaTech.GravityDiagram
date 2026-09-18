namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Снимок порта — точки привязки дуги на стороне ноды.
/// Хранит как логические данные (сторона, смещение), так и
/// вычисленную мировую позицию.
/// </summary>
public sealed record DumpPort(
	/// <summary>Уникальный идентификатор порта.</summary>
	string Id,
	/// <summary>Отображаемый текст рядом с портом. Может быть null.</summary>
	string? Text,
	/// <summary>Идентификатор ноды-владельца.</summary>
	string NodeId,
	/// <summary>Сторона ноды (Top / Right / Bottom / Left).</summary>
	string Side,
	/// <summary>Нормализованное смещение вдоль стороны (0..1).</summary>
	float Offset,
	/// <summary>Смещение после ограничения границами стороны.</summary>
	float ClampedOffset = 0f,
	/// <summary>Абсолютные координаты порта на холсте.</summary>
	DumpVec2? WorldPosition = null);
