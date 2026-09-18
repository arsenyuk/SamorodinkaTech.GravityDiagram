namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Корневой элемент дампа: связывает настройки физического движка
/// и полное состояние диаграммы в единый снимок для воспроизведения.
/// Совместим с JSON-схемами v4 и v5 — отсутствующие поля получают
/// значения по умолчанию.
/// </summary>
public sealed record DumpRoot(DumpSettings Settings, DumpDiagram Diagram);
