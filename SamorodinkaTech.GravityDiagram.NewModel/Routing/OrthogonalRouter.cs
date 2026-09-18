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
        var sourceDetourApplied = false;
        var sourceDetourEnd = -1; // индекс последней точки source detour
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
                    // Входящую ноду проверяем только для предпоследнего сегмента.
                    if (ni == toIdx && k < route.Count - 2) continue;
                    // Сегменты source detour (от a до sourceDetourEnd) пропускаем —
                    // они гарантированно вне rect исходной ноды.
                    if (sourceDetourApplied && k <= sourceDetourEnd) continue;

                    var rect = new RectF(
                        node.Position.X - node.Width / 2,
                        node.Position.Y - node.Height / 2,
                        node.Width,
                        node.Height);

                    // Если rect содержит target node — пропускаем (target внутри obstacle)
                    if (k == route.Count - 2 && nodes[toIdx] != null)
                    {
                        var targetPos = nodes[toIdx].Position;
                        if (targetPos.X >= rect.X && targetPos.X <= rect.X + rect.Width &&
                            targetPos.Y >= rect.Y && targetPos.Y <= rect.Y + rect.Height)
                            continue;
                    }

                    if (SegmentIntersectsRect(a, b, rect))
                    {
                        // Для исходной ноды: сегмент должен строго входить внутрь rect
                        // (середина сегмента внутри rect), а не просто касаться границы.
                        if (ni == fromIdx)
                        {
                            // Проверяем что сегмент строго ВХОДИТ в rect исходной ноды
                            // (не просто касается границы). Используем 4 точки вдоль сегмента.
                            var anyInside = false;
                            for (var t = 0.1f; t <= 0.9f; t += 0.2f)
                            {
                                var p = a + (b - a) * t;
                                if (p.X > rect.X && p.X < rect.X + rect.Width &&
                                    p.Y > rect.Y && p.Y < rect.Y + rect.Height)
                                {
                                    anyInside = true;
                                    break;
                                }
                            }
                            if (!anyInside) continue;

                            // Обход исходной ноды: определяем направление выхода по порту,
                            // и выбираем маршрут: если цель в противоположном направлении —
                            // идём вдоль стороны узла (вверх/вниз), чтобы не создавать петлю.
                            var margin = Math.Max(rect.Width, rect.Height) / 2 + PushOutDistance + 1f;

                            // Определяем: горизонтальный порт (Left/Right) или вертикальный (Top/Bottom)?
                            var isHorizontalPort = a.X <= rect.X + 0.01f || a.X >= rect.X + rect.Width - 0.01f
                                || Math.Abs(a.X - (rect.X + rect.Width / 2)) < 0.01f;

                            List<Vector2> points;
                            if (isHorizontalPort)
                            {
                                // Горизонтальный порт — выходим вверх/вниз (перпендикулярно),
                                // затем к ЦЕЛИ (p2). turnY ДОЛЖЕН быть за пределами rect по Y.
                                var detourMargin = 20f;
                                // Всегда выходим за пределы rect по Y
                                var turnY = a.Y <= (rect.Y + rect.Height) / 2
                                    ? rect.Y - detourMargin
                                    : rect.Y + rect.Height + detourMargin;
                                // turnX — за пределами rect по X
                                var turnX = p2.X < rect.X + rect.Width / 2
                                    ? rect.X - detourMargin
                                    : rect.X + rect.Width + detourMargin;
                                // Если turnX на границе rect — выходим за пределы
                                if (turnX > rect.X - detourMargin && turnX < rect.X + rect.Width + detourMargin)
                                {
                                    var distLeft = MathF.Abs(p2.X - (rect.X - detourMargin));
                                    var distRight = MathF.Abs(p2.X - (rect.X + rect.Width + detourMargin));
                                    turnX = distLeft > distRight
                                        ? rect.X - detourMargin
                                        : rect.X + rect.Width + detourMargin;
                                }
                                points = new List<Vector2>
                                {
                                    a,
                                    new(a.X, turnY),
                                    new(turnX, turnY),
                                    new(turnX, p2.Y),
                                    p2
                                };
                            }
                            else
                            {
                                // Вертикальный порт — выходим влево/вправо (перпендикулярно),
                                // затем вертикально к ЦЕЛИ (p2). turnY за пределами rect по Y.
                                var detourMargin = 20f;
                                var turnX = p2.X <= rect.X + rect.Width / 2
                                    ? rect.X - detourMargin
                                    : rect.X + rect.Width + detourMargin;
                                // turnY за пределами rect по Y (на той же стороне, что и a)
                                var turnY = a.Y <= (rect.Y + rect.Height) / 2
                                    ? rect.Y - detourMargin
                                    : rect.Y + rect.Height + detourMargin;
                                // Если turnY на границе rect — выходим за пределы
                                if (turnY >= rect.Y - detourMargin && turnY <= rect.Y + rect.Height + detourMargin)
                                {
                                    turnY = a.Y <= (rect.Y + rect.Height) / 2
                                        ? rect.Y - detourMargin
                                        : rect.Y + rect.Height + detourMargin;
                                }
                                points = new List<Vector2>
                                {
                                    a,
                                    new(turnX, a.Y),
                                    new(turnX, turnY),
                                    new(p2.X, turnY),
                                    p2
                                };
                            }

                            var insertCount = points.Count;
                            route.RemoveAt(k);
                            route.InsertRange(k, points);
                            sourceDetourApplied = true;
                            sourceDetourEnd = k + insertCount - 1;
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

        // Сдвигаем общие точки коллинеарных сегментов для визуального разделения
        // (пропускаем если был обход исходной ноды — точки детура не должны сдвигаться)
        if (!sourceDetourApplied)
            ShiftSharedPoints(route, ShiftOffset);

        // Выталкиваем промежуточные точки из rect нод (кроме source и target)
        PushOutFromNodes(route, nodes, fromIdx, toIdx);

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

        // Удаляем петли: если две одинаковые точки рядом — удаляем промежуточные
        RemoveLoops(route);

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

        // Удаление коллинеарных точек — в самом последнем порядке (после snap-to-grid)
        MergeCollinear(route);

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
                    Console.Error.WriteLine(
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
    /// Временное правило: ломаем сегмент строго посередине,
    /// но поворот делаем до входа в rect (на границе rect с отступом),
    /// чтобы промежуточные точки были за пределами rect.
    /// </summary>




    private static List<Vector2> ComputeDetour(Vector2 a, Vector2 b, RectF rect)
    {
        var margin = 20f;

        // Проверяем: b внутри rect?
        var bInsideRect = rect.Contains(b);

        List<Vector2> result;

        if (Math.Abs(a.Y - b.Y) < AxisTolerance)
        {
            // Горизонтальный сегмент — ломаем посередине a→b
            var midX = (a.X + b.X) / 2;
            // Определяем направление обхода: вверх или вниз от rect
            var goUp = a.Y <= rect.Y + rect.Height / 2;
            var dy = goUp ? rect.Y - margin : rect.Y + rect.Height + margin;

            if (bInsideRect)
            {
                // b внутри rect — ломаем посередине, обходим rect целиком
                var approachX = b.X < rect.X + rect.Width / 2
                    ? rect.X - margin
                    : rect.X + rect.Width + margin;
                result = new List<Vector2>
                {
                    a, new(midX, a.Y), new(midX, dy), new(approachX, dy), new(approachX, b.Y), b
                };
            }
            else
            {
                result = new List<Vector2>
                {
                    a, new(midX, a.Y), new(midX, dy), new(midX, b.Y), b
                };
            }
        }
        else if (Math.Abs(a.X - b.X) < AxisTolerance)
        {
            // Вертикальный сегмент — ломаем посередине a→b
            var midY = (a.Y + b.Y) / 2;
            var goLeft = a.X >= rect.X + rect.Width / 2;
            var dx = goLeft ? rect.X - margin : rect.X + rect.Width + margin;

            if (bInsideRect)
            {
                var approachY = b.Y < rect.Y + rect.Height / 2
                    ? rect.Y - margin
                    : rect.Y + rect.Height + margin;
                result = new List<Vector2>
                {
                    a, new(a.X, midY), new(dx, midY), new(dx, approachY), new(b.X, approachY), b
                };
            }
            else
            {
                result = new List<Vector2>
                {
                    a, new(a.X, midY), new(dx, midY), new(b.X, midY), b
                };
            }
        }
        else
        {
            // Диагональный сегмент
            var rLeft = rect.X;
            var rRight = rect.X + rect.Width;
            var rTop = rect.Y;
            var rBottom = rect.Y + rect.Height;
            var breakX = a.X < (rLeft + rRight) / 2 ? rLeft - margin : rRight + margin;
            var breakY = a.Y < (rTop + rBottom) / 2 ? rTop - margin : rBottom + margin;
            result = new List<Vector2>
            {
                a,
                new(a.X, breakY),
                new(breakX, breakY),
                new(breakX, b.Y),
                b
            };
        }

#if DEBUG
        for (var i = 0; i < result.Count - 1; i++)
        {
            if (SegmentIntersectsRect(result[i], result[i + 1], rect))
            {
                Console.Error.WriteLine(
                    $"[DEBUG] Detour segment ({result[i].X:F0},{result[i].Y:F0})→" +
                    $"({result[i + 1].X:F0},{result[i + 1].Y:F0}) intersects obstacle rect");
                Console.Error.WriteLine($"  a=({a.X:F0},{a.Y:F0}) b=({b.X:F0},{b.Y:F0}) rect=({rect.X:F0},{rect.Y:F0},{rect.Width:F0},{rect.Height:F0})");
                Console.Error.WriteLine($"  result=[{string.Join(" → ", result.Select(p => $"({p.X:F0},{p.Y:F0})"))}]");
            }
        }
#endif

        return result;
    }
    /// Удаляет одну коллинеарную точку (первую найденную). Возвращает true если удалил.
    public static bool MergeOneCollinear(List<Vector2> route)
    {
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
                    return true;
                }
            }
            else if (sameV)
            {
                var dirPrev = Math.Sign(curr.Y - prev.Y);
                var dirNext = Math.Sign(next.Y - curr.Y);
                if (dirPrev == dirNext && dirPrev != 0)
                {
                    route.RemoveAt(k);
                    return true;
                }
            }
        }
        return false;
    }

    /// Удаляет петли: если две одинаковые точки стоят рядом — удаляет промежуточные.
    private static void RemoveLoops(List<Vector2> route)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = route.Count - 1; i >= 1; i--)
            {
                for (var j = i - 1; j >= 0; j--)
                {
                    if (Vector2.Distance(route[i], route[j]) < AxisTolerance)
                    {
                        // Удаляем точки между j и i (включая i)
                        route.RemoveRange(j + 1, i - j);
                        changed = true;
                        break;
                    }
                }
                if (changed) break;
            }
        }
    }

    private static void MergeCollinear(List<Vector2> route)
    {
        while (MergeOneCollinear(route)) { }
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
    /// Сдвигает одну общую точку по индексу k. Возвращает true если сдвинул.
    public static bool ShiftSharedPointAt(List<Vector2> route, int k, float offset)
    {
        if (k < 1 || k >= route.Count - 1) return false;
        var a = route[k - 1];
        var shared = route[k];
        var b = route[k + 1];

        if (!TryShiftSharedPoint(a, shared, b, offset)) return false;

        if (Math.Abs(a.Y - shared.Y) < AxisTolerance)
        {
            var dir = Math.Sign(shared.X - a.X);
            route[k] = new Vector2(shared.X + dir * offset, shared.Y);
        }
        else
        {
            var dir = Math.Sign(shared.Y - a.Y);
            route[k] = new Vector2(shared.X, shared.Y + dir * offset);
        }
        return true;
    }


    public static void ShiftSharedPoints(List<Vector2> route, float offset = 20f)
    {
        for (var k = 1; k < route.Count - 1; k++)
            ShiftSharedPointAt(route, k, offset);
    }

    // =====================================================================
    // PushOutFromNodes — выталкивание точек из rect нод
    // =====================================================================

    /// Выталкивает одну точку из rect нод. Возвращает true если вытолкнул.
    public static bool PushOneOutFromNodes(List<Vector2> route, List<PhysicsNode> nodes,
        int skipFromIdx, int skipToIdx)
    {
        for (var k = 1; k < route.Count - 1; k++)
        {
            var pt = route[k];

            var isVertical = k > 0 && k < route.Count - 1
                && Math.Abs(route[k - 1].X - pt.X) < AxisTolerance
                && Math.Abs(route[k + 1].X - pt.X) < AxisTolerance;

            foreach (var node in nodes)
            {
                var ni = nodes.IndexOf(node);
                if (ni == skipFromIdx || ni == skipToIdx) continue;

                var rect = new RectF(
                    node.Position.X - node.Width / 2,
                    node.Position.Y - node.Height / 2,
                    node.Width,
                    node.Height);

                if (!rect.Contains(pt)) continue;

                if (isVertical)
                {
                    var dx = pt.X - node.Position.X;
                    pt = new Vector2(dx > 0 ? rect.X + rect.Width + PushOutDistance : rect.X - PushOutDistance, pt.Y);
                }
                else
                {
                    var dx = pt.X - node.Position.X;
                    var dy = pt.Y - node.Position.Y;
                    if (Math.Abs(dx) / rect.Width > Math.Abs(dy) / rect.Height)
                        pt = new Vector2(dx > 0 ? rect.X + rect.Width + PushOutDistance : rect.X - PushOutDistance, pt.Y);
                    else
                        pt = new Vector2(pt.X, dy > 0 ? rect.Y + rect.Height + PushOutDistance : rect.Y - PushOutDistance);
                }

                route[k] = pt;
                return true;
            }
        }
        return false;
    }

    public static void PushOutFromNodes(List<Vector2> route, List<PhysicsNode> nodes,
        int skipFromIdx = -1, int skipToIdx = -1)
    {
        while (PushOneOutFromNodes(route, nodes, skipFromIdx, skipToIdx)) { }
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


