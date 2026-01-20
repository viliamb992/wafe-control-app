using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace RecuperationSystem.Desktop.Views.Controls;

public partial class CustomTitleBar : UserControl
{
    public event EventHandler<PointerPressedEventArgs>? HeaderPointerPressed;
    public event EventHandler? MinimizeClicked;
    public event EventHandler? MaximizeRestoreClicked;
    public event EventHandler? CloseClicked;

    public CustomTitleBar()
    {
        InitializeComponent();
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HeaderPointerPressed?.Invoke(this, e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        MinimizeClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        MaximizeRestoreClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        CloseClicked?.Invoke(this, EventArgs.Empty);
    }
}
