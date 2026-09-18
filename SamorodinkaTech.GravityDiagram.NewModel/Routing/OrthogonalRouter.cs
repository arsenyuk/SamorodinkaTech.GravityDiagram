using System;
using System.Collections.Generic;
using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Ортогональный маршрутизатор дуг между узлами графа.
///
/// Вычисляет путь от порта исходящей ноды к порту входящей ноды,
/// используя только горизонтальные и вертикальные сегменты (как в схемах).
/// Маршрут не должен пересекать прямоугольники других нод.
///
/// Алгоритм ComputeRoute:
/// 1. Строит прямой маршрут p1 → p2
/// 2. Итеративно (до MaxIterations) проверяет каждый сегмент на пересечение с rect нод
/// 3. Если пересечение найдено — вычисляет обход (detour) вокруг rect
/// 4. Удаляет сегменты нулевой длины
/// 5. Объединяет коллинеарные сегменты в одном направлении
/// 6. Сдвигает общие точки коллинеарных сегментов для визуального разделения
/// 7. Выталкивает промежуточные точки из rect нод
/// </summary>
public static class OrthogonalRouter
{
    /// <summary>Сдвиг общей точки коллинеарных сегментов (пиксели).</summary>
    public const float ShiftOffset = 20f;

    /// <summary>Отступ при выталкивании точки из rect ноды (пиксели).</summary>
    public const float PushOutDistance = 15f;

    /// <summary>Максимальное количество итераций поиска обхода.</summary>
    public const int MaxIterations = 6;

    /// <summary>Допуск для проверки осевой выравненности (горизонталь/вертикаль).</summary>
    public const float AxisTolerance = 0.1f;

    // =====================================================================
    // ComputeRoute — основной метод маршрутизации
    // =====================================================================

