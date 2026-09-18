namespace SamorodinkaTech.GravityDiagram.Core;

/// <summary>
/// Двумерный вектор, используемый для хранения позиций,
/// скоростей и сил в дампе. Обёртка над парой float
/// для совместимости с System.Numerics при десериализации.
/// </summary>
public sealed record DumpVec2(float X, float Y);
