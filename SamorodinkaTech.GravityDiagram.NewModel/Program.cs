using Avalonia;
using System;
using System.Threading.Tasks;

namespace SamorodinkaTech.GravityDiagram.NewModel;

/// <summary>
/// Точка входа Avalonia-приложения.
/// Регистрирует глобальные обработчики необработанных исключений
/// и запускает графический интерфейс.
/// </summary>
class Program
{
    /// <summary>
    /// Главный метод: регистрирует обработчики ошибок и запускает Avalonia.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[FATAL] {e.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine($"[TaskScheduler] {e.Exception}");
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Собирает и настраивает билдер Avalonia с платформенным детектом и шрифтом Inter.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
