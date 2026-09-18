namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Полный снимок состояния диаграммы: все ноды, порты и дуги
/// в том порядке, в котором они были добавлены.
/// </summary>
public sealed record DumpDiagram(DumpNode[] Nodes, DumpPort[] Ports, DumpArc[] Arcs);
