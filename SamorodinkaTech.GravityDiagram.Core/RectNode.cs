using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

public sealed class RectNode
{
	private readonly Dictionary<RectSide, PortFlow> _sideRules = new()
	{
		[RectSide.Top] = PortFlow.Both,
		[RectSide.Right] = PortFlow.Both,
		[RectSide.Bottom] = PortFlow.Both,
		[RectSide.Left] = PortFlow.Both,
	};

	public required DiagramId Id { get; init; }
	public required string Text { get; set; }

	public Vector2 Position { get; set; } // center
	public Vector2 Velocity { get; set; }

	public float Width { get; set; } = 160;
	public float Height { get; set; } = 80;

	public float Mass => MathF.Max(1f, Width * Height);

	public void SetSideFlow(RectSide side, PortFlow flow) => _sideRules[side] = flow;
	public PortFlow GetSideFlow(RectSide side) => _sideRules.TryGetValue(side, out var v) ? v : PortFlow.Both;

	public RectF Bounds => RectF.FromCenter(Position, Width, Height);
}
