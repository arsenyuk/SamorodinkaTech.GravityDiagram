using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

public readonly record struct RectF(float X, float Y, float Width, float Height)
{
	public float Left => X;
	public float Top => Y;
	public float Right => X + Width;
	public float Bottom => Y + Height;

	public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);

	public static RectF FromCenter(Vector2 center, float width, float height)
		=> new(center.X - width / 2f, center.Y - height / 2f, width, height);

	public bool Intersects(in RectF other)
		=> !(Right <= other.Left || other.Right <= Left || Bottom <= other.Top || other.Bottom <= Top);

	public bool Contains(Vector2 p)
		=> p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

	public (float overlapX, float overlapY) Overlap(in RectF other)
	{
		var ox = MathF.Min(Right, other.Right) - MathF.Max(Left, other.Left);
		var oy = MathF.Min(Bottom, other.Bottom) - MathF.Max(Top, other.Top);
		return (ox, oy);
	}
}
