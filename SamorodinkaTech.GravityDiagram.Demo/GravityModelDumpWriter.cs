using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Demo;

public static class GravityModelDumpWriter
{
    private static int Pixel(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    public static string WriteDump(Diagram diagram, GravityLayoutEngine engine, float dt, AutoStopDebugSnapshot? uiDebug = null)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(engine);
        if (dt <= 0) throw new ArgumentOutOfRangeException(nameof(dt), "dt must be > 0.");

        var preview = engine.PreviewStep(diagram, dt);

        var dump = BuildDump(diagram, engine.Settings, preview, uiDebug);
        var path = GetUniqueDumpPath(dump.CreatedAtUtc);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(dump, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        File.WriteAllText(path, json);
        return path;
    }

    private static GravityModelDump BuildDump(Diagram diagram, LayoutSettings settings, LayoutStepPreview preview, AutoStopDebugSnapshot? uiDebug)
    {
        // Movement criterion: pixel-stability of integer center coordinates.
        // We don't rely on vector magnitudes since they can jitter while integer pixels stay fixed.
        var currentPixelsById = new Dictionary<string, (int X, int Y)>(StringComparer.Ordinal);
        foreach (var n in diagram.Nodes)
        {
            var c = n.Bounds.Center;
            currentPixelsById[n.Id.Value] = (Pixel((float)c.X), Pixel((float)c.Y));
        }

        var willMoveNextStep = false;
        foreach (var p in preview.Nodes)
        {
            if (!currentPixelsById.TryGetValue(p.Id.Value, out var cur))
            {
                // Conservative: if mapping fails, assume movement could happen.
                willMoveNextStep = true;
                break;
            }

            var pred = (Pixel(p.PredictedPosition.X), Pixel(p.PredictedPosition.Y));
            var predNoForces = (Pixel(p.PredictedPositionIfNoForces.X), Pixel(p.PredictedPositionIfNoForces.Y));

            if (pred != cur || predNoForces != cur)
            {
                willMoveNextStep = true;
                break;
            }
        }

        // Dump is a snapshot; we don't have "previous frame" here.
        // Use the same pixel-based criterion as a practical "moving now" indicator.
        var isMovingNow = willMoveNextStep;

        var nodes = diagram.Nodes
            .Select(n => new DumpNode(
                Id: n.Id.Value,
                Text: n.Text,
                Position: Vec2(n.Position),
                Velocity: Vec2(n.Velocity),
                Width: n.Width,
                Height: n.Height,
                Mass: settings.NodeMass,
                Bounds: new DumpRect(n.Bounds.Left, n.Bounds.Top, n.Bounds.Width, n.Bounds.Height)))
            .ToList();

        var ports = diagram.Ports
            .Select(p =>
            {
                var node = diagram.TryGetNode(p.Ref.NodeId);
                var world = node is null
                    ? (DumpVec2?)null
                    : Vec2(GravityLayoutEngine.GetPortWorldPosition(node, p.Ref));

                return new DumpPort(
                    Id: p.Id.Value,
                    Text: p.Text,
                    NodeId: p.Ref.NodeId.Value,
                    Side: p.Ref.Side.ToString(),
                    Offset: p.Ref.Offset,
                    ClampedOffset: p.Ref.ClampedOffset,
                    WorldPosition: world);
            })
            .ToList();

        var arcPreviewById = preview.Arcs.ToDictionary(a => a.Id.Value, StringComparer.Ordinal);
        var arcs = diagram.Arcs
            .Select(a =>
            {
                arcPreviewById.TryGetValue(a.Id.Value, out var ap);
                var internalPoints = a.InternalPoints.Select(Vec2).ToArray();
                var internalPointForces = ap?.InternalPoints.Select(p => Vec2(p.Force)).ToArray() ?? Array.Empty<DumpVec2>();

                return new DumpArc(
                    Id: a.Id.Value,
                    Text: a.Text,
                    FromPortId: a.FromPortId.Value,
                    ToPortId: a.ToPortId.Value,
                    InternalPoints: internalPoints,
                    InternalPointForces: internalPointForces);
            })
            .ToList();

        var previewByNodeId = preview.Nodes.ToDictionary(n => n.Id.Value, StringComparer.Ordinal);
        var forces = nodes
            .Select(n =>
            {
                if (!previewByNodeId.TryGetValue(n.Id, out var p))
                {
                    return new DumpNodeForces(
                        NodeId: n.Id,
                        ForceBackgroundGravity: DumpVec2.Zero,
                        ForceOverlapRepulsion: DumpVec2.Zero,
                        ForceConnectedArcAttraction: DumpVec2.Zero,
						ForceArcPointEndpoint: DumpVec2.Zero,
                        ForceTotal: DumpVec2.Zero,
                        PredictedPositionIfNoForces: n.Position,
                        PredictedVelocityIfNoForces: n.Velocity,
                        DeltaPositionIfNoForces: DumpVec2.Zero,
                        PredictedPositionBeforeConstraints: n.Position,
                        PredictedPosition: n.Position,
                        DeltaPositionBeforeConstraints: DumpVec2.Zero,
                        DeltaPosition: DumpVec2.Zero);
                }

                return new DumpNodeForces(
                    NodeId: n.Id,
                    ForceBackgroundGravity: Vec2(p.ForceBackgroundGravity),
                    ForceOverlapRepulsion: Vec2(p.ForceOverlapRepulsion),
                    ForceConnectedArcAttraction: Vec2(p.ForceConnectedArcAttraction),
                    ForceArcPointEndpoint: Vec2(p.ForceArcPointEndpoint),
                    ForceTotal: Vec2(p.ForceTotal),
                    PredictedPositionIfNoForces: Vec2(p.PredictedPositionIfNoForces),
                    PredictedVelocityIfNoForces: Vec2(p.PredictedVelocityIfNoForces),
                    DeltaPositionIfNoForces: Vec2(p.DeltaPositionIfNoForces),
                    PredictedPositionBeforeConstraints: Vec2(p.PredictedPositionBeforeConstraints),
                    PredictedPosition: Vec2(p.PredictedPosition),
                    DeltaPositionBeforeConstraints: Vec2(p.DeltaPositionBeforeConstraints),
                    DeltaPosition: Vec2(p.DeltaPosition));
            })
            .ToList();

        return new GravityModelDump(
            SchemaVersion: 5,
            CreatedAtUtc: preview.CreatedAtUtc,
            Dt: preview.Dt,
            IsMovingNow: isMovingNow,
            WillMoveNextStep: willMoveNextStep,
            MaxSpeed: 0f,
            MaxPredictedDelta: 0f,
            MaxPredictedDeltaIfNoForces: 0f,
            Settings: new DumpSettings(settings),
            Diagram: new DumpDiagram(nodes, ports, arcs),
            Forces: forces,
            SumBackgroundGravityForce: Vec2(preview.SumBackgroundGravityForce),
            SumOverlapRepulsionForce: Vec2(preview.SumOverlapRepulsionForce),
            SumConnectedArcAttractionForce: Vec2(preview.SumConnectedArcAttractionForce),
            SumArcPointEndpointForce: Vec2(preview.SumArcPointEndpointForce),
            SumTotalForce: Vec2(preview.SumTotalForce),
            UiDebug: uiDebug);
    }

    private static DumpVec2 Vec2(Vector2 v) => new(v.X, v.Y);

    private static string GetUniqueDumpPath(DateTimeOffset createdAtUtc)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "SamorodinkaTech.GravityDiagram", "dumps");

