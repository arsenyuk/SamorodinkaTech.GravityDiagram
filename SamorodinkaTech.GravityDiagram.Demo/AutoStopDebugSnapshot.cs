namespace SamorodinkaTech.GravityDiagram.Demo;

public sealed record AutoStopDebugSnapshot(
    int TickCounter,
    bool IsAutoStopped,
    int NoPixelMoveTicks,
    int AutoStopNoPixelMoveTicksThreshold,
    int LastPixelMoveTick,
    string LastPixelMoveInfo,
    bool EnablePortLabelAwareNodeMovement,
    bool EnableArcLabelAwareNodeMovement);
