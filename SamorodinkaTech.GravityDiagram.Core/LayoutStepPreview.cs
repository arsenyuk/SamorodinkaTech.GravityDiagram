using System.Numerics;

namespace SamorodinkaTech.GravityDiagram.Core;

public sealed record LayoutStepPreview(
	DateTimeOffset CreatedAtUtc,
	float Dt,
	IReadOnlyList<NodeStepPreview> Nodes,
	IReadOnlyList<ArcStepPreview> Arcs,
	Vector2 SumBackgroundGravityForce,
	Vector2 SumOverlapRepulsionForce,
	Vector2 SumConnectedArcAttractionForce,
	Vector2 SumArcPointEndpointForce,
	Vector2 SumTotalForce);

public sealed record NodeStepPreview(
	DiagramId Id,
	Vector2 Position,
	Vector2 Velocity,
	float Mass,
	Vector2 ForceBackgroundGravity,
	Vector2 ForceOverlapRepulsion,
	Vector2 ForceConnectedArcAttraction,
	Vector2 ForceArcPointEndpoint,
	Vector2 ForceTotal,
	Vector2 PredictedPositionIfNoForces,
	Vector2 PredictedVelocityIfNoForces,
	Vector2 DeltaPositionIfNoForces,
	Vector2 PredictedPositionBeforeConstraints,
	Vector2 PredictedVelocityBeforeConstraints,
	Vector2 PredictedPosition,
	Vector2 PredictedVelocity,
	Vector2 DeltaPositionBeforeConstraints,
	Vector2 DeltaPosition);

public sealed record ArcStepPreview(
	DiagramId Id,
	DiagramId FromPortId,
	DiagramId ToPortId,
	IReadOnlyList<ArcPointStepPreview> InternalPoints);

public sealed record ArcPointStepPreview(
	int Index,
	Vector2 Position,
	Vector2 Force);
