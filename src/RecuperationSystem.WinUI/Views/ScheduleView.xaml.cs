using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.ViewModels;
using RecuperationSystem.Core.ViewModels.Schedule;
using RecuperationSystem.Shared;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.WinUI.Controls;
using RecuperationSystem.WinUI.Helpers;

namespace RecuperationSystem.WinUI.Views;

/// <summary>
/// Weekly schedule screen: the grid plus the add/edit dialog.
/// </summary>
public sealed partial class ScheduleView : UserControl
{
    private bool _initialized;

    public ScheduleView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Set by the window before the view loads.
    /// </summary>
    public ScheduleViewModel ViewModel { get; set; } = null!;

    /// <summary>
    /// The dashboard state, for the operating-mode hint. Set by the window before the view loads.
    /// </summary>
    public AppViewModel Main { get; set; } = null!;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;
        _initialized = true;

        WeekGrid.RangeSelected += OnRangeSelected;
        WeekGrid.EntryInvoked += OnEntryInvoked;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        WeekGrid.SetEntries(ViewModel.Entries);
        WeekGrid.CanSelect = ViewModel.CanAddEntry;
        BuildLegend();
    }

    /// <summary>
    /// Re-reads the text after the app language changed, including the grid and legend built in code.
    /// </summary>
    public void RefreshText()
    {
        Bindings.Update();
        if (!_initialized)
            return;

        WeekGrid.RefreshText();
        BuildLegend();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScheduleViewModel.Entries):
                WeekGrid.SetEntries(ViewModel.Entries);
                break;
            case nameof(ScheduleViewModel.CanAddEntry):
                WeekGrid.CanSelect = ViewModel.CanAddEntry;
                break;
            case nameof(ScheduleViewModel.Modes):
                BuildLegend();
                break;
        }
    }

    private void BuildLegend()
    {
        Legend.Children.Clear();
        foreach (var mode in ViewModel.Modes)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            item.Children.Add(new Rectangle
            {
                Width = 12,
                Height = 12,
                RadiusX = 3,
                RadiusY = 3,
                Fill = Xaml.ScheduleModeBrush(mode),
                VerticalAlignment = VerticalAlignment.Center,
            });
            item.Children.Add(new TextBlock
            {
                Text = ScheduleFormat.ModeName(mode),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                VerticalAlignment = VerticalAlignment.Center,
            });
            Legend.Children.Add(item);
        }
    }

    private async void OnRangeSelected(object? sender, ScheduleSlotRange range)
    {
        await ShowEditorAsync(ViewModel.CreateEntry(range.Day, range.StartMinute, range.EndMinute));
        WeekGrid.ClearSelection();
    }

    private async void OnEntryInvoked(object? sender, ScheduleEntry entry) => await ShowEditorAsync(ViewModel.EditEntry(entry));

    private void OnUseScheduleModeClick(object sender, RoutedEventArgs e)
    {
        var operatingMode = Main.OperatingMode;
        if (!operatingMode.UpdateModeCommand.CanExecute(null))
            return;

        operatingMode.SelectedMode = AppConstants.ModeSchedule;
        operatingMode.UpdateModeCommand.Execute(null);
    }

    private async Task ShowEditorAsync(ScheduleEntryEditorViewModel editor)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            // Dialogs open outside the window's content, so they don't inherit a theme chosen in Settings.
            RequestedTheme = ActualTheme,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            Title = editor.IsNew ? Strings.ScheduleAddAction : Strings.ScheduleEditAction,
            Content = new ScheduleEntryEditor(editor),
            PrimaryButtonText = editor.IsNew ? Strings.ButtonAdd : Strings.ButtonSave,
            SecondaryButtonText = editor.IsNew ? string.Empty : Strings.ButtonDelete,
            CloseButtonText = Strings.ButtonCancel,
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = ViewModel.CanEdit,
            IsSecondaryButtonEnabled = ViewModel.CanEdit,
        };

        // Keep the dialog open until the unit accepted the plan; errors show inside the dialog.
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = !await ViewModel.SaveEntryAsync(editor);
            deferral.Complete();
        };
        dialog.SecondaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            args.Cancel = !await ViewModel.DeleteEntryAsync(editor.Original!);
            if (args.Cancel)
                editor.ErrorMessage = ViewModel.ErrorMessage;
            deferral.Complete();
        };

        await dialog.ShowAsync();
    }
}