    /// <summary>
    /// Вычисляет ортогональный маршрут от p1 к p2, обходя rect всех нод.
    ///
    /// Параметры:
    ///   p1, p2 — начальная и конечная точки маршрута (порты нод)
    ///   fromIdx — индекс исходящей ноды (её rect не проверяется)
    ///   toIdx — индекс входящей ноды (её rect проверяется)
    ///   nodes — список всех нод графа
    ///
    /// Возвращает список точек маршрута (ортогональные сегменты).
    /// </summary>
    public static List<Vector2> ComputeRoute(
        Vector2 p1, Vector2 p2,
        int fromIdx, int toIdx,
        List<PhysicsNode> nodes)
    {
        // Снаппим порты к сетке до построения маршрута
        p1 = new Vector2((float)Math.Round(p1.X), (float)Math.Round(p1.Y));
        p2 = new Vector2((float)Math.Round(p2.X), (float)Math.Round(p2.Y));

        // Начальный маршрут — L-образный для ортогональности
        var route = new List<Vector2> { p1 };
        if (Math.Abs(p1.Y - p2.Y) > AxisTolerance)
            route.Add(new Vector2(p2.X, p1.Y)); // промежуточная точка
        route.Add(p2);

        // Итеративный поиск обходов для каждого сегмента
        for (var iter = 0; iter < MaxIterations; iter++)
        {
            var fixedSomething = false;

            for (var k = 0; k < route.Count - 1; k++)
            {
                var a = route[k];
                var b = route[k + 1];

                // Ищем ноду, rect которой пересекает сегмент a→b
                PhysicsNode? hitNode = null;
                RectF hitRect = default;

                foreach (var node in nodes)
                {
                    var ni = nodes.IndexOf(node);
                    // Пропускаем входящую ноду всегда.
                    // Исходящую ноду проверяем только для первого сегмента,
                    // и только если сегмент строго ВХОДИТ внутрь rect
                    // (середина сегмента внутри rect — не просто касание границы).
                    if (ni == toIdx) continue;
                    if (ni == fromIdx && k > 0) continue;

                    var rect = new RectF(
                        node.Position.X - node.Width / 2,
                        node.Position.Y - node.Height / 2,
                        node.Width,
                        node.Height);

                    if (SegmentIntersectsRect(a, b, rect))
                    {
                        // Для исходной ноды: сегмент должен строго входить внутрь rect
                        // (середина сегмента внутри rect), а не просто касаться границы.
                        if (ni == fromIdx)
                        {
                            var mid = (a + b) * 0.5f;
                            if (!(mid.X > rect.X && mid.X < rect.X + rect.Width &&
                                  mid.Y > rect.Y && mid.Y < rect.Y + rect.Height))
                                continue;

                            // Обход исходной ноды: выходим наружу по нормали порта,
                            // затем идём вдоль стороны узла (вверх/вниз для гориз. порта),
                            // и только потом поворачиваем к цели.
                            var margin = Math.Max(rect.Width, rect.Height) / 2 + PushOutDistance + 1f;
                            var exitPoint = a.X <= rect.X
                                ? new Vector2(a.X - margin, a.Y)       // Left port → влево
                                : a.X >= rect.X + rect.Width
                                    ? new Vector2(a.X + margin, a.Y)   // Right port → вправо
                                    : a.Y <= rect.Y
                                        ? new Vector2(a.X, a.Y - margin) // Top port → вверх
                                        : new Vector2(a.X, a.Y + margin); // Bottom port → вниз

                            // Точка вдоль стороны узла: выходим за пределы rect по Y
                            var cornerY = b.Y <= rect.Y + rect.Height / 2
                                ? rect.Y - margin   // Цель ниже центра → идём сверху
                                : rect.Y + rect.Height + margin; // Цель выше → снизу
                            var alongSide = new Vector2(exitPoint.X, cornerY);

                            // Поворот к цели
                            var turnToTarget = new Vector2(b.X, cornerY);

                            route.RemoveAt(k);
                            route.InsertRange(k, new[] { a, exitPoint, alongSide, turnToTarget, b });
                            fixedSomething = true;
                            break;
                        }

                        hitNode = node;
                        hitRect = rect;
                        break;
                    }
                }

                if (hitNode == null) continue;

                // Вычисляем обход вокруг hitRect и заменяем сегмент
                var detour = ComputeDetour(a, b, hitRect);
                route.RemoveAt(k);
                route.InsertRange(k, detour);

                fixedSomething = true;
                break; // начать проверку заново после замены
            }

            if (!fixedSomething) break;
        }

        // Удаляем сегменты нулевой длины
        for (var k = route.Count - 2; k >= 0; k--)
        {
            if (Vector2.Distance(route[k], route[k + 1]) < 1f)
                route.RemoveAt(k + 1);
        }

        // Объединяем коллинеарные сегменты в одном направлении
        MergeCollinear(route);

        // Сдвигаем общие точки коллинеарных сегментов для визуального разделения
        ShiftSharedPoints(route, ShiftOffset);

        // Выталкиваем промежуточные точки из rect нод
        PushOutFromNodes(route, nodes);

        // Гарантируем, что крайние точки маршрута совпадают с портами
        // и что первый/последний сегменты ортогональны.
        if (route.Count >= 2)
        {
            route[0] = p1;
            route[^1] = p2;

            // Первый сегмент: если не ортогонален — корректируем вторую точку
            var firstDir = route[1] - route[0];
            if (Math.Abs(firstDir.X) > AxisTolerance && Math.Abs(firstDir.Y) > AxisTolerance)
            {
                // Диагональный первый сегмент — делаем ортогональным по доминирующей оси
                if (Math.Abs(firstDir.X) >= Math.Abs(firstDir.Y))
                    route[1] = new Vector2(route[1].X, route[0].Y);
                else
                    route[1] = new Vector2(route[0].X, route[1].Y);
            }

            // Последний сегмент: если не ортогонален — корректируем предпоследнюю точку
            if (route.Count >= 3)
            {
                var lastDir = route[^1] - route[^2];
                if (Math.Abs(lastDir.X) > AxisTolerance && Math.Abs(lastDir.Y) > AxisTolerance)
                {
                    if (Math.Abs(lastDir.X) >= Math.Abs(lastDir.Y))
                        route[^2] = new Vector2(route[^2].X, route[^1].Y);
                    else
                        route[^2] = new Vector2(route[^1].X, route[^2].Y);
                }
            }
        }

        // Снаппинг ВСЕХ точек к сетке (включая порты)
        for (var k = 0; k < route.Count; k++)
        {
            route[k] = new Vector2(
                (float)Math.Round(route[k].X),
                (float)Math.Round(route[k].Y));
        }

        // Гарантируем L-точку (拐角): если p1.Y != p2.Y, но L-точка потеряна — восстанавливаем
        if (route.Count == 2 && Math.Abs(route[0].Y - route[1].Y) > AxisTolerance)
        {
            route.Insert(1, new Vector2(route[1].X, route[0].Y));
        }

#if DEBUG
        // Проверка: каждый сегмент финального маршрута ортогонален
        for (var k = 0; k < route.Count - 1; k++)
        {
            var horiz = Math.Abs(route[k].Y - route[k + 1].Y) < AxisTolerance;
            var vert = Math.Abs(route[k].X - route[k + 1].X) < AxisTolerance;
            if (!horiz && !vert)
            {
                Console.Error.WriteLine(
                    $"[DEBUG] Segment [{k}] ({route[k].X:F2},{route[k].Y:F2})→" +
                    $"({route[k + 1].X:F2},{route[k + 1].Y:F2}) is not axis-aligned");
            }
        }

        // Проверка: каждый сегмент не пересекает rect нод
        for (var k = 0; k < route.Count - 1; k++)
        {
            var segA = route[k];
            var segB = route[k + 1];
            foreach (var node in nodes)
            {
                var ni = nodes.IndexOf(node);
                if (ni == fromIdx || ni == toIdx) continue;

                var rect = new RectF(
                    node.Position.X - node.Width / 2,
                    node.Position.Y - node.Height / 2,
                    node.Width,
                    node.Height);

                if (SegmentIntersectsRect(segA, segB, rect))
                {
                    throw new InvalidOperationException(
                        $"[DEBUG] Final route segment ({segA.X:F0},{segA.Y:F0})→({segB.X:F0},{segB.Y:F0}) " +
                        $"still intersects {node.Label} rect ({rect.X:F0},{rect.Y:F0},{rect.Width:F0},{rect.Height:F0})");
                }
            }
        }
#endif

        return route;
    }

