using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Demo;

/// <summary>
/// Кандидат на размещение подписи: тип, идентификатор, текст,
/// текущая позиция, предпочтительная позиция и размер.
/// </summary>
internal sealed record LabelCandidate(
    LabelKind Kind,
    DiagramId Id,
    string Text,
    Vector2 Origin,
    Vector2 PreferredOrigin,
    Vector2 Size);
