namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Ребро графа — связь от порта From к порту To.
/// Хранит ссылки на экземпляры Port.
/// </summary>
public sealed class Edge
{
    /// <summary>Исходящий порт (начало ребра).</summary>
    public Port From { get; }
    /// <summary>Входящий порт (конец ребра).</summary>
    public Port To { get; }

    public Edge(Port from, Port to) { From = from; To = to; }
}
