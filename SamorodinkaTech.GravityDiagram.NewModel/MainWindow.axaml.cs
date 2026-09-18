using Avalonia.Controls;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Главное окно Avalonia-приложения.
/// Содержит ModelView для отрисовки графа и панель параметров (слайдеры, чекбоксы).
/// </summary>
public partial class MainWindow : Window
{
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        SetupBindings();
        _initialized = true;
    }

    /// <summary>
    /// Привязывает UI-элементы (слайдеры, чекбоксы, кнопки) к параметрам физической модели.
    /// </summary>
    private void SetupBindings()
    {
        var model = ModelView.Model;

        // Params toggle
        var paramsToggle = this.FindControl<Button>("ParamsToggle")!;
        var paramsPanel = this.FindControl<StackPanel>("ParamsPanel")!;
        paramsToggle.Click += (_, _) =>
        {
            paramsPanel.IsVisible = !paramsPanel.IsVisible;
            paramsToggle.Content = paramsPanel.IsVisible ? "Скрыть параметры" : "Параметры модели";
        };

        // Parameter sliders
        BindSlider("RepulsionPSlider", "RepulsionPValue",
            () => model.RepulsionP, v => model.RepulsionP = v, "F0");
        BindSlider("AttractionSlider", "AttractionValue",
            () => model.AttractionK, v => model.AttractionK = v, "F3");
        BindSlider("FrictionSlider", "FrictionValue",
            () => model.FrictionK, v => model.FrictionK = v, "F2");

        // Checkboxes
        var jitterCheck = this.FindControl<CheckBox>("JitterCheck")!;
        jitterCheck.IsCheckedChanged += (_, _) =>
        {
            if (!_initialized) return;
            model.UseJitter = jitterCheck.IsChecked == true;
        };

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

    /// <summary>
    /// Привязывает слайдер к свойству модели и обновляет текстовое значение.
    /// </summary>
    /// <param name="sliderName">Имя слайдера в AXAML.</param>
    /// <param name="valueTextName">Имя TextBlock для отображения текущего значения.</param>
    /// <param name="get">Геттер текущего значения из модели.</param>
    /// <param name="set">Сеттер значения в модель.</param>
    /// <param name="format">Формат строки для числового значения.</param>
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