    // =====================================================================
    // ComputeDetour — вычисление обхода вокруг rect
    // =====================================================================

    /// <summary>
    /// Вычисляет ортогональный обход от a к b вокруг rect.
    /// Возвращает список точек от a до b (включительно), не пересекающих rect.
    ///
    /// Алгоритм:
    /// 1. Определяем тип сегмента (горизонтальный/вертикальный/диагональный)
    /// 2. Выбираем сторону обхода (сверху/снизу/слева/справа)
    /// 3. Строим маршрут через промежуточные точки снаружи rect
    /// 4. Если промежуточная точка внутри rect — переключаем сторону
    /// </summary>
    private static List<Vector2> ComputeDetour(Vector2 a, Vector2 b, RectF rect)
    {
        var rLeft = rect.X;
        var rRight = rect.X + rect.Width;
        var rTop = rect.Y;
        var rBottom = rect.Y + rect.Height;

        var margin = 20f;

        var rCenterX = (rLeft + rRight) / 2;
        var rCenterY = (rTop + rBottom) / 2;

        // Точки по периметру rect с отступом margin
        var topY = rTop - margin;
        var bottomY = rBottom + margin;
        var leftX = rLeft - margin;
        var rightX = rRight + margin;

        List<Vector2> result;

        if (Math.Abs(a.Y - b.Y) < AxisTolerance)
        {
            // Горизонтальный сегмент — обход сверху или снизу
            var goAbove = a.Y <= rCenterY;
            var dy = goAbove ? topY : bottomY;
            result = new List<Vector2> { a, new(a.X, dy), new(b.X, dy), b };
        }
        else if (Math.Abs(a.X - b.X) < AxisTolerance)
        {
            // Вертикальный сегмент — обход слева или справа
            var goLeft = a.X >= rCenterX;
            var dx = goLeft ? leftX : rightX;
            result = new List<Vector2> { a, new(dx, a.Y), new(dx, b.Y), b };
        }
        else
        {
            // Диагональный сегмент — L-образный обход
            var goAbove = a.Y <= rCenterY;
            var goLeft = a.X >= rCenterX;
            var dy = goAbove ? topY : bottomY;
            var dx = goLeft ? leftX : rightX;
            result = new List<Vector2>
            {
                a, new(a.X, dy), new(dx, dy), new(dx, b.Y), b
            };
        }

        // Проверяем: промежуточные точки не внутри rect
        for (var i = 1; i < result.Count - 1; i++)
        {
            var pt = result[i];
            if (pt.X > rLeft && pt.X < rRight && pt.Y > rTop && pt.Y < rBottom)
            {
                // Точка внутри rect — переключаем сторону обхода
                if (Math.Abs(a.Y - b.Y) < AxisTolerance)
                {
                    var dy = a.Y <= rCenterY ? bottomY : topY;
                    result = new List<Vector2> { a, new(a.X, dy), new(b.X, dy), b };
                }
                else if (Math.Abs(a.X - b.X) < AxisTolerance)
                {
                    var dx = a.X >= rCenterX ? rightX : leftX;
                    result = new List<Vector2> { a, new(dx, a.Y), new(dx, b.Y), b };
                }
            }
        }

#if DEBUG
        // Проверка: ни один сегмент обхода не пересекает rect препятствия
        for (var i = 0; i < result.Count - 1; i++)
        {
            if (SegmentIntersectsRect(result[i], result[i + 1], rect))
            {
                throw new InvalidOperationException(
                    $"[DEBUG] Detour segment ({result[i].X:F0},{result[i].Y:F0})→" +
                    $"({result[i + 1].X:F0},{result[i + 1].Y:F0}) intersects obstacle rect");
            }
        }
#endif

        return result;
    }

