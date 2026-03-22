using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using RecuperationSystem.Desktop.Views;
using Serilog;

namespace RecuperationSystem.Desktop.Services;

public class TrayIconService : ITrayIconService
{
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private bool _isExiting = false;

    public void SetMainWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void Initialize()
    {
        if (_mainWindow == null)
        {
            throw new InvalidOperationException("MainWindow must be set before initializing TrayIconService");
        }

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Wafe Recuperation Controller"
        };

        try
        {
            // Try multiple icon loading approaches
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");
            Log.Information("Attempting to load tray icon from: {IconPath}", iconPath);
            
            if (File.Exists(iconPath))
            {
                var iconBytes = File.ReadAllBytes(iconPath);
                using var iconStream = new MemoryStream(iconBytes);
                _trayIcon.Icon = new WindowIcon(iconStream);
                Log.Information("Tray icon loaded successfully from file");
            }
            else
            {
                Log.Warning("Icon file not found at: {IconPath}", iconPath);
                
                // Try to use the main window's icon as fallback
                if (_mainWindow.Icon != null)
                {
                    _trayIcon.Icon = _mainWindow.Icon;
                    Log.Information("Using main window icon for tray");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load tray icon image");
        }

        var menu = new NativeMenu();

        var showMenuItem = new NativeMenuItem("Show")
        {
            IsEnabled = true
        };
        showMenuItem.Click += (sender, args) => ShowWindow();

        var exitMenuItem = new NativeMenuItem("Exit")
        {
            IsEnabled = true
        };
        exitMenuItem.Click += (sender, args) => ExitApplication();

        menu.Add(showMenuItem);
        menu.Add(exitMenuItem);

        _trayIcon.Menu = menu;
        _trayIcon.Clicked += (sender, args) => ShowWindow();
        
        // Set visible AFTER everything is configured
        _trayIcon.IsVisible = true;

        Log.Information("Tray icon initialized and set to visible");
    }

    public void ShowWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            Log.Information("Window restored from tray");
        }
    }

    public void HideWindow()
    {
        if (_mainWindow != null && !_isExiting)
        {
            _mainWindow.Hide();
            Log.Information("Window minimized to tray");
        }
    }

    public void ExitApplication()
    {
        _isExiting = true;
        
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }

        Log.Information("Application exiting");
    }

    public void Dispose()
    {
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
