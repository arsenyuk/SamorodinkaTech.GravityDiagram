namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Ребро графа — связь от порта From к порту To.
/// Хранит ссылки на экземпляры Port.
/// </summary>
public sealed class Edge
{
    public Port From { get; }
    public Port To { get; }

    public Edge(Port from, Port to) { From = from; To = to; }
}
