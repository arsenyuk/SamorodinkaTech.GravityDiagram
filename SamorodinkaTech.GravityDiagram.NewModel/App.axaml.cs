using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Класс Avalonia-приложения.
/// Загружает XAML--resources и создаёт главное окно при инициализации фреймворка.
/// </summary>
public partial class App : Application
{
    /// <summary>Загружает XAML-определения (стили, ресурсы).</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Создаёт и назначает MainWindow после инициализации фреймворка Avalonia.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
