using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Unified dump model that handles both schema v4 and v5 JSON formats.
/// Missing fields from older schemas default to sensible values.
/// </summary>
public sealed record DumpRoot(DumpSettings Settings, DumpDiagram Diagram);

public sealed record DumpSettings(
	float NodeMass,
	float Softening,
	float BackgroundPairGravity,
	float EdgeSpringRestLength,
	float ConnectedArcAttractionK,
	bool MinimizeArcLength,
	float MinNodeSpacing,
	bool UseHardMinSpacing,
	int HardMinSpacingIterations,
	float HardMinSpacingSlop,
	float OverlapRepulsionK,
	float SoftOverlapBoostWhenHardDisabled,
	float Drag,
	float MaxSpeed,
	float ArcPointAttractionK = 6f,
	float ArcPointMoveFactor = 0.035f,
	float ArcPointNodeRepulsionK = 1200f,
	float ArcPointMergeDistance = 2f,
	int ArcPointConstraintIterations = 6,
	float ArcPointExtraClearance = 0f,
	int MaxArcInternalPoints = 64);

public sealed record DumpDiagram(DumpNode[] Nodes, DumpPort[] Ports, DumpArc[] Arcs);

public sealed record DumpNode(
	string Id,
	string? Text,
	DumpVec2 Position,
	DumpVec2 Velocity,
	float Width,
	float Height);

public sealed record DumpPort(
	string Id,
	string? Text,
	string NodeId,
	string Side,
	float Offset,
	float ClampedOffset = 0f,
	DumpVec2? WorldPosition = null);

public sealed record DumpArc(
	string Id,
	string? Text,
	string FromPortId,
	string ToPortId,
	DumpVec2[] InternalPoints = null!,
	DumpVec2[] InternalPointForces = null!);

public sealed record DumpVec2(float X, float Y);
