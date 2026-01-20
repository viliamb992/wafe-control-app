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
        // Ensure we have an owner window for modal dialogs.
        // We keep the app from hitting the API before credentials are available,
        // but we still need a shown window to own the login dialog.
        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();

        // First try auto-login, otherwise show the prompt.
        var loggedIn = await authService.TryAutoLoginAsync();
        if (!loggedIn)
        {
            await PromptForLoginIfNeededAsync(mainWindow, authService);
        }

        // Only show the main window if we have credentials and authentication passed.
        if (authService.IsAuthenticated)
        {
            desktop.MainWindow = mainWindow;
        }
        else
        {
            desktop.Shutdown();
        }
    }

    private static async Task PromptForLoginIfNeededAsync(Avalonia.Controls.Window owner, IAuthenticationService authService)
    {
        try
        {
            var vm = new LoginViewModel();
            var dialog = new LoginWindow { DataContext = vm };

            vm.SubmitRequested += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(vm.Username) || string.IsNullOrEmpty(vm.Password))
                {
                    vm.ErrorMessage = "Username and password are required.";
                    return;
                }

                try
                {
                    var success = await authService.LoginAsync(vm.Username, vm.Password, vm.RememberMe);
                    if (success)
                    {
                        dialog.Close();
                    }
                    else
                    {
                        vm.ErrorMessage = "Invalid username or password.";
                    }
                }
                catch
                {
                    vm.ErrorMessage = "Sign in failed.";
                }
            };

            vm.CancelRequested += (_, _) => dialog.Close();

            await dialog.ShowDialog(owner);
        }
        catch
        {
            // Let existing logging handle errors in the view model/service.
        }
    }
}
