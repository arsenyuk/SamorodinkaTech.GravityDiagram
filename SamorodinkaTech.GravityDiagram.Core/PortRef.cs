namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Ссылка на позицию порта: идентификатор ноды, сторона прямоугольника
/// и нормализованное смещение (0..1) вдоль этой стороны.
/// </summary>
public readonly record struct PortRef(DiagramId NodeId, RectSide Side, float Offset)
{
	/// <summary>Смещение, зажатое в диапазоне [0, 1].</summary>
	public float ClampedOffset => Math.Clamp(Offset, 0f, 1f);
}
