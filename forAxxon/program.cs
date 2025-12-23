using Avalonia;
using forAxxon.services;
using forAxxon.Services;
using forAxxon.ViewModels;
using Microsoft;
using System;
using Microsoft.Extensions.DependencyInjection;
namespace forAxxon;

internal class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddTransient<MainWindowViewModel>();
        var serviceProvider = services.BuildServiceProvider();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .AfterSetup(app =>
            {
                ((App)app.Instance!).ServiceProvider = serviceProvider;
            });
    }
}