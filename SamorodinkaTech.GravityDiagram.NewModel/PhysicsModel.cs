using System;
using System.Collections.Generic;
using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.NewModel;

public sealed class PhysicsNode
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Width = 160f;
    public float Height = 80f;
    public string Label = "";

    public Vector2 PortLeft => new(Position.X - Width / 2, Position.Y);
    public Vector2 PortRight => new(Position.X + Width / 2, Position.Y);

    public PhysicsNode(string label, float x, float y)
    {
        Label = label;
        Position = new Vector2(x, y);
    }
}

public sealed class Edge
{
    public int From;
    public int To;

    public Edge(int from, int to) { From = from; To = to; }
}

public sealed class PhysicsModel
{
    public readonly List<PhysicsNode> Nodes = [];
    public readonly List<Edge> Edges = [];

    public float RepulsionS = 500f;
    public float RepulsionP = 10f;
    public float UniversalRepulsionK = 10f;
    public float AttractionK = 0.001f;
    public float FrictionK = 0.99f;
    public bool UseJitter = true;

    private const float MaxSpeed = 2000f;
    private static readonly Random Rng = new();

    private bool AreConnected(int i, int j)
    {
        foreach (var e in Edges)
        {
            if ((e.From == i && e.To == j) || (e.From == j && e.To == i))
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
        // Max Zone 2 (2×r) of all connected neighbors
        var maxZ2 = 0f;
        foreach (var e in Edges)
        {
            var neighbor = -1;
            if (e.From == nodeIdx) neighbor = e.To;
            else if (e.To == nodeIdx) neighbor = e.From;
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

        // Step 1: Compute jittered (virtual) positions
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

        // Step 2: Pairwise forces
        for (var i = 0; i < Nodes.Count; i++)
        {
            for (var j = i + 1; j < Nodes.Count; j++)
            {
                var delta = virtualPositions[j] - virtualPositions[i];
                var distance = delta.Length();

                if (distance < 0.001f) continue;

                var direction = delta / distance;

                // Linear attraction
                var attraction = direction * (AttractionK * distance);

                // Linear repulsion: Zone 1 (own radius) for all, Zone 2 (2×min) for connected
                var connected = AreConnected(i, j);
                var rI = NodeRadius(i);
                var rJ = NodeRadius(j);
                var zoneL = connected ? 2 * Math.Min(rI, rJ) + 0.02f : Math.Max(Zone3Radius(i), Zone3Radius(j));
                var activeZone = 2f * zoneL;
                var slopeK = zoneL > 0.001f ? (RepulsionS - RepulsionP) / activeZone : 0f;

                Vector2 repulsion = Vector2.Zero;
                if (distance <= activeZone)
                {
                    var force = RepulsionS - slopeK * distance;
                    repulsion = -direction * force;
                }

                var pairForce = attraction + repulsion;
                forces[i] += pairForce;
                forces[j] -= pairForce;
            }
        }

        // Step 3: Integrate
        for (var i = 0; i < Nodes.Count; i++)
        {
            var friction = -FrictionK * Nodes[i].Velocity;
            var totalForce = forces[i] + friction;

            var acceleration = totalForce;
            Nodes[i].Velocity += acceleration * dt;

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

    public static void CreateGraphABC(PhysicsModel model, float cx, float cy)
    {
        model.Nodes.Clear();
        model.Edges.Clear();

        var a = new PhysicsNode("A", cx - 200, cy) { Width = 320f, Height = 160f };
        var b = new PhysicsNode("B", cx, cy);
        var c = new PhysicsNode("C", cx + 200, cy);

        model.Nodes.Add(a);
        model.Nodes.Add(b);
        model.Nodes.Add(c);

        model.Edges.Add(new Edge(0, 1));
        model.Edges.Add(new Edge(1, 2));
    }

    public static void CreateGraphABCSmall(PhysicsModel model, float cx, float cy)
    {
        model.Nodes.Clear();
        model.Edges.Clear();

        var a = new PhysicsNode("A", cx - 200, cy);
        var b = new PhysicsNode("B", cx, cy) { Width = 320f, Height = 160f };
        var c = new PhysicsNode("C", cx + 200, cy);

        model.Nodes.Add(a);
        model.Nodes.Add(b);
        model.Nodes.Add(c);

        model.Edges.Add(new Edge(0, 1));
        model.Edges.Add(new Edge(1, 2));
    }

    public static void CreateFullyConnected(PhysicsModel model, float cx, float cy, int count)
    {
        model.Nodes.Clear();
        model.Edges.Clear();

        var labels = new[] { "A", "B", "C", "D", "E", "F" };
        var radius = 100f + count * 20;

        for (var i = 0; i < count; i++)
        {
            var angle = 2 * MathF.PI * i / count - MathF.PI / 2;
            var x = cx + radius * MathF.Cos(angle);
            var y = cy + radius * MathF.Sin(angle);
            model.Nodes.Add(new PhysicsNode(labels[i], x, y));
        }

        for (var i = 0; i < count; i++)
            for (var j = i + 1; j < count; j++)
                model.Edges.Add(new Edge(i, j));
    }
}
