using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Прямоугольная нода графа: позиция задаётся центром, размер — шириной и высотой.
/// Хранит правила потока данных (входящий/исходящий) по каждой стороне.
/// </summary>
public sealed class RectNode
{
	private readonly Dictionary<RectSide, PortFlow> _sideRules = new()
	{
		[RectSide.Top] = PortFlow.Both,
		[RectSide.Right] = PortFlow.Both,
		[RectSide.Bottom] = PortFlow.Both,
		[RectSide.Left] = PortFlow.Both,
	};

	/// <summary>Уникальный идентификатор ноды.</summary>
	public required DiagramId Id { get; init; }
	/// <summary>Отображаемое имя ноды.</summary>
	public required string Text { get; set; }

	/// <summary>Центр ноды в мировых координатах.</summary>
	public Vector2 Position { get; set; }
	/// <summary>Текущая скорость ноды (для интегрирования движения).</summary>
	public Vector2 Velocity { get; set; }
	/// <summary>Смещение за последний шаг симуляции (для корректировки дуг).</summary>
	public Vector2 LastMovementDelta { get; set; }

	/// <summary>Ширина прямоугольника ноды.</summary>
	public float Width { get; set; } = 160;
	/// <summary>Высота прямоугольника ноды.</summary>
	public float Height { get; set; } = 80;

	/// <summary>Задаёт правила потока данных для указанной стороны (входящий/исходящий/оба).</summary>
	public void SetSideFlow(RectSide side, PortFlow flow) => _sideRules[side] = flow;
	/// <summary>Возвращает правила потока данных для указанной стороны.</summary>
	public PortFlow GetSideFlow(RectSide side) => _sideRules.TryGetValue(side, out var v) ? v : PortFlow.Both;

	/// <summary>Прямоугольные границы ноды, вычисленные из центра и размеров.</summary>
	public RectF Bounds => RectF.FromCenter(Position, Width, Height);
}
