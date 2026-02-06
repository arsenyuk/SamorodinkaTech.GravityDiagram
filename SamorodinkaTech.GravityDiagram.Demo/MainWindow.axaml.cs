using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using System;

namespace SamorodinkaTech.GravityDiagram.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DiagramView.SetDiagram(SampleDiagram.Create());
        SetupSettingsPanel();
    }

    private void SetupSettingsPanel()
    {
        var s = DiagramView.Engine.Settings;

        BindSlider("GravitySlider", "GravityValue", () => s.BackgroundPairGravity, v => s.BackgroundPairGravity = v, "0.000");
        BindSlider("SpringKSlider", "SpringKValue", () => s.EdgeSpringK, v => s.EdgeSpringK = v, "0.00");
        BindSlider("RestLenSlider", "RestLenValue", () => s.EdgeSpringRestLength, v => s.EdgeSpringRestLength = v, "0");
        BindSlider("RepulsionSlider", "RepulsionValue", () => s.OverlapRepulsionK, v => s.OverlapRepulsionK = v, "0");
        BindSlider("SoftOverlapBoostSlider", "SoftOverlapBoostValue", () => s.SoftOverlapBoostWhenHardDisabled, v => s.SoftOverlapBoostWhenHardDisabled = v, "0.00");
        BindSlider("MinSpacingSlider", "MinSpacingValue", () => s.MinNodeSpacing, v => s.MinNodeSpacing = v, "0");
        BindSlider("HardIterSlider", "HardIterValue", () => s.HardMinSpacingIterations, v => s.HardMinSpacingIterations = (int)Math.Round(v), "0");

        BindCheckBox("MinimizeArcCheck", () => s.MinimizeArcLength, v => s.MinimizeArcLength = v);
        BindCheckBox("HardSpacingCheck", () => s.UseHardMinSpacing, v => s.UseHardMinSpacing = v);

        var stabilize = this.FindControl<Button>("StabilizeButton");
        stabilize.Click += (_, _) => DiagramView.StabilizeLayout();
    }

    private void BindSlider(string sliderName, string valueTextName, Func<float> get, Action<float> set, string format)
    {
        var slider = this.FindControl<Slider>(sliderName);
        var text = this.FindControl<TextBlock>(valueTextName);

        slider.Value = get();
        text.Text = get().ToString(format);
        slider.ValueChanged += (_, e) =>
        {
            set((float)e.NewValue);
            text.Text = ((float)e.NewValue).ToString(format);
            DiagramView.ResetVelocities();
        };
    }

    private void BindCheckBox(string checkBoxName, Func<bool> get, Action<bool> set)
    {
        var cb = this.FindControl<CheckBox>(checkBoxName);
        cb.IsChecked = get();
        cb.Checked += (_, _) =>
        {
            set(true);
            DiagramView.ResetVelocities();
        };
        cb.Unchecked += (_, _) =>
        {
            set(false);
            DiagramView.ResetVelocities();
        };
    }
}