using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Снимок состояния физической симуляции на один шаг.
/// Содержит агрегированные данные о силах и ссылки на состояния узлов и дуг.
/// </summary>
/// <param name="CreatedAtUtc">Момент времени (UTC), когда был создан этот снимок.</param>
/// <param name="Dt">Величина временного шага симуляции (в секундах).</param>
/// <param name="Nodes">Состояния всех узлов на данном шаге.</param>
/// <param name="Arcs">Состояния всех дуг на данном шаге.</param>
/// <param name="SumBackgroundGravityForce">Суммарная сила фонового гравитационного притяжения всех узлов к центру.</param>
/// <param name="SumOverlapRepulsionForce">Суммарная сила отталкивания узлов при перекрытии.</param>
/// <param name="SumConnectedArcAttractionForce">Суммарная сила притяжения узлов по соединяющим их дугам.</param>
/// <param name="SumArcPointEndpointForce">Суммарная сила, прижимающая промежуточные точки дуг к портам.</param>
/// <param name="SumTotalForce">Результирующая суммарная сила на всех узлах.</param>
public sealed record LayoutStepPreview(
	DateTimeOffset CreatedAtUtc,
	float Dt,
	IReadOnlyList<NodeStepPreview> Nodes,
	IReadOnlyList<ArcStepPreview> Arcs,
	Vector2 SumBackgroundGravityForce,
	Vector2 SumOverlapRepulsionForce,
	Vector2 SumConnectedArcAttractionForce,
	Vector2 SumArcPointEndpointForce,
	Vector2 SumTotalForce);
