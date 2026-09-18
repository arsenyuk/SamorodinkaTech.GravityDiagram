using System;
using System.Collections.Generic;
using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

public sealed class PhysicsModel
{
    public readonly List<PhysicsNode> Nodes = [];
    public readonly List<Edge> Edges = [];
    public readonly List<Arc> Arcs = [];

    // Sample graph layout constants
    private const float BigNodeWidth = 320f;
    private const float BigNodeHeight = 160f;
    private const float BigNodeOffset = 350f;
    private const float SmallNodeOffset = 200f;

    public float RepulsionP = 10f;
    public float AttractionK = 0.001f;
    public float FrictionK = 0.99f;
    public bool UseJitter = true;

    private const float MaxSpeed = 2000f;
    private const float MinDistance = 0.001f;
    private static readonly Random Rng = new();

    private bool AreConnected(int i, int j)
    {
        foreach (var e in Edges)
        {
            var ei = Nodes.IndexOf(e.From.Node);
            var ej = Nodes.IndexOf(e.To.Node);
            if ((ei == i && ej == j) || (ei == j && ej == i))
                return true;
        }
        return false;
    }

    private float NodeRadius(int i)
    {
        var n = Nodes[i];
        return MathF.Sqrt(n.Width * n.Width + n.Height * n.Height) / 2;
    }

    private float Zone3Radius(int nodeIdx)
    {
        var maxZ2 = 0f;
        foreach (var e in Edges)
        {
            var ei = Nodes.IndexOf(e.From.Node);
            var ej = Nodes.IndexOf(e.To.Node);
            var neighbor = -1;
            if (ei == nodeIdx) neighbor = ej;
            else if (ej == nodeIdx) neighbor = ei;
            if (neighbor >= 0)
            {
                var z2 = 2 * NodeRadius(neighbor);
                if (z2 > maxZ2) maxZ2 = z2;
            }
        }
        return maxZ2;
    }

    public void Step(float dt)
    {
        var forces = new Vector2[Nodes.Count];

        var virtualPositions = new Vector2[Nodes.Count];
        for (var i = 0; i < Nodes.Count; i++)
        {
            virtualPositions[i] = Nodes[i].Position;
            if (UseJitter)
            {
                virtualPositions[i] += new Vector2(
                    Rng.Next(-1, 2),
                    Rng.Next(-1, 2));
            }
        }

        for (var i = 0; i < Nodes.Count; i++)
        {
            for (var j = i + 1; j < Nodes.Count; j++)
            {
                var delta = virtualPositions[j] - virtualPositions[i];
                var distance = delta.Length();

                if (distance < MinDistance) continue;

                var direction = delta / distance;
                var attraction = direction * (AttractionK * distance);

                var connected = AreConnected(i, j);
                var rI = NodeRadius(i);
                var rJ = NodeRadius(j);
                var zoneL = connected ? 2 * Math.Min(rI, rJ) + 0.02f : Math.Max(Zone3Radius(i), Zone3Radius(j));
                var activeZone = 2f * zoneL;

                Vector2 repulsion = Vector2.Zero;
                if (distance <= activeZone)
                    repulsion = -direction * RepulsionP;

                var pairForce = attraction + repulsion;
                forces[i] += pairForce;
                forces[j] -= pairForce;
            }
        }

        for (var i = 0; i < Nodes.Count; i++)
        {
            var friction = -FrictionK * Nodes[i].Velocity;
            var totalForce = forces[i] + friction;
            Nodes[i].Velocity += totalForce * dt;

            var speed = Nodes[i].Velocity.Length();
            if (speed > MaxSpeed)
                Nodes[i].Velocity = Nodes[i].Velocity / speed * MaxSpeed;

            Nodes[i].Position += Nodes[i].Velocity * dt;
        }
    }

    public void ResetVelocities()
    {
        for (var i = 0; i < Nodes.Count; i++)
            Nodes[i].Velocity = Vector2.Zero;
    }

    private static Port MakePort(string id, PhysicsNode node, float offsetX, float offsetY)
        => new(id, node, offsetX, offsetY);

    public static void CreateGraphABC(PhysicsModel model, float cx, float cy)
    {
        model.Nodes.Clear();
        model.Edges.Clear();
        model.Arcs.Clear();

        var a = new PhysicsNode("A", cx - BigNodeOffset, cy) { Width = BigNodeWidth, Height = BigNodeHeight };
        var b = new PhysicsNode("B", cx, cy);
        var c = new PhysicsNode("C", cx + BigNodeOffset, cy);

        model.Nodes.Add(a);
        model.Nodes.Add(b);
        model.Nodes.Add(c);

        var pA_right = MakePort("A_right", a, a.Width / 2, 0);
        var pB_left = MakePort("B_left", b, -b.Width / 2, 0);
        var pB_right = MakePort("B_right", b, b.Width / 2, 0);
        var pC_left = MakePort("C_left", c, -c.Width / 2, 0);

        model.Edges.Add(new Edge(pA_right, pB_left));
        model.Edges.Add(new Edge(pB_right, pC_left));
    }

    public static void CreateGraphABCSmall(PhysicsModel model, float cx, float cy)
    {
        model.Nodes.Clear();
        model.Edges.Clear();
        model.Arcs.Clear();

        var a = new PhysicsNode("A", cx - SmallNodeOffset, cy);
        var b = new PhysicsNode("B", cx, cy) { Width = BigNodeWidth, Height = BigNodeHeight };
        var c = new PhysicsNode("C", cx + SmallNodeOffset, cy);

        model.Nodes.Add(a);
        model.Nodes.Add(b);
        model.Nodes.Add(c);

        var pA_right = MakePort("A_right", a, a.Width / 2, 0);
        var pB_left = MakePort("B_left", b, -b.Width / 2, 0);
        var pB_right = MakePort("B_right", b, b.Width / 2, 0);
        var pC_left = MakePort("C_left", c, -c.Width / 2, 0);

        model.Edges.Add(new Edge(pA_right, pB_left));
        model.Edges.Add(new Edge(pB_right, pC_left));
    }
}
