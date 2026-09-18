using System;
using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Port — точка присоединения дуги к узлу.
/// Каждый порт принадлежит конкретному узлу (PhysicsNode)
/// и вычисляет мировую позицию на основе текущей позиции узла.
/// </summary>
public sealed class Port
{
    public string Id { get; }
    public PhysicsNode Node { get; }
    public float OffsetX { get; }
    public float OffsetY { get; }

    public Port(string id, PhysicsNode node, float offsetX, float offsetY)
    {
        Id = id;
        Node = node;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>
    /// Вычисляет мировую позицию порта на основе текущей позиции узла.
    /// Дуги всегда используют этот метод для получения актуальных координат.
    /// </summary>
    public Vector2 GetWorldPosition() => Node.Position + new Vector2(OffsetX, OffsetY);
}
