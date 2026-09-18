using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Дуга — ортогональный маршрут между двумя портами.
/// Хранит список точек маршрута (internal points).
/// </summary>
public sealed class Arc
{
    /// <summary>Связь графа (ребро), для которого построен этот маршрут.</summary>
    public Edge Edge { get; }
    /// <summary>Список точек маршрута (ортогональные сегменты полилинии).</summary>
    public List<Vector2> Points { get; set; } = new();

    public Arc(Edge edge) { Edge = edge; }
}
