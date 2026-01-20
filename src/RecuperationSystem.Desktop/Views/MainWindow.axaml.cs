using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        // password reveal is handled via PointerPressed/PointerReleased on the reveal button in XAML
        
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

    private void OnMaximizeRestoreClick(object? sender, EventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void SetPasswordReveal(bool reveal)
    {
        var passwordBox = this.FindControl<TextBox>("InlinePasswordBox");
        if (passwordBox is null)
        {
            return;
        }

        passwordBox.PasswordChar = reveal ? '\0' : '•';
    }

    private void OnRevealPasswordPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control c)
        {
            e.Pointer.Capture(c);
        }

        SetPasswordReveal(true);
    }

    private void OnRevealPasswordReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        SetPasswordReveal(false);
    }

    private void OnRevealPasswordCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        => SetPasswordReveal(false);

    private void OnRevealPasswordExited(object? sender, PointerEventArgs e)
        => SetPasswordReveal(false);
}
