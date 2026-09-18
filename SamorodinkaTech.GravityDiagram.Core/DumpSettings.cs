namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Параметры физического движка на момент создания дампа.
/// Хранит коэффициенты гравитации, пружин, демпфирования
/// и ограничения.spacing — всё, что влияет на раскладку.
/// </summary>
public sealed record DumpSettings(
	/// <summary>Масса каждой ноды; определяет силу гравитационного притяжения.</summary>
	float NodeMass,
	/// <summary>Мягкое отталкивание нод друг от друга при сближении.</summary>
	float Softening,
	/// <summary>Коэффициент фонарной гравитации между всеми парами нод.</summary>
	float BackgroundPairGravity,
	/// <summary>Длина покоя пружины дуги (сегмент между внутренними точками).</summary>
	float EdgeSpringRestLength,
	/// <summary>Сила притяжения дуг между собой (одноимённые концы).</summary>
	float ConnectedArcAttractionK,
	/// <summary>Учитывать ли рёбра при вычислении длины дуг для минимизации.</summary>
	bool MinimizeArcLength,
	/// <summary>Минимально допустимое расстояние между центрами нод.</summary>
	float MinNodeSpacing,
	/// <summary>Использовать жёсткую проверку вместо штрафа при нарушении MinNodeSpacing.</summary>
	bool UseHardMinSpacing,
	/// <summary>Число итераций жёсткого выталкивания нод за шаг.</summary>
	int HardMinSpacingIterations,
	/// <summary>Допустимое отклонение при жёстком выталкивании (в пикселях).</summary>
	float HardMinSpacingSlop,
	/// <summary>Коэффициент отталкивания при перекрытии прямоугольников нод.</summary>
	float OverlapRepulsionK,
	/// <summary>Усиление отталкивания, когда UseHardMinSpacing = false.</summary>
	float SoftOverlapBoostWhenHardDisabled,
	/// <summary>Коэффициент демпфирования скорости (0 = нет торможения, 1 = мгновенная остановка).</summary>
	float Drag,
	/// <summary>Абсолютный лимит скорости ноды за шаг (пиксели/шаг).</summary>
	float MaxSpeed,
	/// <summary>Сила пружины, тянущую внутренние точки дуг к среднему положению.</summary>
	float ArcPointAttractionK = 6f,
	/// <summary>Доля перемещения внутренней точки дуги за шаг (0..1).</summary>
	float ArcPointMoveFactor = 0.035f,
	/// <summary>Сила отталкивания внутренних точек дуг от границ нод.</summary>
	float ArcPointNodeRepulsionK = 1200f,
	/// <summary>Расстояние слияния двух соседних внутренних точек дуги.</summary>
	float ArcPointMergeDistance = 2f,
	/// <summary>Число итераций проектирования нарушений границ нод для дуг.</summary>
	int ArcPointConstraintIterations = 6,
	/// <summary>Дополнительный зазор между внутренней точкой дуги и границей ноды.</summary>
	float ArcPointExtraClearance = 0f,
	/// <summary>Максимальное число внутренних точек одной дуги.</summary>
	int MaxArcInternalPoints = 64);