    // =====================================================================
    // MergeCollinear — объединение коллинеарных сегментов
    // =====================================================================

    /// <summary>
    /// Удаляет промежуточные точки, если три последовательные точки
    /// коллинеарны и движутся в одном направлении (нет поворота).
    /// </summary>
    private static void MergeCollinear(List<Vector2> route)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var k = route.Count - 2; k >= 1; k--)
            {
                var prev = route[k - 1];
                var curr = route[k];
                var next = route[k + 1];

                var sameH = Math.Abs(prev.Y - curr.Y) < AxisTolerance
                         && Math.Abs(curr.Y - next.Y) < AxisTolerance;
                var sameV = Math.Abs(prev.X - curr.X) < AxisTolerance
                         && Math.Abs(curr.X - next.X) < AxisTolerance;

                if (sameH)
                {
                    var dirPrev = Math.Sign(curr.X - prev.X);
                    var dirNext = Math.Sign(next.X - curr.X);
                    if (dirPrev == dirNext && dirPrev != 0)
                    {
                        route.RemoveAt(k);
                        changed = true;
                    }
                }
                else if (sameV)
                {
                    var dirPrev = Math.Sign(curr.Y - prev.Y);
                    var dirNext = Math.Sign(next.Y - curr.Y);
                    if (dirPrev == dirNext && dirPrev != 0)
                    {
                        route.RemoveAt(k);
                        changed = true;
                    }
                }
            }
        }
    }

    // =====================================================================
    // TryShiftSharedPoint / ShiftSharedPoints — сдвиг общей точки
    // =====================================================================

    /// <summary>
    /// Проверяет два отрезка с общей точкой: (a → shared) и (shared → b).
    /// Если оба отрезка коллинеарны и движутся в одну сторону — возвращает true.
    /// Не мутирует точки, только определяет возможность сдвига.
    /// </summary>
    public static bool TryShiftSharedPoint(Vector2 a, Vector2 shared, Vector2 b, float offset)
    {
        // Горизонтально: оба отрезка на одной Y-линии
        if (Math.Abs(a.Y - shared.Y) < AxisTolerance && Math.Abs(shared.Y - b.Y) < AxisTolerance)
        {
            var dir1 = Math.Sign(shared.X - a.X);
            var dir2 = Math.Sign(b.X - shared.X);
            if (dir1 != 0 && dir1 == dir2)
                return true;
        }
        // Вертикально: оба отрезка на одной X-линии
        else if (Math.Abs(a.X - shared.X) < AxisTolerance && Math.Abs(shared.X - b.X) < AxisTolerance)
        {
            var dir1 = Math.Sign(shared.Y - a.Y);
            var dir2 = Math.Sign(b.Y - shared.Y);
            if (dir1 != 0 && dir1 == dir2)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Сдвигает общую точку двух коллинеарных сегментов в направлении движения.
    /// Используется для визуального разделения параллельных дуг.
    /// </summary>
    public static void ShiftSharedPoints(List<Vector2> route, float offset = 20f)
    {
        for (var k = 1; k < route.Count - 1; k++)
        {
            var a = route[k - 1];
            var shared = route[k];
            var b = route[k + 1];

            if (!TryShiftSharedPoint(a, shared, b, offset)) continue;

            // Горизонтально — сдвигаем по X
            if (Math.Abs(a.Y - shared.Y) < AxisTolerance)
            {
                var dir = Math.Sign(shared.X - a.X);
                route[k] = new Vector2(shared.X + dir * offset, shared.Y);
            }
            // Вертикально — сдвигаем по Y
            else
            {
                var dir = Math.Sign(shared.Y - a.Y);
                route[k] = new Vector2(shared.X, shared.Y + dir * offset);
            }
        }
    }

    // =====================================================================
    // PushOutFromNodes — выталкивание точек из rect нод
    // =====================================================================

    /// <summary>
    /// Проверяет каждую промежуточную точку маршрута.
    /// Если точка внутри rect какой-либо ноды — выталкивает её
    /// к ближайшему краю rect с отступом PushOutDistance.
    /// </summary>
    public static void PushOutFromNodes(List<Vector2> route, List<PhysicsNode> nodes)
    {
        for (var k = 1; k < route.Count - 1; k++)
        {
            var pt = route[k];

            // Определяем направление сегмента (vert/horiz) по соседним точкам
            var isVertical = k > 0 && k < route.Count - 1
                && Math.Abs(route[k - 1].X - pt.X) < AxisTolerance
                && Math.Abs(route[k + 1].X - pt.X) < AxisTolerance;

            foreach (var node in nodes)
            {
                var rect = new RectF(
                    node.Position.X - node.Width / 2,
                    node.Position.Y - node.Height / 2,
                    node.Width,
                    node.Height);

                if (!rect.Contains(pt)) continue;

                if (isVertical)
                {
                    // Вертикальный сегмент — выталкиваем только по X
                    var dx = pt.X - node.Position.X;
                    pt = new Vector2(dx > 0 ? rect.X + rect.Width + PushOutDistance : rect.X - PushOutDistance, pt.Y);
                }
                else
                {
                    // Горизонтальный/любой — выталкиваем по ближайшему краю
                    var dx = pt.X - node.Position.X;
                    var dy = pt.Y - node.Position.Y;
                    if (Math.Abs(dx) / rect.Width > Math.Abs(dy) / rect.Height)
                        pt = new Vector2(dx > 0 ? rect.X + rect.Width + PushOutDistance : rect.X - PushOutDistance, pt.Y);
                    else
                        pt = new Vector2(pt.X, dy > 0 ? rect.Y + rect.Height + PushOutDistance : rect.Y - PushOutDistance);
                }

                route[k] = pt;
            }
        }
    }

    // =====================================================================
    // SegmentIntersectsRect — проверка пересечения отрезка с rect
    // =====================================================================

    /// <summary>
    /// Проверяет, что отрезок a→b имеет ненулевое пересечение с внутренностью rect.
    ///
    /// Граничные случаи (касание, совпадение с границей) = false.
    /// Если начальная точка внутри rect — это выход изнутри, не пересечение = false.
    ///
    /// Алгоритм:
    /// 1. Горизонтальный: проверка строгого попадания Y внутрь rect и перекрытия X
    /// 2. Вертикальный: аналогично
    /// 3. Диагональный: проверка пересечения с четырьмя рёбрами rect (Cross product)
    /// </summary>
    public static bool SegmentIntersectsRect(Vector2 a, Vector2 b, RectF rect)
    {
        var minX = Math.Min(rect.X, rect.X + rect.Width);
        var maxX = Math.Max(rect.X, rect.X + rect.Width);
        var minY = Math.Min(rect.Y, rect.Y + rect.Height);
        var maxY = Math.Max(rect.Y, rect.Y + rect.Height);

        // Горизонтальный отрезок
        if (Math.Abs(a.Y - b.Y) < AxisTolerance)
        {
            var cy = a.Y;
            var cxMin = Math.Min(a.X, b.X);
            var cxMax = Math.Max(a.X, b.X);
            if (cy > minY && cy < maxY && cxMin < maxX && cxMax > minX)
                return true;
        }

        // Вертикальный отрезок
        if (Math.Abs(a.X - b.X) < AxisTolerance)
        {
            var cx = a.X;
            var cyMin = Math.Min(a.Y, b.Y);
            var cyMax = Math.Max(a.Y, b.Y);
            if (cx > minX && cx < maxX && cyMin < maxY && cyMax > minY)
                return true;
        }

        // Диагональный отрезок — пересечение с рёбрами rect
        var corners = new[]
        {
            new Vector2(minX, minY),
            new Vector2(maxX, minY),
            new Vector2(maxX, maxY),
            new Vector2(minX, maxY)
        };

        for (var i = 0; i < 4; i++)
        {
            if (SegmentsIntersectStrict(a, b, corners[i], corners[(i + 1) % 4]))
                return true;
        }
        return false;
    }

    // =====================================================================
    // Вспомогательные методы геометрии
    // =====================================================================

    /// <summary>
    /// Строгое пересечение двух отрезков через Cross product.
    /// Касание на конце/ребре = false (все d1..d4 должны быть ненулевыми).
    /// </summary>
    public static bool SegmentsIntersectStrict(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var d1 = Cross(d - c, a - c);
        var d2 = Cross(d - c, b - c);
        var d3 = Cross(b - a, c - a);
        var d4 = Cross(b - a, d - a);

        if (d1 == 0 || d2 == 0 || d3 == 0 || d4 == 0) return false;

        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    /// <summary>Векторное произведение 2D (Cross product).</summary>
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}


