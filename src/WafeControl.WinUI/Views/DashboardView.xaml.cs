using System.ComponentModel;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;
using WafeControl.Core.ViewModels.Cards;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.WinUI.Helpers;
using Windows.System;

namespace WafeControl.WinUI.Views;

/// <summary>
/// Dashboard cards. Most state flows through x:Bind; the code-behind covers controls whose user
/// changes must be told apart from updates coming from the unit (mode selector, flow slider, toggles).
/// </summary>
public sealed partial class DashboardView : UserControl
{
    private static readonly VirtualKey[] SliderKeys =
        [VirtualKey.Left, VirtualKey.Right, VirtualKey.Up, VirtualKey.Down, VirtualKey.Home, VirtualKey.End, VirtualKey.PageUp, VirtualKey.PageDown];

    private bool _initialized;
    private bool _syncing;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Set by the window before the view loads.
    /// </summary>
    public AppViewModel ViewModel { get; set; } = null!;

    /// <summary>
    /// Opened from the Ventilation card. Set by the window before the view loads.
    /// </summary>
    public ScheduleViewModel Schedule { get; set; } = null!;

    private OperatingModeCardViewModel OperatingMode => ViewModel.OperatingMode;

    /// <summary>
    /// Re-reads the text after the app language changed, including the mode names built in code.
    /// </summary>
    public void RefreshText()
    {
        Bindings.Update();
        if (_initialized)
            SyncModeItems();
    }

    private FlowSpeedCardViewModel FlowSpeed => ViewModel.FlowSpeed;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;
        _initialized = true;

        OperatingMode.PropertyChanged += OnOperatingModePropertyChanged;
        FlowSpeed.PropertyChanged += OnFlowSpeedPropertyChanged;
        ViewModel.SystemControl.PropertyChanged += OnSystemControlPropertyChanged;

        // The slider handles pointer and keys itself, so listen to handled events too:
        // a gesture (drag, track click, key presses) counts as "dragging" until it ends.
        FlowSlider.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => FlowSpeed.IsDragging = true), true);
        FlowSlider.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => FlowSpeed.IsDragging = false), true);
        FlowSlider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler((_, _) => FlowSpeed.IsDragging = false), true);
        FlowSlider.AddHandler(KeyDownEvent, new KeyEventHandler(OnFlowSliderKey(dragging: true)), true);
        FlowSlider.AddHandler(KeyUpEvent, new KeyEventHandler(OnFlowSliderKey(dragging: false)), true);

        SyncModeItems();
        SyncFlowSlider();
        SafeAsync.Run(() => Schedule.TrackUnitModeAsync(ViewModel.SystemControl.CurrentAuthority));
    }

    // ── Power ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starting needs no question; stopping ends ventilation until someone starts it again, so it asks first.
    /// </summary>
    private void OnPowerClick(object sender, RoutedEventArgs e) => SafeAsync.Run(async () =>
    {
        var system = ViewModel.SystemControl;
        if (system.IsSystemRunning)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                // Dialogs open outside the window's content, so they don't inherit a theme chosen in Settings.
                RequestedTheme = ActualTheme,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = Strings.SystemStopConfirmTitle,
                Content = Strings.SystemStopConfirmMessage,
                PrimaryButtonText = Strings.SystemStopConfirm,
                CloseButtonText = Strings.ButtonCancel,
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        if (system.ToggleSystemCommand.CanExecute(null))
            await system.ToggleSystemCommand.ExecuteAsync(null);
    });

    // ── Next scheduled start ─────────────────────────────────────────────

    // CurrentAuthority is raised on every status update, which also moves the next start along as time passes.
    private void OnSystemControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemControlCardViewModel.CurrentAuthority))
            SafeAsync.Run(() => Schedule.TrackUnitModeAsync(ViewModel.SystemControl.CurrentAuthority));
    }

    // ── Operating mode ───────────────────────────────────────────────────

    private void OnOperatingModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperatingModeCardViewModel.AvailableModes))
            SyncModeItems();
        else if (e.PropertyName == nameof(OperatingModeCardViewModel.SelectedMode))
            SyncModeSelection();
    }

    private void SyncModeItems()
    {
        _syncing = true;
        ModeSelector.Items.Clear();
        foreach (var mode in OperatingMode.AvailableModes)
            ModeSelector.Items.Add(new SegmentedItem { Content = Xaml.ModeName(mode), Tag = mode });
        _syncing = false;

        SyncModeSelection();
    }

    private void SyncModeSelection()
    {
        _syncing = true;
        ModeSelector.SelectedIndex = OperatingMode.AvailableModes
            .Select((mode, index) => (mode, index))
            .FirstOrDefault(m => m.mode == OperatingMode.SelectedMode, (mode: "", index: -1)).index;
        _syncing = false;
    }

    private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _syncing || ModeSelector.SelectedItem is not SegmentedItem { Tag: string mode } || mode == OperatingMode.SelectedMode)
            return;

        if (OperatingMode.UpdateModeCommand.CanExecute(null))
            SafeAsync.Run(() => OperatingMode.ChangeModeAsync(mode));
        else
            SyncModeSelection();
    }

    // ── Flow speed ───────────────────────────────────────────────────────

    private void OnFlowSpeedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowSpeedCardViewModel.FlowSpeed))
            SyncFlowSlider();
    }

    private void SyncFlowSlider()
    {
        _syncing = true;
        FlowSlider.Value = FlowSpeed.FlowSpeed;
        _syncing = false;
    }

    private void OnFlowSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Also raised while XAML initializes (Minimum coerces Value), before the view model is attached.
        if (_initialized && !_syncing)
            FlowSpeed.FlowSpeed = (int)Math.Round(e.NewValue);
    }

    private KeyEventHandler OnFlowSliderKey(bool dragging) => (_, e) =>
    {
        if (SliderKeys.Contains(e.Key))
            FlowSpeed.IsDragging = dragging;
    };

    // ── Silent / holiday ─────────────────────────────────────────────────

    private void OnSilentToggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
            return;

        // Updates from the unit also toggle the switch; only act when it differs from the known state.
        var modes = ViewModel.SpecialModes;
        if (SilentToggle.IsOn != modes.IsSilentMode && modes.SetSilentModeCommand.CanExecute(SilentToggle.IsOn))
            modes.SetSilentModeCommand.Execute(SilentToggle.IsOn);
    }

    private void OnHolidayToggled(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
            return;

        var modes = ViewModel.SpecialModes;
        if (HolidayToggle.IsOn != modes.IsHolidayMode && modes.SetHolidayModeCommand.CanExecute(HolidayToggle.IsOn))
            modes.SetHolidayModeCommand.Execute(HolidayToggle.IsOn);
    }
}
