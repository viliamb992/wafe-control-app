using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Desktop.ViewModels;
using RecuperationSystem.Desktop.Views;
using System;
using System.Threading.Tasks;

namespace RecuperationSystem.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Get services from DI container
            var viewModel = Program.ServiceProvider?.GetRequiredService<AppViewModel>();
            var authService = Program.ServiceProvider?.GetRequiredService<IAuthenticationService>();
            var trayIconService = Program.ServiceProvider?.GetRequiredService<ITrayIconService>() as TrayIconService;
            
            var mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            // Set the main window reference and initialize tray icon
            trayIconService?.SetMainWindow(mainWindow);
            trayIconService?.Initialize();
            mainWindow.TrayIconService = trayIconService;

            _ = ShowMainWindowAfterLoginAsync(desktop, mainWindow, authService);

            // Cleanup on shutdown
            desktop.ShutdownRequested += (sender, e) =>
            {
                trayIconService?.Dispose();
                
                // Dispose ViewModel if it implements IDisposable
                if (viewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            };

#if DEBUG
            // Enable DevTools with F12 key
            mainWindow.AttachDevTools();
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ShowMainWindowAfterLoginAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow,
        IAuthenticationService authService)
    {
        // Ensure the main window is shown immediately.
        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();

        // Try auto-login. If it fails, the inline login UI will be shown by MainWindow.
        _ = await authService.TryAutoLoginAsync();

        desktop.MainWindow = mainWindow;
    }
}
