using Avalonia;
using System;

namespace SamorodinkaTech.GravityDiagram.Demo;

/// <summary>
/// Точка входа демо-приложения. Настраивает Avalonia и запускает классический desktop-lifetime.
/// </summary>
class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    /// <summary>Главная точка входа. Не используйте Avalonia API до вызова AppMain.</summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>Конфигурация Avalonia (платформа, шрифт, логирование). Используется визуальным дизайнером.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
