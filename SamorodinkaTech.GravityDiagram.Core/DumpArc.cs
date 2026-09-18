using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Снимок одной дуги (ортогональной полилинии) между двумя портами.
/// Включает внутренние промежуточные точки и силы,
/// действующие на каждую из них.
/// </summary>
public sealed record DumpArc(
	/// <summary>Уникальный идентификатор дуги.</summary>
	string Id,
	/// <summary>Отображаемый текст рядом с дугой. Может быть null.</summary>
	string? Text,
	/// <summary>Идентификатор порта-отправителя.</summary>
	string FromPortId,
	/// <summary>Идентификатор порта-получателя.</summary>
	string ToPortId,
	/// <summary>Промежуточные ортогональные точки полилинии (не включая концы портов).</summary>
	DumpVec2[] InternalPoints = null!,
	/// <summary>Вектор силы, приложенной к каждой внутренней точке.</summary>
	DumpVec2[] InternalPointForces = null!);
