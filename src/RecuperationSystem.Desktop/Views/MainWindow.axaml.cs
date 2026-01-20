using System;
using Avalonia.Controls;
using Avalonia.Input;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Desktop.ViewModels;

namespace RecuperationSystem.Desktop.Views;

public partial class MainWindow : Window
{
    private AppViewModel ViewModel => (AppViewModel)DataContext!;
    public ITrayIconService? TrayIconService { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        
        // Handle window state changes to minimize to tray
        PropertyChanged += (sender, e) =>
        {
            if (e.Property.Name == nameof(WindowState))
            {
                if (WindowState == WindowState.Minimized && TrayIconService != null)
                {
                    TrayIconService.HideWindow();
                }
            }
        };

        // Handle window closing to minimize to tray instead
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (TrayIconService != null)
        {
            e.Cancel = true;
            TrayIconService.HideWindow();
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClick(object? sender, EventArgs e)
    {
        if (TrayIconService != null)
        {
            TrayIconService.HideWindow();
        }
        else
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void OnCloseClick(object? sender, EventArgs e)
    {
        Close();
    }
}
