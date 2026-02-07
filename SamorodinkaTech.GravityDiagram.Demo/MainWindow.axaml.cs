using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace SamorodinkaTech.GravityDiagram.Demo;

public partial class MainWindow : Window
{
    private readonly DemoSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _saveDebounce;
	private bool _suppressAutoSave;

    public MainWindow()
    {
        InitializeComponent();
        DiagramView.SetDiagram(SampleDiagram.CreateThreeNode());

        // Prepare debounced auto-save.
        _saveDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
        {
            _saveDebounce.Stop();
            TrySaveSettings();
        });

        SetupSettingsPanel();

        Closed += (_, _) => TrySaveSettings();
    }

    private void SetupSettingsPanel()
    {
        var s = DiagramView.Engine.Settings;

        // Load settings before binding so sliders reflect persisted values.
        _suppressAutoSave = true;
        _settingsStore.TryLoadInto(s);
        _suppressAutoSave = false;

        BindSlider("GravitySlider", "GravityValue", () => s.BackgroundPairGravity, v => s.BackgroundPairGravity = v, "0.000");
        BindSlider("NodeMassSlider", "NodeMassValue", () => s.NodeMass, v => s.NodeMass = v, "0.00");
        BindSlider("ConnectedAttractionSlider", "ConnectedAttractionValue", () => s.ConnectedArcAttractionK, v => s.ConnectedArcAttractionK = v, "0.00");
        BindSlider("RestLenSlider", "RestLenValue", () => s.EdgeSpringRestLength, v => s.EdgeSpringRestLength = v, "0");
        BindSlider("RepulsionSlider", "RepulsionValue", () => s.OverlapRepulsionK, v => s.OverlapRepulsionK = v, "0");
        BindSlider("SoftOverlapBoostSlider", "SoftOverlapBoostValue", () => s.SoftOverlapBoostWhenHardDisabled, v => s.SoftOverlapBoostWhenHardDisabled = v, "0.00");
        BindSlider("MinSpacingSlider", "MinSpacingValue", () => s.MinNodeSpacing, v => s.MinNodeSpacing = v, "0");
        BindSlider("HardIterSlider", "HardIterValue", () => s.HardMinSpacingIterations, v => s.HardMinSpacingIterations = (int)Math.Round(v), "0");

        BindCheckBox("MinimizeArcCheck", () => s.MinimizeArcLength, v => s.MinimizeArcLength = v);
        BindCheckBox("HardSpacingCheck", () => s.UseHardMinSpacing, v => s.UseHardMinSpacing = v);

        var stabilize = this.FindControl<Button>("StabilizeButton");
        stabilize.Click += (_, _) => DiagramView.StabilizeLayout();

        var dump = this.FindControl<Button>("DumpButton");
        dump.Click += async (_, _) => await DumpNowAsync();

        var modelSelector = this.FindControl<ComboBox>("ModelSelector");
        modelSelector.SelectionChanged += (_, _) =>
        {
            if (modelSelector.SelectedIndex == 0)
                DiagramView.SetDiagram(SampleDiagram.CreateThreeNode());
            else
                DiagramView.SetDiagram(SampleDiagram.CreateTwoNode());
        };
    }

    private async Task DumpNowAsync()
    {
        var status = this.FindControl<TextBlock>("DumpStatus");
        try
        {
            // Fixed dt for reproducibility.
            const float dt = 1f / 60f;
            var path = GravityModelDumpWriter.WriteDump(DiagramView.Diagram, DiagramView.Engine, dt, DiagramView.GetAutoStopDebugSnapshot());
            status.Text = $"Сохранено: {path}";

            try
            {
                if (Clipboard is not null)
                {
                    await Clipboard.SetTextAsync(path);
                    status.Text += " (путь скопирован)";
                }
            }
            catch
            {
                // Ignore clipboard errors.
            }
        }
        catch (Exception ex)
        {
            status.Text = $"Ошибка дампа: {ex.GetType().Name}: {ex.Message}";
        }
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
            RequestSaveSettings();
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
            RequestSaveSettings();
        };
        cb.Unchecked += (_, _) =>
        {
            set(false);
            DiagramView.ResetVelocities();
            RequestSaveSettings();
        };
    }

    private void RequestSaveSettings()
    {
        if (_suppressAutoSave) return;
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void TrySaveSettings()
    {
        try
        {
            _settingsStore.Save(DiagramView.Engine.Settings);
        }
        catch
        {
            // Demo should not crash due to IO errors.
        }
    }
}