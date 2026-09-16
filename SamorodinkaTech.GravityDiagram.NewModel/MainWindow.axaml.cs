using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SamorodinkaTech.GravityDiagram.NewModel;

public partial class MainWindow : Window
{
    private int _currentNodeCount;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        SetupBindings();
        _initialized = true;
    }

    private void SetupBindings()
    {
        var model = ModelView.Model;

        BindSlider("RepulsionSSlider", "RepulsionSValue",
            () => model.RepulsionS, v => model.RepulsionS = v, "F0");

        BindSlider("RepulsionPSlider", "RepulsionPValue",
            () => model.RepulsionP, v => model.RepulsionP = v, "F0");

        BindSlider("RepulsionLSlider", "RepulsionLValue",
            () => model.RepulsionL, v => model.RepulsionL = v, "F0");

        BindSlider("AttractionSlider", "AttractionValue",
            () => model.AttractionK, v => model.AttractionK = v, "F3");

        BindSlider("FrictionSlider", "FrictionValue",
            () => model.FrictionK, v => model.FrictionK = v, "F2");

        // Random Jitter checkbox
        var jitterCheck = this.FindControl<CheckBox>("JitterCheck")!;
        jitterCheck.IsCheckedChanged += (_, _) =>
        {
            if (!_initialized) return;
            model.UseJitter = jitterCheck.IsChecked == true;
        };

        // Node count selector
        _currentNodeCount = 5;
        var nodeCountSelector = this.FindControl<ComboBox>("NodeCountSelector")!;
        nodeCountSelector.SelectionChanged += (_, e) =>
        {
            if (!_initialized) return;
            if (nodeCountSelector.SelectedIndex < 0) return;
            var count = 5 - nodeCountSelector.SelectedIndex;
            if (count == _currentNodeCount) return;
            _currentNodeCount = count;
            ModelView.SetNodeCount(count);
        };

        // Reset button
        var resetButton = this.FindControl<Button>("ResetButton")!;
        resetButton.Click += (_, _) =>
        {
            var count = 5 - nodeCountSelector.SelectedIndex;
            _currentNodeCount = count;
            ModelView.SetNodeCount(count);
        };
    }

    private void BindSlider(string sliderName, string valueTextName,
        System.Func<float> get, System.Action<float> set, string format)
    {
        var slider = this.FindControl<Slider>(sliderName)!;
        var valueText = this.FindControl<TextBlock>(valueTextName)!;

        slider.Value = get();
        valueText.Text = get().ToString(format);

        slider.ValueChanged += (_, e) =>
        {
            if (!_initialized) return;
            set((float)e.NewValue);
            valueText.Text = ((float)e.NewValue).ToString(format);
            ModelView.ResetSimulation();
        };
    }
}
