using System.ComponentModel;
using System.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels;
using WafeControl.Core.ViewModels.Cards;
using WafeControl.Core.ViewModels.Schedule;

namespace WafeControl.Mobile.Views;

/// <summary>
/// Dashboard. Most state flows through bindings; the code-behind covers controls whose user changes must be told
/// apart from updates coming from the unit (mode selector, flow slider, switches), the stop confirmation, the
/// boost countdown between polls and the network banner.
/// </summary>
public partial class DashboardPage : ContentPage
{
    private const int HapticStep = 10;

    private readonly AppViewModel _app;
    private readonly ScheduleViewModel _schedule;
    private readonly IConnectivity _connectivity;
    private readonly IDispatcherTimer _countdown;
    private readonly Stopwatch _sinceBoostUpdate = new();
    private bool _syncing;
    private int _lastHapticStep;

    public DashboardPage(AppViewModel app, ScheduleViewModel schedule, IConnectivity connectivity)
    {
        _app = app;
        _schedule = schedule;
        _connectivity = connectivity;
        InitializeComponent();
        BindingContext = app;

        _countdown = Dispatcher.CreateTimer();
        _countdown.Interval = TimeSpan.FromSeconds(1);
        _countdown.Tick += (_, _) => UpdateBoost();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private OperatingModeCardViewModel OperatingMode => _app.OperatingMode;

    private FlowSpeedCardViewModel FlowSpeed => _app.FlowSpeed;

    private void OnLoaded(object? sender, EventArgs e)
    {
        OperatingMode.PropertyChanged += OnOperatingModePropertyChanged;
        FlowSpeed.PropertyChanged += OnFlowSpeedPropertyChanged;
        _app.SystemControl.PropertyChanged += OnSystemControlPropertyChanged;
        _app.BoostMode.PropertyChanged += OnBoostPropertyChanged;
        _schedule.PropertyChanged += OnSchedulePropertyChanged;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        Toast.Attach(_app);

        SyncModeItems();
        SyncFlowSlider();
        OnBoostPropertyChanged(null, new PropertyChangedEventArgs(nameof(BoostModeCardViewModel.BoostRemaining)));
        UpdateNextStart();
        UpdateNetworkBanner();
        _ = _schedule.TrackUnitModeAsync(_app.SystemControl.CurrentAuthority);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        OperatingMode.PropertyChanged -= OnOperatingModePropertyChanged;
        FlowSpeed.PropertyChanged -= OnFlowSpeedPropertyChanged;
        _app.SystemControl.PropertyChanged -= OnSystemControlPropertyChanged;
        _app.BoostMode.PropertyChanged -= OnBoostPropertyChanged;
        _schedule.PropertyChanged -= OnSchedulePropertyChanged;
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        Toast.Detach();
        _countdown.Stop();
    }

    // ── Refresh ──────────────────────────────────────────────────────────

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        Toast.ShowRefreshErrors();
        try
        {
            await _app.RefreshStatusAsync();
        }
        finally
        {
            Refresh.IsRefreshing = false;
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) => Dispatcher.Dispatch(UpdateNetworkBanner);

    private void UpdateNetworkBanner() => NoInternetBanner.IsShown = _connectivity.NetworkAccess != NetworkAccess.Internet;

    // ── Power ────────────────────────────────────────────────────────────

    private async void OnPowerClicked(object? sender, EventArgs e)
    {
        var system = _app.SystemControl;
        if (system.IsSystemRunning
            && !await DisplayAlertAsync(Strings.SystemStopConfirmTitle, Strings.SystemStopConfirmMessage, Strings.SystemStopConfirm, Strings.ButtonCancel))
            return;

        if (system.ToggleSystemCommand.CanExecute(null))
            system.ToggleSystemCommand.Execute(null);
    }

    // ── Next scheduled start ─────────────────────────────────────────────

    // CurrentAuthority is raised on every status update, which also moves the next start along as time passes.
    private void OnSystemControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemControlCardViewModel.CurrentAuthority))
        {
            _ = _schedule.TrackUnitModeAsync(_app.SystemControl.CurrentAuthority);
            UpdateNextStart();
        }
    }

    private void OnSchedulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduleViewModel.NextStart))
            UpdateNextStart();
    }

    private void UpdateNextStart()
    {
        var system = _app.SystemControl;
        NextStartPill.IsVisible = DisplayFormat.ShowsNextStart(_schedule.NextStart, system.CurrentAuthority, system.IsSystemRunning);
        NextStartText.Text = DisplayFormat.NextStart(_schedule.NextStart);
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
        ModeSelector.Items = OperatingMode.AvailableModes.Select(ModeNames.Operating).ToArray();
        SyncModeSelection();
    }

    private void SyncModeSelection()
    {
        var modes = OperatingMode.AvailableModes;
        ModeSelector.SelectedIndex = modes.Select((mode, index) => (mode, index))
            .FirstOrDefault(m => m.mode == OperatingMode.SelectedMode, (mode: "", index: -1)).index;
    }

    private void OnModeTapped(object? sender, int index)
    {
        var mode = OperatingMode.AvailableModes[index];
        if (mode == OperatingMode.SelectedMode)
            return;

        OperatingMode.SelectedMode = mode;
        if (OperatingMode.UpdateModeCommand.CanExecute(null))
            OperatingMode.UpdateModeCommand.Execute(null);
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

    private void OnFlowDragStarted(object? sender, EventArgs e)
    {
        _lastHapticStep = FlowSpeed.FlowSpeed / HapticStep;
        FlowSpeed.IsDragging = true;
    }

    // Ending the drag sends the value.
    private void OnFlowDragCompleted(object? sender, EventArgs e) => FlowSpeed.IsDragging = false;

    private void OnFlowValueChanged(object? sender, ValueChangedEventArgs e)
    {
        // Also raised while the XAML loads (Minimum coerces Value), before the view model is in sync.
        if (_syncing || !IsLoaded)
            return;

        var value = (int)Math.Round(e.NewValue);
        FlowSpeed.FlowSpeed = value;

        // A light tick every 10 m³/h while dragging.
        if (FlowSpeed.IsDragging && value / HapticStep != _lastHapticStep)
        {
            _lastHapticStep = value / HapticStep;
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
            catch (FeatureNotSupportedException)
            {
            }
        }
    }

    // ── Boost countdown ──────────────────────────────────────────────────

    // The unit reports the remaining time with each poll; count down locally in between.
    private void OnBoostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BoostModeCardViewModel.BoostRemaining))
            return;

        _sinceBoostUpdate.Restart();
        if (_app.BoostMode.IsBoostActive)
            _countdown.Start();
        UpdateBoost();
    }

    private void UpdateBoost()
    {
        var remaining = Math.Max(0, _app.BoostMode.BoostRemaining - (int)_sinceBoostUpdate.Elapsed.TotalSeconds);
        var countdown = DisplayFormat.Countdown(remaining);
        BoostCountdown.Text = countdown;
        BoostState.Text = DisplayFormat.BoostState(remaining > 0, countdown);
        if (remaining == 0)
            _countdown.Stop();
    }

    // ── Silent / holiday ─────────────────────────────────────────────────

    private void OnSilentToggled(object? sender, ToggledEventArgs e)
    {
        // Updates from the unit also toggle the switch; only act when it differs from the known state.
        var modes = _app.SpecialModes;
        if (e.Value != modes.IsSilentMode && modes.SetSilentModeCommand.CanExecute(e.Value))
            modes.SetSilentModeCommand.Execute(e.Value);
    }

    private void OnHolidayToggled(object? sender, ToggledEventArgs e)
    {
        var modes = _app.SpecialModes;
        if (e.Value != modes.IsHolidayMode && modes.SetHolidayModeCommand.CanExecute(e.Value))
            modes.SetHolidayModeCommand.Execute(e.Value);
    }

    private async void OnScheduleClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//main/schedule");
}
