using Avalonia;
using Avalonia.ReactiveUI;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecuperationSystem.Desktop.Configuration;
using Serilog;

namespace RecuperationSystem.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Determine environment based on build configuration
#if DEBUG
        var environment = "Development";
#else
        var environment = "Production";
#endif

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .Build();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        // Setup Dependency Injection
        var services = new ServiceCollection();
        services.AddApplicationServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        try
        {
            Log.Information("Starting Recuperation System Desktop Application in {Environment} mode", environment);
            BuildAvaloniaApp(serviceProvider)
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            // Dispose service provider
            if (serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider? serviceProvider = null)
        => AppBuilder.Configure(() =>
            {
                var app = new App();
                if (serviceProvider != null)
                    app.SetServices(serviceProvider);
                return app;
            })
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
}
