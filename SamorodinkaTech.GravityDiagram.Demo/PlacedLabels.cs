using System.Collections.Generic;
using Avalonia;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Demo;

/// <summary>
/// Результат размещения подписей: прямоугольники портов, дуг и общий список
/// всех прямоугольников подписей (для проверки пересечений).
/// </summary>
internal sealed record PlacedLabels(
    IReadOnlyDictionary<DiagramId, Rect> PortLabelRects,
    IReadOnlyDictionary<DiagramId, Rect> ArcLabelRects,
    IReadOnlyList<Rect> AllLabelRects);
