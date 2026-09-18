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
    /// <summary>Уникальный идентификатор порта.</summary>
    public string Id { get; }
    /// <summary>Узел, которому принадлежит порт.</summary>
    public PhysicsNode Node { get; }
    /// <summary>Смещение по X относительно центра узла (положительное — вправо).</summary>
    public float OffsetX { get; }
    /// <summary>Смещение по Y относительно центра узла (положительное — вниз).</summary>
    public float OffsetY { get; }

    /// <param name="id">Уникальный идентификатор порта.</param>
    /// <param name="node">Узел-владелец порта.</param>
    /// <param name="offsetX">Смещение по X от центра узла.</param>
    /// <param name="offsetY">Смещение по Y от центра узла.</param>
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
