using System.Numerics;
using SamorodinkaTech.GravityDiagram.NewModel;

namespace SamorodinkaTech.GravityDiagram.NewModel.Tests;

/// <summary>
/// Тесты для OrthogonalRouter — ортогональной маршрутизации дуг между узлами графа.
///
/// Ортогональные дуги — это ломаные линии, состоящие только из горизонтальных
/// и вертикальных сегментов (как в схемах электрических цепей). Дуга должна
/// идти от порта исходящей ноды к порту входящей ноды, не проходя сквозь
/// прямоугольники других нод.
///
/// Маршрут вычисляется методом ComputeRoute, который:
/// 1. Строит L-образный маршрут между двумя портами
/// 2. Проверяет каждый сегмент на пересечение с прямоугольниками нод
/// 3. Если пересечение есть — вычисляет обход (detour) вокруг прямоугольника
/// 4. Удаляет сегменты нулевой длины и объединяет коллинеарные
/// 5. Выталкивает промежуточные точки из прямоугольников нод
/// </summary>
public class OrthogonalRouterTests
{
    /// <summary>Ширина ноды по умолчанию (из PhysicsNode.DefaultWidth).</summary>
    private const float NodeW = PhysicsNode.DefaultWidth;

    /// <summary>Высота ноды по умолчанию (из PhysicsNode.DefaultHeight).</summary>
    private const float NodeH = PhysicsNode.DefaultHeight;

    // =====================================================================
    // Группа 1: Интеграционные тесты маршрутизации (ComputeRoute)
    //
    // Проверяют полный цикл: от вычисления маршрута до проверки,
    // что ни одна промежуточная точка не находится внутри чужого прямоугольника.
    // =====================================================================

