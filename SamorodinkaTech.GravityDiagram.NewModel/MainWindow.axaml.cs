using Avalonia.Controls;

namespace SamorodinkaTech.GravityDiagram.NewModel;

public partial class MainWindow : Window
{
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

        BindSlider("UniversalRepulsionSlider", "UniversalRepulsionValue",
            () => model.UniversalRepulsionK, v => model.UniversalRepulsionK = v, "F1");

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

        // Orthogonal edges checkbox
        var orthogonalCheck = this.FindControl<CheckBox>("OrthogonalCheck")!;
        orthogonalCheck.IsCheckedChanged += (_, _) =>
        {
            if (!_initialized) return;
            ModelView.UseOrthogonalEdges = orthogonalCheck.IsChecked == true;
        };

        // Graph selector
        var graphSelector = this.FindControl<ComboBox>("GraphSelector")!;
        graphSelector.SelectionChanged += (_, _) =>
        {
            if (!_initialized) return;
            ModelView.LoadGraph(graphSelector.SelectedIndex);
        };

        // Reset button
        var resetButton = this.FindControl<Button>("ResetButton")!;
        resetButton.Click += (_, _) =>
        {
            var idx = this.FindControl<ComboBox>("GraphSelector")!.SelectedIndex;
            ModelView.LoadGraph(idx);
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
