using System;
using Avalonia.Controls;
using RecuperationSystem.Desktop.ViewModels.Cards;
using RecuperationSystem.Shared;

namespace RecuperationSystem.Desktop.Views.Controls;

public partial class OperatingModeCard : UserControl
{
    private bool _isUpdatingModeSelection;

    public OperatingModeCard()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is OperatingModeCardViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateModeSelection();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperatingModeCardViewModel.SelectedMode))
        {
            UpdateModeSelection();
        }
    }

    private void UpdateModeSelection()
    {
        if (DataContext is not OperatingModeCardViewModel viewModel) return;

        _isUpdatingModeSelection = true;
        try
        {
            var selectedMode = viewModel.SelectedMode;
            if (selectedMode == AppConstants.ModeIntelligent)
            {
                ModeComboBox.SelectedItem = IntelligentModeItem;
            }
            else if (selectedMode == AppConstants.ModeManual)
            {
                ModeComboBox.SelectedItem = ManualModeItem;
            }
            else if (selectedMode == AppConstants.ModeSchedule)
            {
                ModeComboBox.SelectedItem = ScheduleModeItem;
            }
        }
        finally
        {
            _isUpdatingModeSelection = false;
        }
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingModeSelection) return;
        if (DataContext is not OperatingModeCardViewModel viewModel) return;
        
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item)
        {
            var mode = item.Tag?.ToString() ?? AppConstants.ModeIntelligent;
            viewModel.SelectedMode = mode;
            _ = viewModel.UpdateModeCommand.Execute();
        }
    }
}
