using System.Numerics;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Core.Tests;

public sealed class DiagramCoreTests
{
    [Fact]
    public void RectF_FromCenter_RoundTripsCenter()
    {
        var center = new Vector2(10, 20);
        var r = RectF.FromCenter(center, width: 100, height: 40);

        Assert.Equal(center.X, r.Center.X, 3);
        Assert.Equal(center.Y, r.Center.Y, 3);
    }

    [Fact]
    public void RectNode_BoundsCenter_EqualsPosition()
    {
        var n = new RectNode
        {
            Id = new DiagramId("n"),
            Text = "N",
            Position = new Vector2(-7, 13),
            Width = 160,
            Height = 80,
        };

        Assert.Equal(n.Position.X, n.Bounds.Center.X, 3);
        Assert.Equal(n.Position.Y, n.Bounds.Center.Y, 3);
    }

    [Fact]
    public void AddPort_AutoDistributesOffsets_Proportionally()
    {
        var d = new Diagram { AutoDistributePorts = false };

        var nodeId = new DiagramId("n");
        d.AddNode(new RectNode { Id = nodeId, Text = "N", Position = Vector2.Zero });

        var p1 = new Port { Id = new DiagramId("p1"), Text = "p1", Ref = new PortRef(nodeId, RectSide.Top, 0f) };
        var p2 = new Port { Id = new DiagramId("p2"), Text = "p2", Ref = new PortRef(nodeId, RectSide.Top, 0f) };
        var p3 = new Port { Id = new DiagramId("p3"), Text = "p3", Ref = new PortRef(nodeId, RectSide.Top, 0f) };
        d.AddPort(p1);
        d.AddPort(p2);
        d.AddPort(p3);
        d.DistributeAllPortsProportionally();

        Assert.Equal(0.25f, p1.Ref.Offset, 3);
        Assert.Equal(0.50f, p2.Ref.Offset, 3);
        Assert.Equal(0.75f, p3.Ref.Offset, 3);
    }

    [Fact]
    public void DistributeAllPortsProportionally_CentersSinglePort()
    {
        var d = new Diagram { AutoDistributePorts = false };

        var nodeId = new DiagramId("n");
        d.AddNode(new RectNode { Id = nodeId, Text = "N", Position = Vector2.Zero });

        var p = new Port { Id = new DiagramId("p"), Text = "p", Ref = new PortRef(nodeId, RectSide.Left, 0f) };
        d.AddPort(p);

        d.DistributeAllPortsProportionally();
        Assert.Equal(0.5f, p.Ref.Offset, 3);
    }

    [Fact]
    public void AddArc_Throws_WhenOutgoingForbiddenOnFromSide()
    {
        var d = new Diagram();
        var aId = new DiagramId("a");
        var bId = new DiagramId("b");

        var a = d.AddNode(new RectNode { Id = aId, Text = "A", Position = Vector2.Zero });
        var b = d.AddNode(new RectNode { Id = bId, Text = "B", Position = new Vector2(200, 0) });

        a.SetSideFlow(RectSide.Right, PortFlow.Incoming); // outgoing forbidden
        b.SetSideFlow(RectSide.Left, PortFlow.Incoming);

        var ap = d.AddPort(new Port { Id = new DiagramId("ap"), Text = "ap", Ref = new PortRef(aId, RectSide.Right, 0.5f) });
        var bp = d.AddPort(new Port { Id = new DiagramId("bp"), Text = "bp", Ref = new PortRef(bId, RectSide.Left, 0.5f) });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            d.AddArc(new Arc { Id = new DiagramId("e"), FromPortId = ap.Id, ToPortId = bp.Id, Text = "" }));

        Assert.Contains("forbids outgoing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetPortWorldPosition_ClampsOffsetToSideEndpoints()
    {
        var n = new RectNode
        {
            Id = new DiagramId("n"),
            Text = "N",
            Position = new Vector2(10, 20),
            Width = 100,
            Height = 40,
        };

        var left = n.Position.X - n.Width / 2f;
        var right = n.Position.X + n.Width / 2f;
        var top = n.Position.Y - n.Height / 2f;
        var bottom = n.Position.Y + n.Height / 2f;

        var pTopOver = GravityLayoutEngine.GetPortWorldPosition(n, new PortRef(n.Id, RectSide.Top, 2f));
        Assert.Equal(right, pTopOver.X, 3);
        Assert.Equal(top, pTopOver.Y, 3);

        var pLeftUnder = GravityLayoutEngine.GetPortWorldPosition(n, new PortRef(n.Id, RectSide.Left, -1f));
        Assert.Equal(left, pLeftUnder.X, 3);
        Assert.Equal(top, pLeftUnder.Y, 3);

        var pRightMid = GravityLayoutEngine.GetPortWorldPosition(n, new PortRef(n.Id, RectSide.Right, 0.5f));
        Assert.Equal(right, pRightMid.X, 3);
        Assert.Equal((top + bottom) / 2f, pRightMid.Y, 3);
    }

    [Fact]
    public void Step_WithHardMinSpacing_MakesExpandedBoundsNonIntersecting()
    {
        var settings = new LayoutSettings
        {
            BackgroundPairGravity = 0f,
            ConnectedArcAttractionK = 0f,
            OverlapRepulsionK = 0f,
            Drag = 0f,
            MaxSpeed = 1_000_000f,
            MinNodeSpacing = 40f,
            UseHardMinSpacing = true,
            HardMinSpacingIterations = 20,
            HardMinSpacingSlop = 0f,
        };

        var d = new Diagram();
        d.AddNode(new RectNode { Id = new DiagramId("a"), Text = "A", Position = new Vector2(0, 0), Width = 100, Height = 60 });
        d.AddNode(new RectNode { Id = new DiagramId("b"), Text = "B", Position = new Vector2(0, 0), Width = 100, Height = 60 });

        var engine = new GravityLayoutEngine(settings);
        engine.Step(d, 1f / 60f);

        var nodes = d.Nodes;
        var margin = settings.MinNodeSpacing * 0.5f;
        for (var i = 0; i < nodes.Count; i++)
        {
            var ra = Expand(nodes[i].Bounds, margin);
            for (var j = i + 1; j < nodes.Count; j++)
            {
                var rb = Expand(nodes[j].Bounds, margin);
                Assert.False(ra.Intersects(rb));
            }
        }
    }

    private static RectF Expand(in RectF rect, float margin)
        => new(rect.X - margin, rect.Y - margin, rect.Width + 2 * margin, rect.Height + 2 * margin);
}
