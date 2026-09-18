using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Узел графа — прямоугольник с позицией, размером, скоростью и портами.
/// Позиция — центр прямоугольника.
/// </summary>
public sealed class PhysicsNode
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Width = 160f;
    public float Height = 80f;
    public string Label = "";

    public const float DefaultWidth = 160f;
    public const float DefaultHeight = 80f;

    public Vector2 PortLeft => new(Position.X - Width / 2, Position.Y);
    public Vector2 PortRight => new(Position.X + Width / 2, Position.Y);

    public PhysicsNode(string label, float x, float y)
    {
        Label = label;
        Position = new Vector2(x, y);
    }
}
