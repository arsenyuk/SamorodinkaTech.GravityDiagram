using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Demo;

public static class SampleDiagram
{
    public static Diagram CreateThreeNode()
    {
        var diagram = new Diagram();

        var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "1", Position = new Vector2(260, 280), Width = 170, Height = 80 });
        var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "2", Position = new Vector2(560, 220), Width = 170, Height = 80 });
        var n3 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "3", Position = new Vector2(560, 360), Width = 170, Height = 80 });

        foreach (var n in new[] { n1, n2, n3 })
        {
            n.SetSideFlow(RectSide.Top, PortFlow.Incoming);
            n.SetSideFlow(RectSide.Bottom, PortFlow.Outgoing);
            n.SetSideFlow(RectSide.Left, PortFlow.Both);
            n.SetSideFlow(RectSide.Right, PortFlow.Both);
        }

        var p1Out = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
        var p2In = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });
        var p3In = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n3.Id, RectSide.Left, 0.5f) });

        diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "1→2", FromPortId = p1Out.Id, ToPortId = p2In.Id });
        diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "1→3", FromPortId = p1Out.Id, ToPortId = p3In.Id });

        return diagram;
    }

    public static Diagram CreateTwoNode()
    {
        var diagram = new Diagram();

        var n1 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "A", Position = new Vector2(260, 280), Width = 170, Height = 80 });
        var n2 = diagram.AddNode(new RectNode { Id = DiagramId.New(), Text = "B", Position = new Vector2(560, 280), Width = 170, Height = 80 });

        foreach (var n in new[] { n1, n2 })
        {
            n.SetSideFlow(RectSide.Top, PortFlow.Incoming);
            n.SetSideFlow(RectSide.Bottom, PortFlow.Outgoing);
            n.SetSideFlow(RectSide.Left, PortFlow.Both);
            n.SetSideFlow(RectSide.Right, PortFlow.Both);
        }

        var p1Out = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "out", Ref = new PortRef(n1.Id, RectSide.Right, 0.5f) });
        var p2In = diagram.AddPort(new Port { Id = DiagramId.New(), Text = "in", Ref = new PortRef(n2.Id, RectSide.Left, 0.5f) });

        diagram.AddArc(new Arc { Id = DiagramId.New(), Text = "A→B", FromPortId = p1Out.Id, ToPortId = p2In.Id });

        return diagram;
    }
}
