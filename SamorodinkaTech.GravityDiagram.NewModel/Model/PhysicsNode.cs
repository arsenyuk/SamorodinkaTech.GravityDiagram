using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Узел графа — прямоугольник с позицией, размером, скоростью и портами.
/// Позиция — центр прямоугольника.
/// </summary>
public sealed class PhysicsNode
{
    /// <summary>Текущая позиция центра узла (в пикселях).</summary>
    public Vector2 Position;
    /// <summary>Текущая скорость узла (в пикселях/сек).</summary>
    public Vector2 Velocity;
    /// <summary>Ширина прямоугольника узла.</summary>
    public float Width = 160f;
    /// <summary>Высота прямоугольника узла.</summary>
    public float Height = 80f;
    /// <summary>Текстовая метка узла (отображается в центре).</summary>
    public string Label = "";

    /// <summary>Ширина узла по умолчанию (160 px).</summary>
    public const float DefaultWidth = 160f;
    /// <summary>Высота узла по умолчанию (80 px).</summary>
    public const float DefaultHeight = 80f;

    /// <summary>Позиция левого порта (левая грань прямоугольника по центру Y).</summary>
    public Vector2 PortLeft => new(Position.X - Width / 2, Position.Y);
    /// <summary>Позиция правого порта (правая грань прямоугольника по центру Y).</summary>
    public Vector2 PortRight => new(Position.X + Width / 2, Position.Y);

    /// <param name="label">Метка узла.</param>
    /// <param name="x">X-координата центра.</param>
    /// <param name="y">Y-координата центра.</param>
    public PhysicsNode(string label, float x, float y)
    {
        Label = label;
        Position = new Vector2(x, y);
    }
}
