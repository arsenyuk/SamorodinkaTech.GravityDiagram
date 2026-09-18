using System;
using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Axis-aligned rectangle (X, Y — левый верхний угол, Width, Height — размеры).
/// </summary>
public readonly record struct RectF(float X, float Y, float Width, float Height)
{
    /// <summary>Проверяет, находится ли точка внутри rect (строго, не на границе).</summary>
    public bool Contains(Vector2 p) =>
        p.X >= X && p.X <= X + Width && p.Y >= Y && p.Y <= Y + Height;
}
