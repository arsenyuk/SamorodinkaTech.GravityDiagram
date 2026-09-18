using SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Настройки физического движка компоновки. Управляют массой нод,
/// силами взаимодействия, параметрами дуг и ограничениями расстояний.
/// Находится в глобальном пространстве имён для обратной совместимости.
/// </summary>
public sealed class LayoutSettings
{
	/// <summary>Масса всех нод (единая для всех). Влияет на ускорение: a = F / m.</summary>
	public float NodeMass { get; set; } = 12.8f;

	/// <summary>Параметр смягчения (softening) для гравитационного взаимодействия.</summary>
	public float Softening { get; set; } = 40f;
	/// <summary>
	/// Сила попарного притяжения между нодами (гравитационная).
	/// Намеренно мала, т.к. сила масштабируется произведением масс нод.
	/// </summary>
	public float BackgroundPairGravity { get; set; } = 0.12f;
	/// <summary>Длина покоя пружины ребра (расстояние, при котором сила пружины равна нулю).</summary>
	public float EdgeSpringRestLength { get; set; } = 220f;

	/// <summary>
	/// Сила взаимного притяжения прямоугольников, соединённых дугой.
	/// Реализовано как «пружина» вдоль дуги (только притягивает, не отталкивает).
	/// </summary>
	public float ConnectedArcAttractionK { get; set; } = 2.2f;

	// --- Физика промежуточных точек дуг ---
	/// <summary>
	/// Сила притяжения между смежными промежуточными точками дуги.
	/// Точки дуги считаются безмассовыми: не накапливают скорость.
	/// </summary>
	public float ArcPointAttractionK { get; set; } = 6.0f;

	/// <summary>
	/// Коэффициент скорости движения точек дуги: dX = force * ArcPointMoveFactor * dt.
	/// </summary>
	public float ArcPointMoveFactor { get; set; } = 0.035f;

	/// <summary>
	/// Сила отталкивания точек дуги от расширенных границ нод.
	/// Величина силы пропорциональна глубине проникновения в зону допуска.
	/// </summary>
	public float ArcPointNodeRepulsionK { get; set; } = 1200f;

	/// <summary>
	/// Расстояние слияния: если две соседние точки дуги ближе, чем это значение,
	/// они объединяются в одну (среднее арифметическое).
	/// </summary>
	public float ArcPointMergeDistance { get; set; } = 2.0f;

	/// <summary>Количество итераций ограничений для удержания точек дуги за пределами нод.</summary>
	public int ArcPointConstraintIterations { get; set; } = 6;

	/// <summary>Дополнительный зазор между точками/сегментами дуги и границами нод.</summary>
	public float ArcPointExtraClearance { get; set; } = 0f;

	/// <summary>Максимальное количество промежуточных точек одной дуги (защита от runaway insertion).</summary>
	public int MaxArcInternalPoints { get; set; } = 64;

	/// <summary>
	/// Если true, дуги стремятся быть максимально короткими
	/// (длина покоя пружины считается равной нулю).
	/// </summary>
	public bool MinimizeArcLength { get; set; } = false;

	/// <summary>Минимальное расстояние между краями прямоугольников (edge-to-edge).</summary>
	public float MinNodeSpacing { get; set; } = 0f;

	/// <summary>Включить жёсткий пошаговый ограничитель, enforcing MinNodeSpacing.</summary>
	public bool UseHardMinSpacing { get; set; } = false;
	/// <summary>Количество итераций жёсткого ограничителя расстояний.</summary>
	public int HardMinSpacingIterations { get; set; } = 4;
	/// <summary>Допуск (slop) жёсткого ограничителя расстояний.</summary>
	public float HardMinSpacingSlop { get; set; } = 0.5f;

	/// <summary>Сила отталкивания перекрывающихся прямоугольников.</summary>
	public float OverlapRepulsionK { get; set; } = 90f;

	/// <summary>
	/// Дополнительный множитель усиления отталкивания при UseHardMinSpacing=false.
	/// В этом режиме отталкивание — основной механизм быстрого устранения пересечений.
	/// </summary>
	public float SoftOverlapBoostWhenHardDisabled { get; set; } = 4f;
	/// <summary>Коэффициент сопротивления среды (затухание скорости).</summary>
	public float Drag { get; set; } = 2.2f;
	/// <summary>Максимальная скорость ноды (за один шаг).</summary>
	public float MaxSpeed { get; set; } = 2500f;

	/// <summary>
	/// Порог нормализации сил: нормализация применяется только если max(force) > threshold.
	/// Предотвращает шумовые малые силы от управления системой.
	/// </summary>
	public float ForceNormalizationThreshold { get; set; } = 1.0f;

	/// <summary>Параметры начальной маршрутизации дуг (тип, фиксация концов по нормали).</summary>
	public ArcLayoutOptions ArcLayoutOptions { get; set; } = new ArcLayoutOptions();
}