        var stamp = createdAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss.fff", CultureInfo.InvariantCulture);
        var name = $"gravity-dump-{stamp}-{Guid.NewGuid():N}.json";
        return Path.Combine(folder, name);
    }

    public sealed record GravityModelDump(
        int SchemaVersion,
        DateTimeOffset CreatedAtUtc,
        float Dt,
        bool IsMovingNow,
        bool WillMoveNextStep,
        float MaxSpeed,
        float MaxPredictedDelta,
        float MaxPredictedDeltaIfNoForces,
        DumpSettings Settings,
        DumpDiagram Diagram,
        IReadOnlyList<DumpNodeForces> Forces,
        DumpVec2 SumBackgroundGravityForce,
        DumpVec2 SumOverlapRepulsionForce,
        DumpVec2 SumConnectedArcAttractionForce,
        DumpVec2 SumArcPointEndpointForce,
        DumpVec2 SumTotalForce,
        AutoStopDebugSnapshot? UiDebug);

    public sealed record DumpSettings
    {
        public DumpSettings(LayoutSettings s)
        {
            NodeMass = s.NodeMass;
            Softening = s.Softening;
            BackgroundPairGravity = s.BackgroundPairGravity;
            EdgeSpringRestLength = s.EdgeSpringRestLength;
            ConnectedArcAttractionK = s.ConnectedArcAttractionK;
            MinimizeArcLength = s.MinimizeArcLength;
            MinNodeSpacing = s.MinNodeSpacing;
            UseHardMinSpacing = s.UseHardMinSpacing;
            HardMinSpacingIterations = s.HardMinSpacingIterations;
            HardMinSpacingSlop = s.HardMinSpacingSlop;
            OverlapRepulsionK = s.OverlapRepulsionK;
            SoftOverlapBoostWhenHardDisabled = s.SoftOverlapBoostWhenHardDisabled;
            Drag = s.Drag;
            MaxSpeed = s.MaxSpeed;

            ArcPointAttractionK = s.ArcPointAttractionK;
            ArcPointMoveFactor = s.ArcPointMoveFactor;
            ArcPointNodeRepulsionK = s.ArcPointNodeRepulsionK;
            ArcPointMergeDistance = s.ArcPointMergeDistance;
            ArcPointConstraintIterations = s.ArcPointConstraintIterations;
            ArcPointExtraClearance = s.ArcPointExtraClearance;
            MaxArcInternalPoints = s.MaxArcInternalPoints;
        }

        public float NodeMass { get; init; }
        public float Softening { get; init; }
        public float BackgroundPairGravity { get; init; }
        public float EdgeSpringRestLength { get; init; }
        public float ConnectedArcAttractionK { get; init; }
        public bool MinimizeArcLength { get; init; }
        public float MinNodeSpacing { get; init; }
        public bool UseHardMinSpacing { get; init; }
        public int HardMinSpacingIterations { get; init; }
        public float HardMinSpacingSlop { get; init; }
        public float OverlapRepulsionK { get; init; }
        public float SoftOverlapBoostWhenHardDisabled { get; init; }
        public float Drag { get; init; }
        public float MaxSpeed { get; init; }

        public float ArcPointAttractionK { get; init; }
        public float ArcPointMoveFactor { get; init; }
        public float ArcPointNodeRepulsionK { get; init; }
        public float ArcPointMergeDistance { get; init; }
        public int ArcPointConstraintIterations { get; init; }
        public float ArcPointExtraClearance { get; init; }
        public int MaxArcInternalPoints { get; init; }
    }

    public sealed record DumpDiagram(
        IReadOnlyList<DumpNode> Nodes,
        IReadOnlyList<DumpPort> Ports,
        IReadOnlyList<DumpArc> Arcs);

    public sealed record DumpNode(
        string Id,
        string Text,
        DumpVec2 Position,
        DumpVec2 Velocity,
        float Width,
        float Height,
        float Mass,
        DumpRect Bounds);

    public sealed record DumpPort(
        string Id,
        string Text,
        string NodeId,
        string Side,
        float Offset,
        float ClampedOffset,
        DumpVec2? WorldPosition);

    public sealed record DumpArc(
        string Id,
        string Text,
        string FromPortId,
        string ToPortId,
        DumpVec2[] InternalPoints,
        DumpVec2[] InternalPointForces);

    public sealed record DumpNodeForces(
        string NodeId,
        DumpVec2 ForceBackgroundGravity,
        DumpVec2 ForceOverlapRepulsion,
        DumpVec2 ForceConnectedArcAttraction,
		DumpVec2 ForceArcPointEndpoint,
        DumpVec2 ForceTotal,
            DumpVec2 PredictedPositionIfNoForces,
            DumpVec2 PredictedVelocityIfNoForces,
            DumpVec2 DeltaPositionIfNoForces,
        DumpVec2 PredictedPositionBeforeConstraints,
        DumpVec2 PredictedPosition,
        DumpVec2 DeltaPositionBeforeConstraints,
        DumpVec2 DeltaPosition);

    public readonly record struct DumpRect(float X, float Y, float Width, float Height);

    public readonly record struct DumpVec2(float X, float Y)
    {
        public static DumpVec2 Zero => new(0, 0);
    }
}
