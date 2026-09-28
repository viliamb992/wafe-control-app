using System.ComponentModel;
using System.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;
using WafeControl.Core.ViewModels.Cards;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Mobile.Controls;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Views;

/// <summary>
/// Dashboard. Most state flows through bindings; the code-behind covers controls whose user changes must be told
/// apart from updates coming from the unit (mode selector, flow slider, switches), the stop confirmation, the
/// boost countdown between polls and the data banner's look.
/// </summary>
public partial class DashboardPage : ContentPage
{
    private const int HapticStep = 10;

    private readonly AppViewModel _app;
    private readonly ScheduleViewModel _schedule;
    private readonly IDispatcherTimer _countdown;
    private readonly Stopwatch _sinceBoostUpdate = new();
    private bool _syncing;
    private int _lastHapticStep;

    public DashboardPage(AppViewModel app, ScheduleViewModel schedule, ReleaseCheckViewModel releases)
    {
        _app = app;
        _schedule = schedule;
        InitializeComponent();
        BindingContext = app;
        UpdateBanner.BindingContext = releases;

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
        _app.PropertyChanged += OnAppPropertyChanged;
        OperatingMode.PropertyChanged += OnOperatingModePropertyChanged;
        FlowSpeed.PropertyChanged += OnFlowSpeedPropertyChanged;
        _app.SystemControl.PropertyChanged += OnSystemControlPropertyChanged;
        _app.BoostMode.PropertyChanged += OnBoostPropertyChanged;
        _schedule.PropertyChanged += OnSchedulePropertyChanged;
        Toast.Attach(_app);

        SyncModeItems();
        SyncFlowSlider();
        OnBoostPropertyChanged(null, new PropertyChangedEventArgs(nameof(BoostModeCardViewModel.BoostRemaining)));
        UpdateNextStart();
        UpdateDataBanner();
        SafeAsync.Run(() => _schedule.TrackUnitModeAsync(_app.SystemControl.CurrentAuthority));
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _app.PropertyChanged -= OnAppPropertyChanged;
        OperatingMode.PropertyChanged -= OnOperatingModePropertyChanged;
        FlowSpeed.PropertyChanged -= OnFlowSpeedPropertyChanged;
        _app.SystemControl.PropertyChanged -= OnSystemControlPropertyChanged;
        _app.BoostMode.PropertyChanged -= OnBoostPropertyChanged;
        _schedule.PropertyChanged -= OnSchedulePropertyChanged;
        Toast.Detach();
        _countdown.Stop();
    }

    // ── Refresh and data state ───────────────────────────────────────────

    // Pull to refresh; a failure shows in the toast.
    private void OnRefreshing(object? sender, EventArgs e) => SafeAsync.Run(async () =>
    {
        try
        {
            await _app.RefreshStatusCommand.ExecuteAsync(null);
        }
        finally
        {
            Refresh.IsRefreshing = false;
        }
    });

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppViewModel.DataState))
            Dispatcher.Dispatch(UpdateDataBanner);
    }

    // The unit gone is an error; old data or no connection a warning, each with its own icon.
    private void UpdateDataBanner()
    {
        DataBanner.Severity = _app.IsDataBannerError ? BannerSeverity.Error : BannerSeverity.Warning;
        DataBanner.Icon = _app.DataState switch
        {
            DataState.NoInternet => FluentIcons.WifiOff,
            DataState.UnitOffline or DataState.ServerUnreachable => FluentIcons.CloudOff,
            _ => null,
        };
    }

    // ── Power ────────────────────────────────────────────────────────────

    private void OnPowerClicked(object? sender, EventArgs e) => SafeAsync.Run(async () =>
    {
        var system = _app.SystemControl;
        if (system.IsSystemRunning
            && !await DisplayAlertAsync(Strings.SystemStopConfirmTitle, Strings.SystemStopConfirmMessage, Strings.SystemStopConfirm, Strings.ButtonCancel))
            return;

        if (system.ToggleSystemCommand.CanExecute(null))
            await system.ToggleSystemCommand.ExecuteAsync(null);
    });

    // ── Next scheduled start ─────────────────────────────────────────────

    // CurrentAuthority is raised on every status update, which also moves the next start along as time passes.
    private void OnSystemControlPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemControlCardViewModel.CurrentAuthority))
        {
            SafeAsync.Run(() => _schedule.TrackUnitModeAsync(_app.SystemControl.CurrentAuthority));
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

    private void OnScheduleClicked(object? sender, EventArgs e) => SafeAsync.Run(() => Shell.Current.GoToAsync("//main/schedule"));
}
