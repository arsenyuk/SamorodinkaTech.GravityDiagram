using System.Collections.Generic;
using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Demo;

/// <summary>
/// Маршрутизированная дуга: исходная дуга + рассчитанная полилиния
/// с точками для отрисовки и информацией для размещения подписи.
/// </summary>
internal sealed record RoutedArc(
    Arc Arc,
    List<Vector2> Polyline,
    Vector2 LabelBasePoint,
    Vector2 LabelNormal);
