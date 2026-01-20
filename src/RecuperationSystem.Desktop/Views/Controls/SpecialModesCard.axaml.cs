using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RecuperationSystem.Desktop.Views.Controls;

public partial class SpecialModesCard : UserControl
{
    public SpecialModesCard()
    {
        InitializeComponent();
    }

    private void OnSilentModeToggled(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Cards.SpecialModesCardViewModel viewModel && sender is ToggleSwitch toggle)
        {
            _ = viewModel.SetSilentModeCommand.Execute(toggle.IsChecked ?? false);
        }
    }

    private void OnHolidayModeToggled(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.Cards.SpecialModesCardViewModel viewModel && sender is ToggleSwitch toggle)
        {
            _ = viewModel.SetHolidayModeCommand.Execute(toggle.IsChecked ?? false);
        }
    }
}
