using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Дуга — ортогональный маршрут между двумя портами.
/// Хранит список точек маршрута (internal points).
/// </summary>
public sealed class Arc
{
    public Edge Edge { get; }
    public List<Vector2> Points { get; set; } = new();

    public Arc(Edge edge) { Edge = edge; }
}
