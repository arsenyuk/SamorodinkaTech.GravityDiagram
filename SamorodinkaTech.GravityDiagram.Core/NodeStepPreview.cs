using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Снимок состояния одного узла на шаге симуляции.
/// Включает текущие и предсказанные позиции, скорости, а также все компоненты сил,
/// действующие на узел до и после применения ограничений.
/// </summary>
/// <param name="Id">Идентификатор узла в диаграмме.</param>
/// <param name="Position">Текущая позиция центра узла.</param>
/// <param name="Velocity">Текущая скорость узла.</param>
/// <param name="Mass">Масса узла (определяет инерцию и силу гравитационного притяжения).</param>
/// <param name="ForceBackgroundGravity">Сила фонового гравитационного притяжения к центру диаграммы.</param>
/// <param name="ForceOverlapRepulsion">Сила отталкивания, возникающая при перекрытии с другими узлами.</param>
/// <param name="ForceConnectedArcAttraction">Сила притяжения, удерживающая узел на концах соединяющих дуг.</param>
/// <param name="ForceArcPointEndpoint">Сила, прижимающая промежуточные точки дуг к портам узла.</param>
/// <param name="ForceTotal">Результирующая сила, действующая на узел.</param>
/// <param name="PredictedPositionIfNoForces">Предсказание позиции при отсутствии любых сил (инерция).</param>
/// <param name="PredictedVelocityIfNoForces">Предсказание скорости при отсутствии любых сил.</param>
/// <param name="DeltaPositionIfNoForces">Смещение позиции при отсутствии любых сил.</param>
/// <param name="PredictedPositionBeforeConstraints">Предсказание позиции после применения сил, но до ограничений (граничные условия).</param>
/// <param name="PredictedVelocityBeforeConstraints">Предсказание скорости после применения сил, но до ограничений.</param>
/// <param name="PredictedPosition">Итоговая предсказанная позиция узла после всех ограничений.</param>
/// <param name="PredictedVelocity">Итоговая предсказанная скорость узла после всех ограничений.</param>
/// <param name="DeltaPositionBeforeConstraints">Смещение позиции до применения ограничений.</param>
/// <param name="DeltaPosition">Итоговое смещение позиции узла за один шаг симуляции.</param>
public sealed record NodeStepPreview(
	DiagramId Id,
	Vector2 Position,
	Vector2 Velocity,
	float Mass,
	Vector2 ForceBackgroundGravity,
	Vector2 ForceOverlapRepulsion,
	Vector2 ForceConnectedArcAttraction,
	Vector2 ForceArcPointEndpoint,
	Vector2 ForceTotal,
	Vector2 PredictedPositionIfNoForces,
	Vector2 PredictedVelocityIfNoForces,
	Vector2 DeltaPositionIfNoForces,
	Vector2 PredictedPositionBeforeConstraints,
	Vector2 PredictedVelocityBeforeConstraints,
	Vector2 PredictedPosition,
	Vector2 PredictedVelocity,
	Vector2 DeltaPositionBeforeConstraints,
	Vector2 DeltaPosition);
