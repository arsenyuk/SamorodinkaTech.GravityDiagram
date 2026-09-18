using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Снимок состояния одной дуги (связи между двумя портами) на шаге симуляции.
/// Содержит идентификаторы портов и текущие позиции промежуточных точек ортогональной полилинии.
/// </summary>
/// <param name="Id">Идентификатор дуги в диаграмме.</param>
/// <param name="FromPortId">Идентификатор исходящего порта (начала дуги).</param>
/// <param name="ToPortId">Идентификатор входящего порта (конца дуги).</param>
/// <param name="InternalPoints">Промежуточные точки ортогональной полилинии дуги (не включая порты).</param>
public sealed record ArcStepPreview(
	DiagramId Id,
	DiagramId FromPortId,
	DiagramId ToPortId,
	IReadOnlyList<ArcPointStepPreview> InternalPoints);
