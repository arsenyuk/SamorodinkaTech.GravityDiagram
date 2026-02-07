using System;
using System.IO;
using System.Text.Json;
using SamorodinkaTech.GravityDiagram.Core;

namespace SamorodinkaTech.GravityDiagram.Demo;

public sealed class DemoSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string SettingsFilePath { get; }

    public DemoSettingsStore(string? settingsFilePath = null)
    {
        SettingsFilePath = settingsFilePath ?? GetDefaultSettingsFilePath();
    }

    public bool TryLoadInto(LayoutSettings target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!File.Exists(SettingsFilePath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var dto = JsonSerializer.Deserialize<LayoutSettingsDto>(json, JsonOptions);
            if (dto is null) return false;

            dto.ApplyTo(target);
            return true;
        }
        catch
        {
            // Keep demo resilient: ignore broken settings file.
            return false;
        }
    }

    public void Save(LayoutSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var dto = LayoutSettingsDto.From(source);
        var json = JsonSerializer.Serialize(dto, JsonOptions);

        var dir = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(SettingsFilePath, json);
    }

    private static string GetDefaultSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        // macOS: ~/Library/Application Support
        // Windows: %AppData%
        // Linux: ~/.config (usually)
        return Path.Combine(appData, "SamorodinkaTech.GravityDiagram", "demo-layout-settings.json");
    }

    private sealed record LayoutSettingsDto(
        int Version,
        float NodeMass,
        float BackgroundPairGravity,
        float ConnectedArcAttractionK,
        float EdgeSpringRestLength,
        bool MinimizeArcLength,
        float MinNodeSpacing,
        bool UseHardMinSpacing,
        int HardMinSpacingIterations,
        float HardMinSpacingSlop,
        float OverlapRepulsionK,
        float SoftOverlapBoostWhenHardDisabled,
        float Softening,
        float Drag,
        float MaxSpeed)
    {
        public static LayoutSettingsDto From(LayoutSettings s) => new(
            Version: 2,
            NodeMass: s.NodeMass,
            BackgroundPairGravity: s.BackgroundPairGravity,
            ConnectedArcAttractionK: s.ConnectedArcAttractionK,
            EdgeSpringRestLength: s.EdgeSpringRestLength,
            MinimizeArcLength: s.MinimizeArcLength,
            MinNodeSpacing: s.MinNodeSpacing,
            UseHardMinSpacing: s.UseHardMinSpacing,
            HardMinSpacingIterations: s.HardMinSpacingIterations,
            HardMinSpacingSlop: s.HardMinSpacingSlop,
            OverlapRepulsionK: s.OverlapRepulsionK,
            SoftOverlapBoostWhenHardDisabled: s.SoftOverlapBoostWhenHardDisabled,
            Softening: s.Softening,
            Drag: s.Drag,
            MaxSpeed: s.MaxSpeed);

        public void ApplyTo(LayoutSettings s)
        {
            if (NodeMass > 0f)
            {
                s.NodeMass = NodeMass;
            }

            s.BackgroundPairGravity = BackgroundPairGravity;
            s.ConnectedArcAttractionK = ConnectedArcAttractionK;
            s.EdgeSpringRestLength = EdgeSpringRestLength;
            s.MinimizeArcLength = MinimizeArcLength;
            s.MinNodeSpacing = MinNodeSpacing;
            s.UseHardMinSpacing = UseHardMinSpacing;
            s.HardMinSpacingIterations = HardMinSpacingIterations;
            s.HardMinSpacingSlop = HardMinSpacingSlop;
            s.OverlapRepulsionK = OverlapRepulsionK;
            s.SoftOverlapBoostWhenHardDisabled = SoftOverlapBoostWhenHardDisabled;
            s.Softening = Softening;
            s.Drag = Drag;
            s.MaxSpeed = MaxSpeed;
        }
    }
}