    /// <summary>
    /// Горизонтальный сценарий: дуга A→C проходит через ноду B.
    ///
    /// Расположение:  A(0,0) — B(300,0) — C(600,0), все одной линии по Y=0.
    /// Порты: A.right=(80,0), C.left=(520,0).
    /// L-маршрут: (80,0)→(520,0) — горизонтальный отрезок, проходящий через B.
    ///
    /// Ожидаемое поведение:
    /// - Маршрут должен содержать более 3 точек ( есть обход )
    /// - Ни одна промежуточная точка маршрута не должна находиться внутри rect B
    /// </summary>
    [Fact]
    public void FirstSegment_GoesIntoNodeZone1_MustGenerateNewPoint()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("B", 300, 0) { Width = NodeW, Height = NodeH },
            new("C", 600, 0) { Width = NodeW, Height = NodeH },
        };

        var p1 = new Vector2(80, 0);   // правый порт A
        var p2 = new Vector2(520, 0);  // левый порт C

        // Предусловие: прямая (80,0)→(520,0) действительно пересекает rect B
        var bZone1 = new RectF(300 - NodeW / 2, 0 - NodeH / 2, NodeW, NodeH);
        Assert.True(OrthogonalRouter.SegmentIntersectsRect(p1, p2, bZone1));

        // Маршрут должен содержать >3 точек (obход добавляет промежуточные точки)
        var route = OrthogonalRouter.ComputeRoute(p1, p2, 0, 2, nodes);
        Assert.True(route.Count > 3, $"Expected >3 points, got {route.Count}");

        // Никакая промежуточная точка не внутри rect B
        for (var k = 1; k < route.Count - 1; k++)
            Assert.False(bZone1.Contains(route[k]), $"Point [{k}] inside B");
    }

    /// <summary>
    /// Вертикальный сценарий: дуга A→C проходит через ноду B.
    ///
    /// Расположение:  A(0,0) — B(0,200) — C(0,400), все одной линии по X=0.
    /// Порты: A.bottom=(0,40), C.top=(0,360).
    /// L-маршрут: (0,40)→(0,360) — вертикальный отрезок, проходящий через B.
    ///
    /// Ожидаемое поведение:
    /// - Маршрут должен содержать >3 точек (обход)
    /// - Ни одна промежуточная точка не внутри rect B
    /// </summary>
    [Fact]
    public void VerticalSegment_GoesIntoNode_GeneratesDetour()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("B", 0, 200) { Width = NodeW, Height = NodeH },
            new("C", 0, 400) { Width = NodeW, Height = NodeH },
        };

        var p1 = new Vector2(0, NodeH / 2);      // нижний порт A
        var p2 = new Vector2(0, 400 - NodeH / 2); // верхний порт C

        var bZone1 = new RectF(0 - NodeW / 2, 200 - NodeH / 2, NodeW, NodeH);
        Assert.True(OrthogonalRouter.SegmentIntersectsRect(p1, p2, bZone1));

        var route = OrthogonalRouter.ComputeRoute(p1, p2, 0, 2, nodes);
        Assert.True(route.Count > 3, $"Expected >3 points, got {route.Count}");

        for (var k = 1; k < route.Count - 1; k++)
            Assert.False(bZone1.Contains(route[k]), $"Point [{k}] inside B");
    }

    /// <summary>
    /// Нет препятствий: A и C расположены вертикально, между ними нет нод.
    ///
    /// Маршрут не должен содержать лишних точек обхода.
    /// Допускается до 4 точек (L-образный маршрут).
    /// </summary>
    [Fact]
    public void NoObstacle_NoExtraPoints()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("C", 0, 400) { Width = NodeW, Height = NodeH },
        };

        var p1 = new Vector2(0, NodeH / 2);
        var p2 = new Vector2(0, 400 - NodeH / 2);

        var route = OrthogonalRouter.ComputeRoute(p1, p2, 0, 1, nodes);
        Assert.True(route.Count <= 4, $"Expected ≤4 points, got {route.Count}");
    }

    // =====================================================================
    // Группа 2: SegmentIntersectsRect — строгое пересечение отрезка с rect
    //
    /// Метод SegmentIntersectsRect определяет, пересекает ли отрезок a→b
    /// внутреннюю область прямоугольника rect. Граничные случаи (касание
    /// угла, совпадение с границей, отрезок на границе) возвращают false.
    ///
    /// Алгоритм:
    /// 1. Горизонтальный отрезок: проверка строгого попадания по Y внутрь rect
    ///    и перекрытия проекции по X
    /// 2. Вертикальный отрезок: аналогично по X и Y
    /// 3. Диагональный: проверка пересечения с четырьмя рёбрами rect
    ///    (строгое пересечение через Cross product, касание = false)
    ///
    /// Граничный случай: если начальная точка внутри rect — это выход изнутри,
    /// а не пересечение. Метод возвращает false.
    // =====================================================================

    /// <summary>
    /// Горизонтальный отрезок (50,100)→(200,100) проходит через rect (100,80,60,40).
    /// Отрезок лежит на Y=100, который строго внутри rect по Y (80..120).
    /// Проекция по X перекрывается: 50..200 пересекается с 100..160.
    /// </summary>
    [Fact]
    public void HorizontalThroughRect_ReturnsTrue()
    {
        var rect = new RectF(100, 80, 60, 40);
        var a = new Vector2(50, 100);
        var b = new Vector2(200, 100);

        Assert.True(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Вертикальный отрезок (100,50)→(100,200) проходит через rect (80,100,40,60).
    /// Отрезок лежит на X=100, который строго внутри rect по X (80..120).
    /// Проекция по Y перекрывается: 50..200 пересекается с 100..160.
    /// </summary>
    [Fact]
    public void VerticalThroughRect_ReturnsTrue()
    {
        var rect = new RectF(80, 100, 40, 60);
        var a = new Vector2(100, 50);
        var b = new Vector2(100, 200);

        Assert.True(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Отрезок проходит ровно по нижней границе rect (Y = rect.Y + rect.Height).
    /// Касание границы НЕ считается пересечением.
    /// </summary>
    [Fact]
    public void TouchingRectEdge_ReturnsFalse()
    {
        var rect = new RectF(100, 80, 60, 40);
        var edgeY = rect.Y + rect.Height; // 120
        var a = new Vector2(50, edgeY);
        var b = new Vector2(200, edgeY);

        Assert.False(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Отрезок проходит через угол rect (точка (100,80) — левый верхний угол).
    /// Касание угла НЕ считается пересечением.
    /// </summary>
    [Fact]
    public void TouchingRectCorner_ReturnsFalse()
    {
        var rect = new RectF(100, 80, 60, 40);
        var a = new Vector2(rect.X, rect.Y); // (100, 80)
        var b = new Vector2(200, rect.Y);

        Assert.False(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Отрезок лежит на левой границе rect (X = rect.X).
    /// Совпадение с границей НЕ считается пересечением.
    /// </summary>
    [Fact]
    public void SegmentOnRectBorder_ReturnsFalse()
    {
        var rect = new RectF(100, 80, 60, 40);
        var a = new Vector2(rect.X, 50);   // (100, 50)
        var b = new Vector2(rect.X, 200);  // (100, 200)

        Assert.False(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Отрезок проходит полностью МИМО rect (Y=50, rect по Y: 100..140).
    /// Нет пересечения ни по Y, ни по X.
    /// </summary>
    [Fact]
    public void SegmentMissesRect_ReturnsFalse()
    {
        var rect = new RectF(100, 100, 60, 40);
        var a = new Vector2(50, 50);
        var b = new Vector2(200, 50);

        Assert.False(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Отрезок НАЧИНАЕТСЯ внутри rect и выходит наружу.
    /// Метод должен вернуть true (пересечение).
    /// </summary>
    [Fact]
    public void SegmentStartsInsideRect_ReturnsTrue()
    {
        var rect = new RectF(100, 100, 60, 40);
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        var a = new Vector2(cx, cy);
        var b = new Vector2(200, cy);

        Assert.True(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Диагональный отрезок (80,80)→(180,140) пересекает rect (100,100,60,40).
    /// Проверяется пересечение через Cross product с рёбрами rect.
    /// </summary>
    [Fact]
    public void DiagonalCrossesRect_ReturnsTrue()
    {
        var rect = new RectF(100, 100, 60, 40);
        var a = new Vector2(80, 80);
        var b = new Vector2(180, 140);

        Assert.True(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    /// <summary>
    /// Диагональный отрезок проходит через угол rect (100,100).
    /// Касание угла — не пересечение.
    /// </summary>
    [Fact]
    public void DiagonalTouchesCorner_ReturnsFalse()
    {
        var rect = new RectF(100, 100, 60, 40);
        var a = new Vector2(80, 80);
        var b = new Vector2(rect.X, rect.Y); // (100, 100)

        Assert.False(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
    }

    // =====================================================================
    // Группа 3: ShiftSharedPoints — сдвиг общей точки
    //
    // Если три последовательные точки маршрута (prev, curr, next) лежат
    // на одной прямой и движутся в одном направлении, то общая точка curr
    // сдвигается в этом направлении на ShiftOffset пикселей.
    //
    // Это нужно для визуального разделения параллельных сегментов дуг,
    // чтобы они не накладывались друг на друга.
    // =====================================================================

    /// <summary>
    /// Три горизонтальные точки, движение вправо (X растёт): (0,0)→(50,0)→(100,0).
    /// Общая точка (50,0) должна сдвинуться вправо на ShiftOffset → (70,0).
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_Right_ShiftsForward()
    {
        var route = new List<Vector2> { new(0, 0), new(50, 0), new(100, 0) };

        OrthogonalRouter.ShiftSharedPoints(route, OrthogonalRouter.ShiftOffset);

        Assert.Equal(50 + OrthogonalRouter.ShiftOffset, route[1].X, 1);
        Assert.Equal(0f, route[1].Y, 1);
    }

    /// <summary>
    /// Три горизонтальные точки, движение влево (X убывает): (100,0)→(50,0)→(0,0).
    /// Общая точка (50,0) должна сдвинуться влево на ShiftOffset → (30,0).
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_Left_ShiftsForward()
    {
        var route = new List<Vector2> { new(100, 0), new(50, 0), new(0, 0) };

        OrthogonalRouter.ShiftSharedPoints(route, OrthogonalRouter.ShiftOffset);

        Assert.Equal(50 - OrthogonalRouter.ShiftOffset, route[1].X, 1);
        Assert.Equal(0f, route[1].Y, 1);
    }

    /// <summary>
    /// Три вертикальные точки, движение вверх (Y убывает): (0,100)→(0,50)→(0,0).
    /// Общая точка (0,50) должна сдвинуться вверх на ShiftOffset → (0,30).
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_Up_ShiftsForward()
    {
        var route = new List<Vector2> { new(0, 100), new(0, 50), new(0, 0) };

        OrthogonalRouter.ShiftSharedPoints(route, OrthogonalRouter.ShiftOffset);

        Assert.Equal(0f, route[1].X, 1);
        Assert.Equal(50 - OrthogonalRouter.ShiftOffset, route[1].Y, 1);
    }

    /// <summary>
    /// Три вертикальные точки, движение вниз (Y растёт): (0,0)→(0,50)→(0,100).
    /// Общая точка (0,50) должна сдвинуться вниз на ShiftOffset → (0,70).
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_Down_ShiftsForward()
    {
        var route = new List<Vector2> { new(0, 0), new(0, 50), new(0, 100) };

        OrthogonalRouter.ShiftSharedPoints(route, OrthogonalRouter.ShiftOffset);

        Assert.Equal(0f, route[1].X, 1);
        Assert.Equal(50 + OrthogonalRouter.ShiftOffset, route[1].Y, 1);
    }

    /// <summary>
    /// Точка поворота (горизонт → вертикаль): (0,0)→(50,0)→(50,100).
    /// Это угол, а не коллинеарные сегменты. Точка НЕ сдвигается.
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_Turn_ShiftsNothing()
    {
        var route = new List<Vector2> { new(0, 0), new(50, 0), new(50, 100) };

        OrthogonalRouter.ShiftSharedPoints(route, OrthogonalRouter.ShiftOffset);

        Assert.Equal(50f, route[1].X, 1);
        Assert.Equal(0f, route[1].Y, 1);
    }

    // =====================================================================
    // Группа 4: TryShiftSharedPoint — сдвиг общей точки двух отрезков
    //
    // TryShiftSharedPoint принимает два отрезка с общей точкой:
    //   segment1 = (a → shared), segment2 = (shared → b)
    // Если оба отрезка коллинеарны (оба горизонтальны или оба вертикальны)
    // и движутся в одну сторону — общая точка сдвигается в этом направлении.
    //
    // Метод не изменяет точки — только возвращает true/false.
    // Сдвиг выполняется вызывающим кодом (ShiftSharedPoints).
    // =====================================================================

    /// <summary>
    /// Два горизонтальных отрезка вправо: (0,0)→(50,0) и (50,0)→(100,0).
    /// Общая точка (50,0) должна быть сдвинута вправо.
    /// </summary>
    [Fact]
    public void TryShiftSharedPoint_HorizontalRight_ReturnsTrue()
    {
        var a = new Vector2(0, 0);
        var shared = new Vector2(50, 0);
        var b = new Vector2(100, 0);

        Assert.True(OrthogonalRouter.TryShiftSharedPoint(a, shared, b, 20));
    }

    /// <summary>
    /// Два горизонтальных отрезка влево: (100,0)→(50,0) и (50,0)→(0,0).
    /// Общая точка (50,0) должна быть сдвинута влево.
    /// </summary>
    [Fact]
    public void TryShiftSharedPoint_HorizontalLeft_ReturnsTrue()
    {
        var a = new Vector2(100, 0);
        var shared = new Vector2(50, 0);
        var b = new Vector2(0, 0);

        Assert.True(OrthogonalRouter.TryShiftSharedPoint(a, shared, b, 20));
    }

    /// <summary>
    /// Два вертикальных отрезка вверх (Y убывает): (0,100)→(0,50) и (0,50)→(0,0).
    /// Общая точка (0,50) должна быть сдвинута вверх.
    /// </summary>
    [Fact]
    public void TryShiftSharedPoint_VerticalUp_ReturnsTrue()
    {
        var a = new Vector2(0, 100);
        var shared = new Vector2(0, 50);
        var b = new Vector2(0, 0);

        Assert.True(OrthogonalRouter.TryShiftSharedPoint(a, shared, b, 20));
    }

    /// <summary>
    /// Два вертикальных отрезка вниз (Y растёт): (0,0)→(0,50) и (0,50)→(0,100).
    /// Общая точка (0,50) должна быть сдвинута вниз.
    /// </summary>
    [Fact]
    public void TryShiftSharedPoint_VerticalDown_ReturnsTrue()
    {
        var a = new Vector2(0, 0);
        var shared = new Vector2(0, 50);
        var b = new Vector2(0, 100);

        Assert.True(OrthogonalRouter.TryShiftSharedPoint(a, shared, b, 20));
    }

    /// <summary>
    /// Отрезки образуют угол: (0,0)→(50,0) горизонтально и (50,0)→(50,100) вертикально.
    /// Разные направления — общая точка НЕ сдвигается.
    /// </summary>
    [Fact]
    public void TryShiftSharedPoint_Turn_ReturnsFalse()
    {
        var a = new Vector2(0, 0);
        var shared = new Vector2(50, 0);
        var b = new Vector2(50, 100);

        Assert.False(OrthogonalRouter.TryShiftSharedPoint(a, shared, b, 20));
    }

    /// <summary>
    /// Два отрезка в противоположных горизонтальных направлениях:
    /// (0,0)→(50,0) вправо и (50,0)→(0,0) влево.
    /// Направления разные — общая точка НЕ сдвигается.
    /// </summary>
    [Fact]
    public void TryShiftSharedPoint_OppositeDirections_ReturnsFalse()
    {
        var a = new Vector2(0, 0);
        var shared = new Vector2(50, 0);
        var b = new Vector2(0, 0);

        Assert.False(OrthogonalRouter.TryShiftSharedPoint(a, shared, b, 20));
    }

    // =====================================================================
    // Группа 4b: ShiftSharedPoints — сдвиг всех общих точек в маршруте
    //
    // ShiftSharedPoints обходит весь маршрут и сдвигает общие точки
    // коллинеарных сегментов. Использует TryShiftSharedPoint для проверки.
    // =====================================================================

    /// <summary>
    /// Маршрут из 5 горизонтальных точек вправо: (0,0)→(30,0)→(60,0)→(90,0)→(120,0).
    /// Три промежуточные точки должны быть сдвинуты вправо.
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_AllHorizontalRight_ShiftsAll()
    {
        var route = new List<Vector2>
        {
            new(0, 0), new(30, 0), new(60, 0), new(90, 0), new(120, 0)
        };

        OrthogonalRouter.ShiftSharedPoints(route, 20);

        Assert.Equal(50f, route[1].X, 1);   // 30 + 20
        Assert.Equal(80f, route[2].X, 1);   // 60 + 20
        Assert.Equal(110f, route[3].X, 1);  // 90 + 20
    }

    /// <summary>
    /// Маршрут с поворотом: (0,0)→(50,0)→(50,100)→(100,100).
    /// Точка поворота (50,0) НЕ сдвигается — это угол.
    /// Точка (50,100) НЕ сдвигается — тоже угол.
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_WithTurns_ShiftsNothing()
    {
        var route = new List<Vector2>
        {
            new(0, 0), new(50, 0), new(50, 100), new(100, 100)
        };

        OrthogonalRouter.ShiftSharedPoints(route, 20);

        Assert.Equal(50f, route[1].X, 1);
        Assert.Equal(0f, route[1].Y, 1);
        Assert.Equal(50f, route[2].X, 1);
        Assert.Equal(100f, route[2].Y, 1);
    }

    /// <summary>
    /// Смешанный маршрут: горизонталь + поворот + вертикаль.
    /// (0,0)→(50,0)→(50,100)→(50,200).
    /// Первая точка (50,0) — угол, не сдвигается.
    /// Вторая точка (50,100) — коллинеарна с (50,0) и (50,200), сдвигается вниз.
    /// </summary>
    [Fact]
    public void ShiftSharedPoints_MixedRoute_ShiftsCollinearOnly()
    {
        var route = new List<Vector2>
        {
            new(0, 0), new(50, 0), new(50, 100), new(50, 200)
        };

        OrthogonalRouter.ShiftSharedPoints(route, 20);

        Assert.Equal(50f, route[1].X, 1);
        Assert.Equal(0f, route[1].Y, 1);
        Assert.Equal(50f, route[2].X, 1);
        Assert.Equal(120f, route[2].Y, 1); // 100 + 20
    }

    // =====================================================================
    // Группа 4c: PushOutFromNodes — выталкивание точек из rect нод
    //
    // PushOutFromNodes проверяет каждую промежуточную точку маршрута.
    // Если точка внутри rect какой-либо ноды — выталкивает к ближайшему
    // краю rect с отступом PushOutDistance.
    // =====================================================================

    /// <summary>
    /// Точка (50, 50) находится внутри rect ноды (20,20,60,60).
    /// PushOutFromNodes должна вытолкнуть её к ближайшему краю.
    /// </summary>
    [Fact]
    public void PushOutFromNodes_PointInsideRect_PushesOut()
    {
        var nodes = new List<PhysicsNode>
        {
            new("X", 50, 50) { Width = 60, Height = 60 },
        };

        var route = new List<Vector2>
        {
            new(0, 50),
            new(50, 50),  // внутри rect X (20,20,60,60)
            new(100, 50)
        };

        OrthogonalRouter.PushOutFromNodes(route, nodes);

        // Точка (50,50) должна быть вытолкнута за пределы rect
        var rect = new RectF(20, 20, 60, 60);
        Assert.False(rect.Contains(route[1]),
            $"Point ({route[1].X},{route[1].Y}) still inside rect");
    }

    /// <summary>
    /// Точка вне rect — не должна сдвигаться.
    /// </summary>
    [Fact]
    public void PushOutFromNodes_PointOutsideRect_Unchanged()
    {
        var nodes = new List<PhysicsNode>
        {
            new("X", 50, 50) { Width = 60, Height = 60 },
        };

        var route = new List<Vector2>
        {
            new(0, 0),
            new(50, 0),  // вне rect X
            new(100, 0)
        };

        var before = route[1];
        OrthogonalRouter.PushOutFromNodes(route, nodes);

        Assert.Equal(before.X, route[1].X, 1);
        Assert.Equal(before.Y, route[1].Y, 1);
    }

    /// <summary>
    /// Точка внутри rect — выталкивается к ближайшему краю.
    /// Если точка ближе к левому краю — выталкивается влево.
    /// </summary>
    [Fact]
    public void PushOutFromNodes_CloserToLeft_PushesLeft()
    {
        var nodes = new List<PhysicsNode>
        {
            new("X", 100, 50) { Width = 80, Height = 40 },
        };

        var route = new List<Vector2>
        {
            new(0, 50),
            new(65, 50),  // rect: (60,30,80,40), ближе к левому краю (60)
            new(200, 50)
        };

        OrthogonalRouter.PushOutFromNodes(route, nodes);

        // Точка должна быть вытолкнута влево (route[1].X < 60)
        Assert.True(route[1].X < 60,
            $"Expected X < 60 (left of rect), got {route[1].X}");
    }

    /// <summary>
    /// Точка внутри rect — выталкивается к ближайшему краю.
    /// Горизонтальный сегмент проходит через rect ближе к верхнему краю.
    /// Выталкивается вверх.
    /// </summary>
    [Fact]
    public void PushOutFromNodes_CloserToTop_PushesUp()
    {
        var nodes = new List<PhysicsNode>
        {
            new("X", 100, 100) { Width = 80, Height = 80 },
        };

        var route = new List<Vector2>
        {
            new(0, 65),
            new(100, 65),  // rect: (60,60,80,80), ближе к верхнему краю (60)
            new(200, 65)
        };

        OrthogonalRouter.PushOutFromNodes(route, nodes);

        // Точка должна быть вытолкнута вверх (route[1].Y < 60)
        Assert.True(route[1].Y < 60,
            $"Expected Y < 60 (above rect), got {route[1].Y}");
    }

    // =====================================================================
    // Группа 4d: SegmentsIntersectStrict — строгое пересечение двух отрезков
    //
    // SegmentsIntersectStrict определяет, пересекаются ли два отрезка
    // через Cross product. Все d1..d4 должны быть ненулевыми (касание = false).
    // =====================================================================

    /// <summary>
    /// Крестообразное пересечение: (0,0)→(10,10) и (0,10)→(10,0).
    /// Отрезки пересекаются в точке (5,5).
    /// </summary>
    [Fact]
    public void SegmentsIntersectStrict_Cross_ReturnsTrue()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(10, 10);
        var c = new Vector2(0, 10);
        var d = new Vector2(10, 0);

        Assert.True(OrthogonalRouter.SegmentsIntersectStrict(a, b, c, d));
    }

    /// <summary>
    /// Параллельные горизонтальные отрезки: (0,0)→(10,0) и (0,5)→(10,5).
    /// Не пересекаются.
    /// </summary>
    [Fact]
    public void SegmentsIntersectStrict_ParallelHorizontal_ReturnsFalse()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(10, 0);
        var c = new Vector2(0, 5);
        var d = new Vector2(10, 5);

        Assert.False(OrthogonalRouter.SegmentsIntersectStrict(a, b, c, d));
    }

    /// <summary>
    /// Параллельные вертикальные отрезки: (0,0)→(0,10) и (5,0)→(5,10).
    /// Не пересекаются.
    /// </summary>
    [Fact]
    public void SegmentsIntersectStrict_ParallelVertical_ReturnsFalse()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(0, 10);
        var c = new Vector2(5, 0);
        var d = new Vector2(5, 10);

        Assert.False(OrthogonalRouter.SegmentsIntersectStrict(a, b, c, d));
    }

    /// <summary>
    /// Касание на конце: (0,0)→(5,5) и (5,5)→(10,0).
    /// Конец одного отрезка совпадает с началом другого — касание = false.
    /// </summary>
    [Fact]
    public void SegmentsIntersectStrict_TouchingAtEndpoint_ReturnsFalse()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(5, 5);
        var c = new Vector2(5, 5);
        var d = new Vector2(10, 0);

        Assert.False(OrthogonalRouter.SegmentsIntersectStrict(a, b, c, d));
    }

    /// <summary>
    /// Отрезки не пересекаются и не касаются: (0,0)→(5,0) и (6,0)→(10,0).
    /// Горизонтальные, на одной линии, но с разрывом.
    /// </summary>
    [Fact]
    public void SegmentsIntersectStrict_CollinearNoOverlap_ReturnsFalse()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(5, 0);
        var c = new Vector2(6, 0);
        var d = new Vector2(10, 0);

        Assert.False(OrthogonalRouter.SegmentsIntersectStrict(a, b, c, d));
    }

    /// <summary>
    /// Один отрезок полностью внутри другого: (0,0)→(10,0) и (2,0)→(8,0).
    /// Коллинеарные, перекрываются, но Cross product даёт 0 — касание = false.
    /// </summary>
    [Fact]
    public void SegmentsIntersectStrict_CollinearOverlap_ReturnsFalse()
    {
        var a = new Vector2(0, 0);
        var b = new Vector2(10, 0);
        var c = new Vector2(2, 0);
        var d = new Vector2(8, 0);

        Assert.False(OrthogonalRouter.SegmentsIntersectStrict(a, b, c, d));
    }
    //
    // Тестируют полный цикл: ComputeRoute → обход rect → финальный маршрут
    // без пересечений. Проверяют что ComputeDetour, MergeCollinear,
    // PushOutFromNodes работают корректно в составе ComputeRoute.
    // =====================================================================

    /// <summary>
    /// Горизонтальная дуга проходит через ноду B.
    /// ComputeDetour должен построить обход сверху или снизу.
    /// Финальный маршрут не пересекает rect B.
    /// </summary>
    [Fact]
    public void ComputeRoute_HorizontalObstacle_DetoursAboveOrBelow()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("B", 300, 0) { Width = NodeW, Height = NodeH },
            new("C", 600, 0) { Width = NodeW, Height = NodeH },
        };

        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(80, 0), new Vector2(520, 0), 0, 2, nodes);

        // Маршрут должен иметь промежуточные точки (обход)
        Assert.True(route.Count > 3);

        // Все сегменты — горизонтальные или вертикальные (ортогональность)
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < 0.1f;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < 0.1f;
            Assert.True(horiz || vert,
                $"Segment {k} not axis-aligned: {route[k]} → {route[k + 1]}");
        }

        // Ни одна точка маршрута не внутри rect B
        var bRect = new RectF(300 - NodeW / 2, 0 - NodeH / 2, NodeW, NodeH);
        for (var k = 1; k < route.Count - 1; k++)
            Assert.False(bRect.Contains(route[k]),
                $"Point [{k}] ({route[k].X},{route[k].Y}) inside B rect");
    }

    /// <summary>
    /// Вертикальная дуга проходит через ноду B.
    /// ComputeDetour должен построить обход слева или справа.
    /// </summary>
    [Fact]
    public void ComputeRoute_VerticalObstacle_DetoursLeftOrRight()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("B", 0, 200) { Width = NodeW, Height = NodeH },
            new("C", 0, 400) { Width = NodeW, Height = NodeH },
        };

        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, NodeH / 2), new Vector2(0, 400 - NodeH / 2), 0, 2, nodes);

        Assert.True(route.Count > 3);

        var bRect = new RectF(0 - NodeW / 2, 200 - NodeH / 2, NodeW, NodeH);
        for (var k = 1; k < route.Count - 1; k++)
            Assert.False(bRect.Contains(route[k]),
                $"Point [{k}] inside B rect");
    }

    /// <summary>
    /// Нет препятствий — маршрут прямой (или L-образный), без лишних точек обхода.
    /// Проверяет что MergeCollinear корректно удаляет коллинеарные точки.
    /// </summary>
    [Fact]
    public void ComputeRoute_NoObstacle_MinimalRoute()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("C", 0, 400) { Width = NodeW, Height = NodeH },
        };

        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, NodeH / 2), new Vector2(0, 400 - NodeH / 2), 0, 1, nodes);

        // Маршрут должен быть компактным (≤4 точки)
        Assert.True(route.Count <= 4,
            $"Expected ≤4 points for direct route, got {route.Count}");

        // Все сегменты ортогональны
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < 0.1f;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < 0.1f;
            Assert.True(horiz || vert);
        }
    }

    /// <summary>
    /// Два последовательных сегмента в одном направлении (горизонтально вправо)
    /// объединяются в один. Проверяет MergeCollinear.
    /// </summary>
    [Fact]
    public void ComputeRoute_MergesCollinearSegments()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("C", 400, 0) { Width = NodeW, Height = NodeH },
        };

        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(80, 0), new Vector2(320, 0), 0, 1, nodes);

        // Без препятствий маршрут должен быть компактным
        Assert.True(route.Count <= 4,
            $"Expected ≤4 points, got {route.Count}");
    }

    /// <summary>
    /// PushOutFromNodes: если промежуточная точка попадает внутрь rect ноды,
    /// она выталкивается к ближайшему краю. Проверяем что ни одна точка
    /// финального маршрута не внутри rect.
    /// </summary>
    [Fact]
    public void ComputeRoute_PointsPushedOutOfRects()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = NodeW, Height = NodeH },
            new("B", 200, 0) { Width = NodeW, Height = NodeH },
            new("C", 400, 0) { Width = NodeW, Height = NodeH },
        };

        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(80, 0), new Vector2(320, 0), 0, 2, nodes);

        var bRect = new RectF(200 - NodeW / 2, 0 - NodeH / 2, NodeW, NodeH);
        for (var k = 1; k < route.Count - 1; k++)
            Assert.False(bRect.Contains(route[k]),
                $"Point [{k}] inside B rect");

        // Все сегменты ортогональны
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < 0.1f;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < 0.1f;
            Assert.True(horiz || vert,
                $"Segment {k} not axis-aligned: {route[k]} → {route[k + 1]}");
        }
    }

    // =====================================================================
    // Группа 6: Интеграционный тест — дуга следует за нодой
    //
    // Проверяет архитектуру: Port привязан к ноде, Edge ссылается на Port,
    // ComputeRoute берёт координаты из Port.GetWorldPosition(node).
    // При перемещении ноды координаты порта меняются → маршрут пересчитывается.
    // =====================================================================

    [Fact]
    public void ArcFollowsNode_WhenNodeMoves_ArcPointsUpdate()
    {
        // Две ноды A и C, между ними дуга через порты
        var a = new PhysicsNode("A", 0, 0) { Width = 160, Height = 80 };
        var c = new PhysicsNode("C", 400, 0) { Width = 160, Height = 80 };

        var portA = new Port("portA", a, a.Width / 2, 0);
        var portC = new Port("portC", c, -c.Width / 2, 0);
        var edge = new Edge(portA, portC);

        var nodes = new List<PhysicsNode> { a, c };
        var arcs = new List<Arc> { new(edge) };

        // Вычисляем начальный маршрут
        var route1 = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(),
            portC.GetWorldPosition(),
            0, 1, nodes);
        arcs[0].Points = route1;

        // Начальная позиция дуги
        var startX = arcs[0].Points[0].X;

        // Перемещаем ноду A вправо на 200
        a.Position = new Vector2(200, 0);

        // Пересчитываем маршрут с новыми координатами портов
        var route2 = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(),
            portC.GetWorldPosition(),
            0, 1, nodes);
        arcs[0].Points = route2;

        // Начальная точка дуги должна сместиться вместе с нодой A
        Assert.False(Math.Abs(startX - arcs[0].Points[0].X) < 0.1f,
            "Arc start point did not follow node A");

        // Первая точка маршрута должна совпадать с позицией порта A
        var portAPos = portA.GetWorldPosition();
        Assert.Equal(portAPos.X, arcs[0].Points[0].X, 1);
        Assert.Equal(portAPos.Y, arcs[0].Points[0].Y, 1);

        // Последняя точка маршрута должна совпадать с позицией порта C
        var portCPos = portC.GetWorldPosition();
        Assert.Equal(portCPos.X, arcs[0].Points[^1].X, 1);
        Assert.Equal(portCPos.Y, arcs[0].Points[^1].Y, 1);

        // Маршрут должен быть ортогональным
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var horiz = Math.Abs(arcs[0].Points[k].Y - arcs[0].Points[k + 1].Y) < 0.1f;
            var vert = Math.Abs(arcs[0].Points[k].X - arcs[0].Points[k + 1].X) < 0.1f;
            Assert.True(horiz || vert,
                $"Segment {k} not axis-aligned after move");
        }
    }

    [Fact]
    public void ArcStartEndMatchPortPositions()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 160, Height = 80 };
        var c = new PhysicsNode("C", 400, 0) { Width = 160, Height = 80 };

        var portA = new Port("portA", a, a.Width / 2, 0);
        var portC = new Port("portC", c, -c.Width / 2, 0);
        var edge = new Edge(portA, portC);

        var nodes = new List<PhysicsNode> { a, c };
        var arcs = new List<Arc> { new(edge) };

        // Вычисляем маршрут
        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portC.GetWorldPosition(),
            0, 1, nodes);

        // Первая точка = позиция порта A
        var expectedStart = portA.GetWorldPosition();
        Assert.Equal(expectedStart.X, arcs[0].Points[0].X, 1);
        Assert.Equal(expectedStart.Y, arcs[0].Points[0].Y, 1);

        // Последняя точка = позиция порта C
        var expectedEnd = portC.GetWorldPosition();
        Assert.Equal(expectedEnd.X, arcs[0].Points[^1].X, 1);
        Assert.Equal(expectedEnd.Y, arcs[0].Points[^1].Y, 1);
    }

    [Fact]
    public void WhenNodeMovesAndRecompute_ArcFollowsPort()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 160, Height = 80 };
        var c = new PhysicsNode("C", 400, 0) { Width = 160, Height = 80 };

        var portA = new Port("portA", a, a.Width / 2, 0);
        var portC = new Port("portC", c, -c.Width / 2, 0);
        var edge = new Edge(portA, portC);

        var nodes = new List<PhysicsNode> { a, c };
        var arcs = new List<Arc> { new(edge) };

        // Начальное вычисление
        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portC.GetWorldPosition(),
            0, 1, nodes);
        var startAfterLoad = arcs[0].Points[0].X;

        // Перемещаем ноду A
        a.Position = new Vector2(300, 100);

        // Пересчитываем
        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portC.GetWorldPosition(),
            0, 1, nodes);

        // Начальная точка дуги изменилась
        Assert.NotEqual(startAfterLoad, arcs[0].Points[0].X);

        // Совпадает с позицией порта A
        var portAPos = portA.GetWorldPosition();
        Assert.Equal(portAPos.X, arcs[0].Points[0].X, 1);
        Assert.Equal(portAPos.Y, arcs[0].Points[0].Y, 1);
    }

    // =====================================================================
    // Тест: при сдвиге ноды B влево дуга A→B должна породить
    // ортогональные сегменты вместо диагонального отрезка,
    // и новые точки должны быть вне узлов.
    // =====================================================================

    [Fact]
    public void WhenNodeBMovesLeft_ArcGeneratesOrthogonalSegments()
    {
        // A сверху, B снизу и далеко вправо.
        // Правый порт A и левый порт B — начальный L-маршрут не пересекает ноды.
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 300, 200) { Width = 80, Height = 80 };

        var portA = new Port("A_right", a, a.Width / 2, 0); // (40, 0)
        var portB = new Port("B_left", b, -b.Width / 2, 0); // (260, 200)
        var edge = new Edge(portA, portB);

        var nodes = new List<PhysicsNode> { a, b };
        var arcs = new List<Arc> { new(edge) };

        // Начальный маршрут: L-образный (3 точки)
        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        var initialCount = arcs[0].Points.Count;

        // Сдвигаем B далеко влево — левый порт B оказывается внутри тела A.
        // A rect: (-40,-40,80,80) → X: -40..40
        // B.X=-20 → B left port = -20-40 = -60 (за пределами A, но маршрут проходит через A)
        // B.X=0 → B left port = 0-40 = -40 (на границе A)
        // Нужно чтобы L-маршрут (40,0)→(portB.X,0)→(portB.X,200) пересёк A rect.
        // portB.X < 40 → горизонтальный сегмент (40,0)→(portB.X,0) проходит через A rect.
        b.Position = new Vector2(-20, 200);

        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        // После сдвига маршрут должен стать сложнее (обход)
        Assert.True(arcs[0].Points.Count > initialCount,
            $"Expected more route points after move ({arcs[0].Points.Count} vs initial {initialCount})");

        // Все сегменты должны быть ортогональными
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var p1 = arcs[0].Points[k];
            var p2 = arcs[0].Points[k + 1];
            var horiz = Math.Abs(p1.Y - p2.Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(p1.X - p2.X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({p1.X:F1},{p1.Y:F1})→({p2.X:F1},{p2.Y:F1}) is not axis-aligned");
        }

        // Промежуточные точки (не порты) должны быть вне rect нод
        var rectA = new RectF(a.Position.X - a.Width / 2, a.Position.Y - a.Height / 2, a.Width, a.Height);
        var rectB = new RectF(b.Position.X - b.Width / 2, b.Position.Y - b.Height / 2, b.Width, b.Height);

        for (var k = 1; k < arcs[0].Points.Count - 1; k++)
        {
            var pt = arcs[0].Points[k];
            Assert.False(rectA.Contains(pt),
                $"Intermediate point {k} ({pt.X:F1},{pt.Y:F1}) is inside node A rect");
            Assert.False(rectB.Contains(pt),
                $"Intermediate point {k} ({pt.X:F1},{pt.Y:F1}) is inside node B rect");
        }

        // Первая точка = порт A, последняя = порт B
        var start = portA.GetWorldPosition();
        var end = portB.GetWorldPosition();
        Assert.Equal(start.X, arcs[0].Points[0].X, 1);
        Assert.Equal(start.Y, arcs[0].Points[0].Y, 1);
        Assert.Equal(end.X, arcs[0].Points[^1].X, 1);
        Assert.Equal(end.Y, arcs[0].Points[^1].Y, 1);
    }

    // =====================================================================
    // Горизонтальное расположение: A слева, B справа.
    // Правый порт A → левый порт B.
    // При сдвиге B влево дуга не должна заходить внутрь A.
    // =====================================================================

    [Fact]
    public void Horizontal_AB_MoveBLeft_ArcDoesNotEnterA()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("A_right", a, a.Width / 2, 0);  // (40, 0)
        var portB = new Port("B_left", b, -b.Width / 2, 0);  // (160, 0)
        var edge = new Edge(portA, portB);

        var nodes = new List<PhysicsNode> { a, b };
        var arcs = new List<Arc> { new(edge) };

        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        var initialCount = arcs[0].Points.Count;

        // Сдвигаем B влево — левый порт B оказывается внутри A
        // A rect: (-40,-40,80,80) → X: -40..40
        // B.X=20 → portB.X = 20-40 = -20 (внутри A)
        b.Position = new Vector2(20, 0);

        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        // Все сегменты ортогональны
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var p1 = arcs[0].Points[k];
            var p2 = arcs[0].Points[k + 1];
            var horiz = Math.Abs(p1.Y - p2.Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(p1.X - p2.X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({p1.X:F1},{p1.Y:F1})→({p2.X:F1},{p2.Y:F1}) is not axis-aligned");
        }

        // Промежуточные сегменты (не первый и не последний) не проходят через тело A
        var rectA = new RectF(a.Position.X - a.Width / 2, a.Position.Y - a.Height / 2, a.Width, a.Height);
        for (var k = 1; k < arcs[0].Points.Count - 2; k++)
        {
            Assert.False(
                SegmentPassesThroughRect(arcs[0].Points[k], arcs[0].Points[k + 1], rectA),
                $"Segment {k} passes through node A");
        }

        // Первая точка = порт A, последняя = порт B
        var start = portA.GetWorldPosition();
        var end = portB.GetWorldPosition();
        Assert.Equal(start.X, arcs[0].Points[0].X, 1);
        Assert.Equal(start.Y, arcs[0].Points[0].Y, 1);
        Assert.Equal(end.X, arcs[0].Points[^1].X, 1);
        Assert.Equal(end.Y, arcs[0].Points[^1].Y, 1);
    }

    // =====================================================================
    // Горизонтальное расположение: A слева, B справа.
    // Левый порт B → правый порт A (обратная дуга).
    // При сдвиге A вправо дуга не должна заходить внутрь B.
    // =====================================================================

    [Fact]
    public void Horizontal_BA_MoveARight_ArcDoesNotEnterB()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portB = new Port("B_right", b, b.Width / 2, 0);  // (240, 0)
        var portA = new Port("A_left", a, -a.Width / 2, 0);  // (-40, 0)
        var edge = new Edge(portB, portA);

        var nodes = new List<PhysicsNode> { a, b };
        var arcs = new List<Arc> { new(edge) };

        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portB.GetWorldPosition(), portA.GetWorldPosition(),
            1, 0, nodes);

        // Сдвигаем A вправо — правый порт A оказывается внутри B
        // B rect: (160,-40,80,80) → X: 160..240
        // A.X=180 → portA.X = 180+40 = 220 (внутри B)
        a.Position = new Vector2(180, 0);

        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portB.GetWorldPosition(), portA.GetWorldPosition(),
            1, 0, nodes);


        // Все сегменты ортогональны
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var p1 = arcs[0].Points[k];
            var p2 = arcs[0].Points[k + 1];
            var horiz = Math.Abs(p1.Y - p2.Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(p1.X - p2.X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({p1.X:F1},{p1.Y:F1})→({p2.X:F1},{p2.Y:F1}) is not axis-aligned");
        }

        // Промежуточные сегменты (не первый и не последний) не проходят через тело B
        var rectB = new RectF(b.Position.X - b.Width / 2, b.Position.Y - b.Height / 2, b.Width, b.Height);
        for (var k = 1; k < arcs[0].Points.Count - 2; k++)
        {
            Assert.False(
                SegmentPassesThroughRect(arcs[0].Points[k], arcs[0].Points[k + 1], rectB),
                $"Segment {k} passes through node B");
        }

        // Первая точка = порт B, последняя = порт A
        var start = portB.GetWorldPosition();
        var end = portA.GetWorldPosition();
        Assert.Equal(start.X, arcs[0].Points[0].X, 1);
        Assert.Equal(start.Y, arcs[0].Points[0].Y, 1);
        Assert.Equal(end.X, arcs[0].Points[^1].X, 1);
        Assert.Equal(end.Y, arcs[0].Points[^1].Y, 1);
    }

    /// <summary>
    /// Проверяет, что отрезок a→b проходит через внутренность rect
    /// (не просто касается границы).
    /// </summary>
    private static bool SegmentPassesThroughRect(Vector2 a, Vector2 b, RectF rect)
    {
        var minX = rect.X;
        var maxX = rect.X + rect.Width;
        var minY = rect.Y;
        var maxY = rect.Y + rect.Height;

        // Горизонтальный сегмент
        if (Math.Abs(a.Y - b.Y) < OrthogonalRouter.AxisTolerance)
        {
            var cy = a.Y;
            if (cy > minY && cy < maxY)
            {
                var cxMin = Math.Min(a.X, b.X);
                var cxMax = Math.Max(a.X, b.X);
                // Сегмент пересекает rect если его X-диапазон перекрывается с rect
                // и хотя бы одна точка внутри rect (не на границе)
                if (cxMin < maxX && cxMax > minX)
                {
                    // Проверяем что сегмент gerçekten внутри (не только на границе)
                    var insideStart = a.X > minX && a.X < maxX;
                    var insideEnd = b.X > minX && b.X < maxX;
                    if (insideStart || insideEnd) return true;
                    // Или сегмент проходит сквозь rect
                    if (cxMin < minX && cxMax > maxX) return true;
                }
            }
        }
        // Вертикальный сегмент
        else if (Math.Abs(a.X - b.X) < OrthogonalRouter.AxisTolerance)
        {
            var cx = a.X;
            if (cx > minX && cx < maxX)
            {
                var cyMin = Math.Min(a.Y, b.Y);
                var cyMax = Math.Max(a.Y, b.Y);
                if (cyMin < maxY && cyMax > minY)
                {
                    var insideStart = a.Y > minY && a.Y < maxY;
                    var insideEnd = b.Y > minY && b.Y < maxY;
                    if (insideStart || insideEnd) return true;
                    if (cyMin < minY && cyMax > maxY) return true;
                }
            }
        }

        return false;
    }

    // =====================================================================
    // Горизонтальная дуга через ноду: цель по Y не совпадает с портом.
    // Детур должен дать строго ортогональные сегменты (нет петли).
    // =====================================================================

    [Fact]
    public void DetourAroundSourceNode_AllSegmentsAxisAligned()
    {
        // A справа, B слева и ниже. Правый порт A → левый порт B.
        // Дуга (80,0)→(-100,200) проходит через тело A.
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", -100, 200) { Width = 80, Height = 80 };

        var portA = new Port("A_right", a, a.Width / 2, 0);   // (40, 0)
        var portB = new Port("B_left", b, -b.Width / 2, 0);   // (-140, 200)
        var edge = new Edge(portA, portB);

        var nodes = new List<PhysicsNode> { a, b };
        var arcs = new List<Arc> { new(edge) };

        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        // Все сегменты строго ортогональны
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var p1 = arcs[0].Points[k];
            var p2 = arcs[0].Points[k + 1];
            var horiz = Math.Abs(p1.Y - p2.Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(p1.X - p2.X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({p1.X:F1},{p1.Y:F1})→({p2.X:F1},{p2.Y:F1}) is not axis-aligned");
        }

        // Нет петли: горизонтальные сегменты идут влево к цели
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var dx = arcs[0].Points[k + 1].X - arcs[0].Points[k].X;
            if (Math.Abs(dx) > OrthogonalRouter.AxisTolerance)
            {
                Assert.True(dx < 0,
                    $"Horizontal segment {k} goes right ({dx:F1}) instead of left toward target");
            }
        }

        // Промежуточные точки вне rect A
        var rectA = new RectF(a.Position.X - a.Width / 2, a.Position.Y - a.Height / 2, a.Width, a.Height);
        for (var k = 1; k < arcs[0].Points.Count - 1; k++)
        {
            Assert.False(rectA.Contains(arcs[0].Points[k]),
                $"Point {k} inside A rect");
        }
    }

    // =====================================================================
    // Простой случай: B ниже A, Right-порты.
    // Дуга A→B через правый порт A и левый порт B.
    // B смещён влево так, что порт B внутри A.
    // Детур не должен создавать петлю вправо.
    // =====================================================================

    [Fact]
    public void SimpleCase_BelowA_RightPorts_NoLoop()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 0, 200) { Width = 80, Height = 80 };

        var portA = new Port("A_right", a, a.Width / 2, 0);  // (40, 0)
        var portB = new Port("B_left", b, -b.Width / 2, 0);  // (-40, 200)
        var edge = new Edge(portA, portB);

        var nodes = new List<PhysicsNode> { a, b };
        var arcs = new List<Arc> { new(edge) };

        // Начальный маршрут
        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        // Сдвигаем B влево — порт B оказывается внутри A
        b.Position = new Vector2(-20, 200);

        arcs[0].Points = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        // Все сегменты ортогональны
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var p1 = arcs[0].Points[k];
            var p2 = arcs[0].Points[k + 1];
            var horiz = Math.Abs(p1.Y - p2.Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(p1.X - p2.X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({p1.X:F1},{p1.Y:F1})→({p2.X:F1},{p2.Y:F1}) not axis-aligned");
        }

        // Нет петли: нет горизонтальных сегментов вправо
        for (var k = 0; k < arcs[0].Points.Count - 1; k++)
        {
            var dx = arcs[0].Points[k + 1].X - arcs[0].Points[k].X;
            if (Math.Abs(dx) > OrthogonalRouter.AxisTolerance)
            {
                Assert.True(dx < 0,
                    $"Segment {k} goes right ({dx:F1}) — creates a loop!");
            }
        }

        // Промежуточные точки вне rect A
        var rectA = new RectF(a.Position.X - a.Width / 2, a.Position.Y - a.Height / 2, a.Width, a.Height);
        for (var k = 1; k < arcs[0].Points.Count - 1; k++)
        {
            Assert.False(rectA.Contains(arcs[0].Points[k]),
                $"Point {k} inside A rect");
        }
    }

    // =====================================================================
    // Тест: три ноды горизонтально, дуга B→C.
    // C смещена влево и вниз — дуга B→C заезжает на B.
    // =====================================================================

    [Fact]
    public void ThreeNodesHorizontally_BC_DetourProducesAxisAlignedRoute()
    {
        // Воспроизведение сценария из DEBUG-вывода:
        // B(300,0), C(600,0) — горизонтально. Дуга B→C.
        // C смещена влево(150,100) — порт C=(70,100), маршрут проходит через B.
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(380f, 0f), new Vector2(70f, 100f), 1, 2,
            new List<PhysicsNode>
            {
                new("A", 0, 0) { Width = 160, Height = 80 },
                new("B", 300, 0) { Width = 160, Height = 80 },
                new("C", 150, 100) { Width = 160, Height = 80 },
            });

        // Все сегменты ортогональны
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({route[k].X:F1},{route[k].Y:F1})→({route[k + 1].X:F1},{route[k + 1].Y:F1}) not axis-aligned");
        }

        // Ни один сегмент не пересекает B
        var rectB = new RectF(220f, -40f, 160f, 80f);
        for (var k = 0; k < route.Count - 1; k++)
        {
            Assert.False(
                SegmentPassesThroughRect(route[k], route[k + 1], rectB),
                $"Segment {k} passes through B");
        }

        // Первая точка = порт B, последняя = порт C
        Assert.Equal(380f, route[0].X, 1);
        Assert.Equal(70f, route[^1].X, 1);
    }

    // =====================================================================
    // Тест: A справа, B слева и ниже — детур без петли.
    // На основе пойманного(DEBUG) состояния когда сегмент (560,351)→(745,350)
    // был диагональным.
    // =====================================================================

    [Fact]
    public void RightPort_TargetBelowLeft_NoDiagonalSegments()
    {
        var a = new PhysicsNode("A", 500, 350) { Width = 160, Height = 80 };
        var b = new PhysicsNode("B", 200, 500) { Width = 160, Height = 80 };

        var portA = new Port("A_right", a, a.Width / 2, 0);  // (580, 350)
        var portB = new Port("B_left", b, -b.Width / 2, 0);  // (120, 500)
        var edge = new Edge(portA, portB);

        var nodes = new List<PhysicsNode> { a, b };

        var route = OrthogonalRouter.ComputeRoute(
            portA.GetWorldPosition(), portB.GetWorldPosition(),
            0, 1, nodes);

        // Все сегменты строго ортогональны
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({route[k].X:F1},{route[k].Y:F1})→({route[k + 1].X:F1},{route[k + 1].Y:F1}) not axis-aligned");
        }

        // Нет петли: горизонтальные сегменты идут влево к цели
        for (var k = 0; k < route.Count - 1; k++)
        {
            var dx = route[k + 1].X - route[k].X;
            if (Math.Abs(dx) > OrthogonalRouter.AxisTolerance)
            {
                Assert.True(dx < 0,
                    $"Segment {k} goes right ({dx:F1}) — loop!");
            }
        }

        // Промежуточные точки вне A
        var rectA = new RectF(a.Position.X - a.Width / 2, a.Position.Y - a.Height / 2, a.Width, a.Height);
        for (var k = 1; k < route.Count - 1; k++)
        {
            Assert.False(rectA.Contains(route[k]),
                $"Point {k} inside A rect");
        }
    }

    // Горизонтальный сегмент пересекает rect ноды
    [Fact]
    public void SegmentIntersectsRect_Horizontal()
    {
        var rect = new RectF(100, -50, 100, 100); // (100,-50)→(200,50)
        Assert.True(OrthogonalRouter.SegmentIntersectsRect(
            new Vector2(50, 0), new Vector2(250, 0), rect));
    }

    // Вертикальный сегмент пересекает rect ноды
    [Fact]
    public void SegmentIntersectsRect_Vertical()
    {
        var rect = new RectF(-50, 100, 100, 100); // (-50,100)→(50,200)
        Assert.True(OrthogonalRouter.SegmentIntersectsRect(
            new Vector2(0, 50), new Vector2(0, 250), rect));
    }

    // Горизонтальный сегмент касается rect по границе — не пересекает
    [Fact]
    public void SegmentIntersectsRect_Horizontal_TouchesBoundary()
    {
        var rect = new RectF(100, -50, 100, 100); // (100,-50)→(200,50)
        // Сегмент на нижней границе rect (Y=50)
        Assert.False(OrthogonalRouter.SegmentIntersectsRect(
            new Vector2(50, 50), new Vector2(250, 50), rect));
    }

    // Вертикальный сегмент касается rect по границе — не пересекает
    [Fact]
    public void SegmentIntersectsRect_Vertical_TouchesBoundary()
    {
        var rect = new RectF(-50, 100, 100, 100); // (-50,100)→(50,200)
        // Сегмент на правой границе rect (X=50)
        Assert.False(OrthogonalRouter.SegmentIntersectsRect(
            new Vector2(50, 50), new Vector2(50, 250), rect));
    }

    // =====================================================================
    // Тест из пойманного DEBUG-состояния:
    // Горизонтальный сегмент (560,350)→(763,350) заканчивается на границе rect (763,310,160,80).
    // ComputeDetour не должен создавать петлю через rect.
    // =====================================================================

    [Fact]
    public void ComputeDetour_SegmentEndsAtRectBoundary_NoLoop()
    {
        var a = new Vector2(560f, 350f);
        var b = new Vector2(763f, 350f);
        var rect = new RectF(763f, 310f, 160f, 80f);

        // Прямой сегмент не пересекает rect (касается границы)
        Assert.False(OrthogonalRouter.SegmentIntersectsRect(a, b, rect));
        // Сегмент на границе rect тоже не пересекает
        Assert.False(OrthogonalRouter.SegmentIntersectsRect(
            new Vector2(380, 350), new Vector2(400, 350),
            new RectF(400, 310, 160, 80)));

        // Но ComputeRoute всё равно должен обработать этот случай
        var nodes = new List<PhysicsNode>
        {
            new("A", 480, 350) { Width = 160, Height = 80 },
            new("B", 843, 350) { Width = 160, Height = 80 },
        };

        var route = OrthogonalRouter.ComputeRoute(a, b, 0, 1, nodes);

        // Все сегменты ортогональны
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({route[k].X:F1},{route[k].Y:F1})→({route[k + 1].X:F1},{route[k + 1].Y:F1}) not axis-aligned");
        }

        // Ни один сегмент не пересекает rect
        for (var k = 0; k < route.Count - 1; k++)
        {
            Assert.False(
                OrthogonalRouter.SegmentIntersectsRect(route[k], route[k + 1], rect),
                $"Segment {k} intersects rect");
        }
    }

    // =====================================================================
    // Пошаговые тесты ComputeDetour через ComputeRoute
    // =====================================================================

    /// <summary>
    /// Горизонтальный сегмент слева→справа пересекает ноду B.
    /// Обход должен идти сверху или снизу.
    /// </summary>
    [Fact]
    public void Detour_Horizontal_CrossesNode_FromLeft()
    {
        // a=(0,0) b=(200,0), нода B посередине: rect=(80,-40,40,80)
        var nodes = new List<PhysicsNode>
        {
            new("A", -20, 0) { Width = 40, Height = 40 },
            new("Obs", 100, 0) { Width = 40, Height = 80 },
            new("B", 220, 0) { Width = 40, Height = 40 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, 0), new Vector2(200, 0), 0, 2, nodes);

        AssertRouteValid(route, NodeRect(nodes[1]), 0, 2, nodes);
    }

    /// <summary>
    /// Горизонтальный сегмент справа→влево пересекает ноду.
    /// </summary>
    [Fact]
    public void Detour_Horizontal_CrossesNode_FromRight()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 220, 0) { Width = 40, Height = 40 },
            new("Obs", 100, 0) { Width = 40, Height = 80 },
            new("B", -20, 0) { Width = 40, Height = 40 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(200, 0), new Vector2(0, 0), 0, 2, nodes);

        AssertRouteValid(route, NodeRect(nodes[1]), 0, 2, nodes);
    }

    /// <summary>
    /// Вертикальный сегмент сверху→вниз пересекает ноду.
    /// </summary>
    [Fact]
    public void Detour_Vertical_CrossesNode_FromTop()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, -20) { Width = 40, Height = 40 },
            new("Obs", 0, 100) { Width = 80, Height = 40 },
            new("B", 0, 220) { Width = 40, Height = 40 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, 0), new Vector2(0, 200), 0, 2, nodes);

        AssertRouteValid(route, NodeRect(nodes[1]), 0, 2, nodes);
    }

    /// <summary>
    /// Вертикальный сегмент снизу→вверх пересекает ноду.
    /// </summary>
    [Fact]
    public void Detour_Vertical_CrossesNode_FromBottom()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 220) { Width = 40, Height = 40 },
            new("Obs", 0, 100) { Width = 80, Height = 40 },
            new("B", 0, -20) { Width = 40, Height = 40 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, 200), new Vector2(0, 0), 0, 2, nodes);

        AssertRouteValid(route, NodeRect(nodes[1]), 0, 2, nodes);
    }

    /// <summary>
    /// Горизонтальный сегмент, цель на границе rect сверху.
    /// </summary>
    [Fact]
    public void Detour_Horizontal_TargetOnNodeBoundary_Top()
    {
        // Нода B: rect=(100,-40,40,80). Цель b=(100,-40) — на верхней границе.
        var nodes = new List<PhysicsNode>
        {
            new("A", -20, 0) { Width = 40, Height = 40 },
            new("B", 120, 0) { Width = 40, Height = 80 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, 0), new Vector2(100, -40), 0, 1, nodes);

        AssertRouteValid(route, NodeRect(nodes[0]), 0, 1, nodes);
    }

    /// <summary>
    /// Горизонтальный сегмент, цель на границе rect снизу.
    /// </summary>
    [Fact]
    public void Detour_Horizontal_TargetOnNodeBoundary_Bottom()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", -20, 0) { Width = 40, Height = 40 },
            new("B", 120, 0) { Width = 40, Height = 80 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, 0), new Vector2(100, 40), 0, 1, nodes);

        AssertRouteValid(route, NodeRect(nodes[0]), 0, 1, nodes);
    }

    /// <summary>
    /// Горизонтальный сегмент, цель внутри rect.
    /// </summary>
    [Fact]
    public void Detour_Horizontal_TargetInsideNode()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", -20, 0) { Width = 40, Height = 40 },
            new("B", 120, 0) { Width = 40, Height = 80 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, 0), new Vector2(100, 0), 0, 1, nodes);

        AssertRouteValid(route, NodeRect(nodes[0]), 0, 1, nodes);
    }

    /// <summary>
    /// Source node detour: первый сегмент входит в исходную ноду.
    /// Маршрут должен идти влево к цели.
    /// </summary>
    [Fact]
    public void Detour_SourceNode_FirstSegmentEntersSource()
    {
        // A справа, B слева. Правый порт A=(40,0) → левый порт B=(-120,200).
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = 80, Height = 80 },
            new("B", -80, 200) { Width = 80, Height = 80 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(40, 0), new Vector2(-120, 200), 0, 1, nodes);

        AssertRouteValid(route, NodeRect(nodes[0]), 0, 1, nodes);

        // Первый сегмент должен идти влево (к цели)
        var firstDir = route[1] - route[0];
        Assert.True(firstDir.X < 0,
            $"First segment should go left, but dir.X={firstDir.X}");
    }

    /// <summary>
    /// Source node detour: цель внизу и слева — нет петли.
    /// </summary>
    [Fact]
    public void Detour_SourceNode_TargetBelowAndLeft_NoLoop()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = 80, Height = 80 },
            new("B", -80, 200) { Width = 80, Height = 80 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(40, 0), new Vector2(-120, 200), 0, 1, nodes);

        AssertRouteValid(route, NodeRect(nodes[0]), 0, 1, nodes);

        // Нет горизонтальных сегментов вправо
        for (var k = 0; k < route.Count - 1; k++)
        {
            var dx = route[k + 1].X - route[k].X;
            if (Math.Abs(dx) > OrthogonalRouter.AxisTolerance)
            {
                Assert.True(dx < 0,
                    $"Segment {k} goes right ({dx}) — loop!");
            }
        }
    }

    /// <summary>
    /// Вертикальный source node detour.
    /// </summary>
    [Fact]
    public void Detour_SourceNode_Vertical_FirstSegmentEntersSource()
    {
        var nodes = new List<PhysicsNode>
        {
            new("A", 0, 0) { Width = 80, Height = 80 },
            new("B", 200, -80) { Width = 80, Height = 80 },
        };
        var route = OrthogonalRouter.ComputeRoute(
            new Vector2(0, 40), new Vector2(200, -120), 0, 1, nodes);

        AssertRouteValid(route, NodeRect(nodes[0]), 0, 1, nodes);
    }

    private static RectF NodeRect(PhysicsNode node)
        => new(node.Position.X - node.Width / 2, node.Position.Y - node.Height / 2, node.Width, node.Height);

    /// <summary>
    /// Хелпер: проверяет что маршрут ортогонален и не пересекает obstacle.
    /// </summary>
    private static void AssertRouteValid(List<Vector2> route, RectF obstacle,
        int fromIdx, int toIdx, List<PhysicsNode> nodes)
    {
        // Все сегменты ортогональны
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < OrthogonalRouter.AxisTolerance;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < OrthogonalRouter.AxisTolerance;
            Assert.True(horiz || vert,
                $"Segment {k} ({route[k].X:F1},{route[k].Y:F1})→({route[k + 1].X:F1},{route[k + 1].Y:F1}) not axis-aligned");
        }

        // Ни один сегмент не пересекает obstacle
        for (var k = 0; k < route.Count - 1; k++)
        {
            Assert.False(
                OrthogonalRouter.SegmentIntersectsRect(route[k], route[k + 1], obstacle),
                $"Segment {k} ({route[k].X:F0},{route[k].Y:F0})→({route[k + 1].X:F0},{route[k + 1].Y:F0}) intersects obstacle");
        }
    }
}
