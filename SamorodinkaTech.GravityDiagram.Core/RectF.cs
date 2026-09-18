using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Оси выровненный прямоугольник. Координаты (X, Y) — левый верхний угол;
/// Width, Height — размеры.
/// </summary>
public readonly record struct RectF(float X, float Y, float Width, float Height)
{
	/// <summary>Координата левого края.</summary>
	public float Left => X;
	/// <summary>Координата верхнего края.</summary>
	public float Top => Y;
	/// <summary>Координата правого края (X + Width).</summary>
	public float Right => X + Width;
	/// <summary>Координата нижнего края (Y + Height).</summary>
	public float Bottom => Y + Height;

	/// <summary>Центр прямоугольника.</summary>
	public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);

	/// <summary>Создаёт прямоугольник по центру и размерам.</summary>
	public static RectF FromCenter(Vector2 center, float width, float height)
		=> new(center.X - width / 2f, center.Y - height / 2f, width, height);

	/// <summary>Проверяет, пересекаются ли два прямоугольника (границы не включены).</summary>
	public bool Intersects(in RectF other)
		=> !(Right <= other.Left || other.Right <= Left || Bottom <= other.Top || other.Bottom <= Top);

	/// <summary>Проверяет, находится ли точка внутри прямоугольника (включая границы).</summary>
	public bool Contains(Vector2 p)
		=> p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

	/// <summary>Вычисляет величину перекрытия по осям X и Y.</summary>
	public (float overlapX, float overlapY) Overlap(in RectF other)
	{
		var ox = MathF.Min(Right, other.Right) - MathF.Max(Left, other.Left);
		var oy = MathF.Min(Bottom, other.Bottom) - MathF.Max(Top, other.Top);
		return (ox, oy);
	}
}
