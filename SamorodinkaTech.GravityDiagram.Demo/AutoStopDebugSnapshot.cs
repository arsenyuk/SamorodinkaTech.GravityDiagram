namespace SamorodinkaTech.GravityDiagram.Demo;

/// <summary>
/// Снимок состояния системы автостопа симуляции.
/// Фиксирует тик, на котором движение по пикселям прекратилось, и порог срабатывания.
/// Используется для отладки дампов и diagnostics.
/// </summary>
public sealed record AutoStopDebugSnapshot(
    int TickCounter,
    bool IsAutoStopped,
    int NoPixelMoveTicks,
    int AutoStopNoPixelMoveTicksThreshold,
    int LastPixelMoveTick,
    string LastPixelMoveInfo,
    bool EnablePortLabelAwareNodeMovement,
    bool EnableArcLabelAwareNodeMovement);
