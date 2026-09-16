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

    public PhysicsNode(string label, float x, float y)
    {
        Label = label;
        Position = new Vector2(x, y);
    }
}

public sealed class PhysicsModel
{
    public readonly List<PhysicsNode> Nodes = [];

    public float RepulsionK = 50f;
    public float RepulsionZoneSize = 100f;
    public float AttractionK = 0.05f;
    public float FrictionK = 1.0f;
    public bool UseJitter = true;

    private const float MaxSpeed = 2000f;
    private static readonly Random Rng = new();

    public void Step(float dt)
    {
        var forces = new Vector2[Nodes.Count];

        // Step 1: Compute jittered (virtual) positions for force calculation
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

        // Step 2: Pairwise forces based on virtual positions
        for (var i = 0; i < Nodes.Count; i++)
        {
            for (var j = i + 1; j < Nodes.Count; j++)
            {
                var delta = virtualPositions[j] - virtualPositions[i];
                var distance = delta.Length();

                if (distance < 0.001f) continue;

                var direction = delta / distance;

                // Linear attraction (always active)
                var attraction = direction * (AttractionK * distance);

                // Linear repulsion (only within zone)
                Vector2 repulsion = Vector2.Zero;
                if (distance < RepulsionZoneSize)
                {
                    repulsion = -direction * (RepulsionK * (RepulsionZoneSize - distance));
                }

                var pairForce = attraction + repulsion;
                forces[i] += pairForce;
                forces[j] -= pairForce;
            }
        }

        // Step 3: Integrate using real positions and computed forces
        for (var i = 0; i < Nodes.Count; i++)
        {
            var friction = -FrictionK * Nodes[i].Velocity;
            var totalForce = forces[i] + friction;

            // F = ma, mass = 1
            var acceleration = totalForce;
            Nodes[i].Velocity += acceleration * dt;

            // Clamp speed
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

    public static List<PhysicsNode> CreateDefaultNodes(int count, float cx, float cy)
    {
        var nodes = new List<PhysicsNode>(count);
        for (var i = 0; i < count; i++)
            nodes.Add(new PhysicsNode((i + 1).ToString(), cx, cy));
        return nodes;
    }
}
