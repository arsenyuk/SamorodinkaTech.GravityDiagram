using System;
using System.Collections.Generic;
using System.Numerics;
using SamorodinkaTech.GravityDiagram.NewModel;
using Xunit;

namespace SamorodinkaTech.GravityDiagram.NewModel.Tests;

/// <summary>
/// Тесты ModelView.AdjustArcs: подстройкаpersistent waypoints после перемещения нод.
/// </summary>
public sealed class AdjustArcsTests
{
    // =====================================================================
    // После AdjustArcs первая точка = позиция порта источника,
    // последняя точка = позиция порта цели.
    // =====================================================================

    [Fact]
    public void AdjustArcs_FirstPointMatchesSourcePort()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("out", a, a.Width / 2, 0);
        var portB = new Port("in", b, -b.Width / 2, 0);

        var (model, view) = CreateModelView(new[] { a, b }, new[] { portA, portB });

        var arc = new Arc(new Edge(portA, portB));
        arc.Points.AddRange(new[]
        {
            new Vector2(40, 0),
            new Vector2(40, -60),
            new Vector2(160, -60),
            new Vector2(160, 0),
        });
        model.Arcs.Add(arc);

        view.AdjustArcs();

        Assert.Equal(portA.GetWorldPosition(), arc.Points[0]);
    }

    [Fact]
    public void AdjustArcs_LastPointMatchesTargetPort()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("out", a, a.Width / 2, 0);
        var portB = new Port("in", b, -b.Width / 2, 0);

        var (model, view) = CreateModelView(new[] { a, b }, new[] { portA, portB });

        var arc = new Arc(new Edge(portA, portB));
        arc.Points.AddRange(new[]
        {
            new Vector2(40, 0),
            new Vector2(40, -60),
            new Vector2(160, -60),
            new Vector2(160, 0),
        });
        model.Arcs.Add(arc);

        view.AdjustArcs();

        Assert.Equal(portB.GetWorldPosition(), arc.Points[^1]);
    }

    // =====================================================================
    // После перемещения source node дуга подстраивается:
    // первая точка = новый порт.
    // =====================================================================

    [Fact]
    public void AdjustArcs_FirstPointFollowsSourceAfterMove()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("out", a, a.Width / 2, 0);
        var portB = new Port("in", b, -b.Width / 2, 0);

        var (model, view) = CreateModelView(new[] { a, b }, new[] { portA, portB });

        var arc = new Arc(new Edge(portA, portB));
        arc.Points.AddRange(new[]
        {
            new Vector2(40, 0),
            new Vector2(40, -60),
            new Vector2(160, -60),
            new Vector2(160, 0),
        });
        model.Arcs.Add(arc);

        // Сдвигаем source node
        a.Position = new Vector2(50, 30);

        view.AdjustArcs();

        // Первая точка = новый порт source
        Assert.Equal(portA.GetWorldPosition(), arc.Points[0]);
    }

    // =====================================================================
    // После перемещения target node дуга подстраивается:
    // последняя точка = новый порт.
    // =====================================================================

    [Fact]
    public void AdjustArcs_LastPointFollowsTargetAfterMove()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("out", a, a.Width / 2, 0);
        var portB = new Port("in", b, -b.Width / 2, 0);

        var (model, view) = CreateModelView(new[] { a, b }, new[] { portA, portB });

        var arc = new Arc(new Edge(portA, portB));
        arc.Points.AddRange(new[]
        {
            new Vector2(40, 0),
            new Vector2(40, -60),
            new Vector2(160, -60),
            new Vector2(160, 0),
        });
        model.Arcs.Add(arc);

        // Сдвигаем target node
        b.Position = new Vector2(300, 50);

        view.AdjustArcs();

        // Последняя точка = новый порт target
        Assert.Equal(portB.GetWorldPosition(), arc.Points[^1]);
    }

    // =====================================================================
    // Сегменты нулевой длины удаляются.
    // =====================================================================

    [Fact]
    public void AdjustArcs_RemovesZeroLengthSegments()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("out", a, a.Width / 2, 0);
        var portB = new Port("in", b, -b.Width / 2, 0);

        var (model, view) = CreateModelView(new[] { a, b }, new[] { portA, portB });

        var arc = new Arc(new Edge(portA, portB));
        arc.Points.AddRange(new[]
        {
            new Vector2(40, 0),
            new Vector2(40, 0),  // нулевой сегмент
            new Vector2(160, -60),
            new Vector2(160, 0),
        });
        model.Arcs.Add(arc);

        view.AdjustArcs();

        // Нулевой сегмент удалён
        Assert.Equal(3, arc.Points.Count);
    }

    // =====================================================================
    // После перемещения обоих нод — первая и последняя точки подстраиваются.
    // =====================================================================

    [Fact]
    public void AdjustArcs_BothPortsFollowAfterBothMove()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("out", a, a.Width / 2, 0);
        var portB = new Port("in", b, -b.Width / 2, 0);

        var (model, view) = CreateModelView(new[] { a, b }, new[] { portA, portB });

        var arc = new Arc(new Edge(portA, portB));
        arc.Points.AddRange(new[]
        {
            new Vector2(40, 0),
            new Vector2(40, -60),
            new Vector2(160, -60),
            new Vector2(160, 0),
        });
        model.Arcs.Add(arc);

        // Сдвигаем обе ноды
        a.Position = new Vector2(50, 30);
        b.Position = new Vector2(300, 50);

        view.AdjustArcs();

        Assert.Equal(portA.GetWorldPosition(), arc.Points[0]);
        Assert.Equal(portB.GetWorldPosition(), arc.Points[^1]);
    }

    // =====================================================================
    // Все сегменты остаются ортогональными после подстройки.
    // =====================================================================

    [Fact]
    public void AdjustArcs_FirstSegmentRemainsAxisAligned()
    {
        var a = new PhysicsNode("A", 0, 0) { Width = 80, Height = 80 };
        var b = new PhysicsNode("B", 200, 0) { Width = 80, Height = 80 };

        var portA = new Port("out", a, a.Width / 2, 0);
        var portB = new Port("in", b, -b.Width / 2, 0);

        var (model, view) = CreateModelView(new[] { a, b }, new[] { portA, portB });

        var arc = new Arc(new Edge(portA, portB));
        arc.Points.AddRange(new[]
        {
            new Vector2(40, 0),
            new Vector2(40, -60),
            new Vector2(160, -60),
            new Vector2(160, 0),
        });
        model.Arcs.Add(arc);

        // Сдвигаем source
        a.Position = new Vector2(50, 30);

        view.AdjustArcs();

        // Первый сегмент ортогонален (сдвигается вместе с source)
        var horiz0 = Math.Abs(arc.Points[0].Y - arc.Points[1].Y) < OrthogonalRouter.AxisTolerance;
        var vert0 = Math.Abs(arc.Points[0].X - arc.Points[1].X) < OrthogonalRouter.AxisTolerance;
        Assert.True(horiz0 || vert0,
            $"First segment not axis-aligned: ({arc.Points[0].X:F1},{arc.Points[0].Y:F1})→({arc.Points[1].X:F1},{arc.Points[1].Y:F1})");
    }

    // --- Helper ---

    private static (PhysicsModel model, ModelView view) CreateModelView(
        PhysicsNode[] nodes, Port[] ports)
    {
        var model = new PhysicsModel();
        foreach (var n in nodes) model.Nodes.Add(n);

        for (var i = 0; i < ports.Length - 1; i += 2)
            model.Edges.Add(new Edge(ports[i], ports[i + 1]));

        var view = new ModelView();
        // Подключаем модель через reflection (Model свойство readonly)
        typeof(ModelView).GetProperty("Model")!.SetValue(view, model);

        return (model, view);
    }
}
