using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WafeControl.Core.Configuration;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels.Cards;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Core.ViewModels;

/// <summary>
/// Application-level ViewModel coordinating card ViewModels and managing shared state.
/// Create it on the UI thread: events raised on background threads are marshalled back to it.
/// </summary>
public sealed partial class AppViewModel : ObservableObject, IDisposable, IAppContext
{
    private static readonly TimeSpan DataStateInterval = TimeSpan.FromSeconds(30);

    private readonly IAuthenticationService _authService;
    private readonly ISystemControlService _systemControl;
    private readonly PollingConfiguration _pollingConfig;
    private readonly INetworkStatus _network;
    private readonly TimeProvider _time;
    private readonly ILogger<AppViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly ITimer _dataStateTimer;
    private CancellationTokenSource? _autoRefreshCts;
    private CancellationTokenSource? _feedbackClearCts;
    private bool _isLoadingUnit;
    private bool _isPaused;
    private bool _disposed;

    public AppViewModel(
        IAuthenticationService authService,
        ISystemControlService systemControl,
        IOptions<PollingConfiguration> pollingConfig,
        ILoggerFactory loggerFactory,
        INetworkStatus? network = null,
        TimeProvider? timeProvider = null)
    {
        _authService = authService;
        _systemControl = systemControl;
        _pollingConfig = pollingConfig.Value;
        _network = network ?? new SystemNetworkStatus();
        _time = timeProvider ?? TimeProvider.System;
        _logger = loggerFactory.CreateLogger<AppViewModel>();
        _uiContext = SynchronizationContext.Current;

        StatusMessage = Strings.AppStatusNotConnected;

        Login = new LoginViewModel();
        Login.SubmitRequested += OnLoginSubmitRequested;
        Login.CancelRequested += (_, _) => CancelLogin();

        // Initialize card ViewModels
        SystemControl = new SystemControlCardViewModel(systemControl, this, loggerFactory.CreateLogger<SystemControlCardViewModel>());
        OperatingMode = new OperatingModeCardViewModel(systemControl, this, loggerFactory.CreateLogger<OperatingModeCardViewModel>());
        FlowSpeed = new FlowSpeedCardViewModel(systemControl, this, loggerFactory.CreateLogger<FlowSpeedCardViewModel>());
        SpecialModes = new SpecialModesCardViewModel(systemControl, this, loggerFactory.CreateLogger<SpecialModesCardViewModel>());
        BoostMode = new BoostModeCardViewModel(systemControl, this, loggerFactory.CreateLogger<BoostModeCardViewModel>());

        // Subscribe to events
        _authService.AuthenticationChanged += OnAuthenticationChanged;
        _systemControl.StatusUpdated += OnStatusUpdated;
        _network.Changed += OnNetworkChanged;
        OperatingMode.PropertyChanged += OnOperatingModePropertyChanged;

        // "Data 5 min old" moves on even when nothing arrives.
        _dataStateTimer = _time.CreateTimer(_ => RunOnUiThread(UpdateDataState), null, DataStateInterval, DataStateInterval);

        _logger.LogInformation("AppViewModel initialized with polling config: StatusRefresh={StatusRefresh}ms",
            _pollingConfig.StatusRefreshIntervalMs);

        // Login flow is initiated from App startup to avoid hitting API before credentials are available.
    }

    #region Card ViewModels

    public SystemControlCardViewModel SystemControl { get; }
    public OperatingModeCardViewModel OperatingMode { get; }
    public FlowSpeedCardViewModel FlowSpeed { get; }
    public SpecialModesCardViewModel SpecialModes { get; }
    public BoostModeCardViewModel BoostMode { get; }

    #endregion

    #region Properties

    /// <summary>
    /// Connection progress while signing in ("Connecting to Wafe…"); command results go to <see cref="Feedback"/>.
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    /// <summary>
    /// The result of the last user action. Successes clear themselves after a few seconds, warnings and errors a
    /// little later; progress stays until the result replaces it.
    /// </summary>
    [ObservableProperty]
    public partial Feedback? Feedback { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitLoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(TryDemoCommand))]
    public partial bool IsLoggingIn { get; private set; }

    public bool IsAuthenticated => _authService.IsAuthenticated;

    /// <summary>
    /// Signed in to the simulated demo unit.
    /// </summary>
    public bool IsDemo => _authService.IsAuthenticated && _authService.IsDemo;

    /// <summary>
    /// True while <see cref="StartAsync"/> tries the remembered login; show neither the login form nor the dashboard.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoginRequired))]
    public partial bool IsStarting { get; private set; }

    public LoginViewModel Login { get; }

    /// <summary>
    /// Who is signed in, for the account row: the email, or "Demo mode".
    /// </summary>
    public string AccountName => !IsAuthenticated ? string.Empty : IsDemo ? Strings.DemoBannerTitle : _authService.Username;

    public bool IsLoginRequired => !IsAuthenticated && !IsStarting;

    // Expose properties from card VMs for convenience and bindings
    public bool IsManualMode => OperatingMode.IsManualMode;
    public bool IsSystemOnline => _systemControl.IsSystemOnline;
    public bool HasSensorData => _systemControl.HasSensorData;

    /// <summary>
    /// The unit's name from the Wafe portal without its serial number, e.g. "byt 1.001"; empty when unknown.
    /// </summary>
    public string UnitName => UnitNames.WithoutSerialNumber(PortalUnitName);

    /// <summary>
    /// The unit's name as stored in the Wafe portal, serial number included; empty when unknown.
    /// </summary>
    public string PortalUnitName => IsAuthenticated ? _systemControl.CurrentHeader?.Name ?? string.Empty : string.Empty;

    /// <summary>
    /// Time of the unit's latest data (local); null until the first header arrives.
    /// </summary>
    public DateTimeOffset? LastUpdate =>
        IsAuthenticated && _systemControl.CurrentHeader is { Timestamp: > 0 } header ? header.Time.ToLocalTime() : null;

    /// <summary>
    /// "Last update: 14:32:05", or empty until the unit's data time is known.
    /// </summary>
    public string LastUpdateText => DisplayFormat.LastUpdate(LastUpdate);

    /// <summary>
    /// Model, serial number and service contact. Loaded once per sign-in; null until then.
    /// </summary>
    [ObservableProperty]
    public partial SystemInfo? Unit { get; private set; }

    // Temperatures in °C; null when the unit doesn't report them (e.g. while it is offline).
    public double? OutsideTemp => GetTemperature(0);
    public double? FreshTemp => GetTemperature(1);
    public double? IndoorTemp => GetTemperature(2);
    public double? WasteTemp => GetTemperature(3);

    private double? GetTemperature(int index)
    {
        var temps = _systemControl.CurrentStatus?.Temperatures;
        return temps is not null && temps.Count > index ? temps[index] : null;
    }

    public int Co2Level => _systemControl.CurrentStatus?.Co2 ?? 0;
    public int CurrentFlow => _systemControl.CurrentStatus?.FlowCurrent ?? 0;
    public double? Humidity => _systemControl.CurrentStatus?.Humidity;

    // Filter health in percent; null when the unit doesn't report it.
    public int? FreshFilterHealth => _systemControl.CurrentStatus?.Filters?.Fresh?.Health;
    public int? WasteFilterHealth => _systemControl.CurrentStatus?.Filters?.Waste?.Health;

    #endregion

    #region Data state

    /// <summary>
    /// How current the readings are.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSendCommands), nameof(IsDataCurrent))]
    public partial DataState DataState { get; private set; }

    /// <summary>
    /// Signed in and the server reachable: a command can get through.
    /// </summary>
    public bool CanSendCommands => IsAuthenticated && DataState is not (DataState.ServerUnreachable or DataState.NoInternet);

    /// <summary>
    /// The readings on screen are the unit's current ones; otherwise show them dimmed.
    /// </summary>
    public bool IsDataCurrent => DataState is DataState.Live or DataState.UnitOffline;

    /// <summary>
    /// Short text for the connection indicator: "Online", "Offline", "Data 6 min old", "No connection", "Demo".
    /// </summary>
    public string DataStateText => DataState switch
    {
        DataState.Stale => string.Format(Strings.DataStaleShort, DataAgeMinutes),
        DataState.UnitOffline => Strings.TitleBarOffline,
        DataState.ServerUnreachable => Strings.DataNoConnectionShort,
        DataState.NoInternet => Strings.DataNoInternetShort,
        _ => IsDemo ? Strings.DemoSubtitle : DisplayFormat.Online(IsSystemOnline),
    };

    /// <summary>
    /// Banner title while the data isn't live; null when there's nothing to say.
    /// </summary>
    public string? DataBannerTitle => DataState switch
    {
        DataState.Stale => Strings.BannerStaleTitle,
        DataState.UnitOffline => Strings.AppStatusUnitOffline,
        DataState.ServerUnreachable => Strings.BannerUnreachableTitle,
        DataState.NoInternet => Strings.BannerNoInternetTitle,
        _ => null,
    };

    public string? DataBannerMessage => DataState switch
    {
        DataState.Stale => string.Format(Strings.BannerStaleMessage, DataAgeMinutes),
        DataState.UnitOffline => Strings.BannerUnitOfflineMessage,
        DataState.ServerUnreachable => string.Format(Strings.BannerUnreachableMessage, DataTimeText),
        DataState.NoInternet => string.Format(Strings.BannerNoInternetMessage, DataTimeText),
        _ => null,
    };

    /// <summary>
    /// The banner is an error (the unit is gone), not a warning (the data is old).
    /// </summary>
    public bool IsDataBannerError => DataState == DataState.UnitOffline;

    private int DataAgeMinutes => LastUpdate is { } time ? Math.Max(1, (int)(_time.GetUtcNow() - time).TotalMinutes) : 0;

    // "14:02" today, with the date otherwise.
    private string DataTimeText => LastUpdate is { } time
        ? time.LocalDateTime.Date == _time.GetLocalNow().Date
            ? time.LocalDateTime.ToString("HH:mm", CultureInfo.CurrentCulture)
            : ScheduleFormat.DateAndTime(time.LocalDateTime)
        : ModeNames.NoValue;

    private DataState ComputeDataState()
    {
        if (!IsAuthenticated)
            return DataState.Live;

        var failures = _systemControl.ConsecutiveRefreshFailures;
        if (!_network.IsAvailable && (failures > 0 || _systemControl.CurrentStatus is null))
            return DataState.NoInternet;
        if (failures >= _pollingConfig.UnreachableAfterFailures)
            return DataState.ServerUnreachable;
        if (_systemControl.CurrentHeader is { Online: false })
            return DataState.UnitOffline;
        if (LastUpdate is { } time && _time.GetUtcNow() - time > TimeSpan.FromSeconds(_pollingConfig.StaleAfterSeconds))
            return DataState.Stale;
        return DataState.Live;
    }

    private void UpdateDataState()
    {
        if (_disposed)
            return;

        DataState = ComputeDataState();

        // The texts also depend on the time and the unit, so raise them every time.
        OnPropertyChanged(nameof(DataStateText));
        OnPropertyChanged(nameof(DataBannerTitle));
        OnPropertyChanged(nameof(DataBannerMessage));
        OnPropertyChanged(nameof(IsDataBannerError));
    }

    #endregion

    #region Commands

    /// <summary>
    /// Signs in with remembered credentials, if any. Call once when the UI is ready.
    /// </summary>
    public async Task StartAsync()
    {
        IsStarting = true;
        StatusMessage = Strings.AppStatusConnecting;
        try
        {
            var result = await _authService.TryAutoLoginAsync();
            if (result is { Ok: false } failed)
            {
                // The form opens with the reason, and the email to try again with.
                Login.Username = _authService.Username;
                Login.ErrorMessage = ErrorText.For(failed.Error);
            }

            if (result is not { Ok: true })
                StatusMessage = Strings.AppStatusSignInPrompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-login failed");
            StatusMessage = Strings.AppStatusConnectionFailed;
        }
        finally
        {
            IsStarting = false;
        }
    }

    /// <summary>
    /// Signs out and forgets the remembered login; in demo mode, just leaves the demo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private async Task SignOutAsync()
    {
        if (!_authService.IsDemo)
            await _authService.ClearRememberedLoginAsync();

        _authService.Logout();
        Login.Password = string.Empty;
    }

    /// <summary>
    /// Opens the demo unit instead of signing in.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTryDemo))]
    private void TryDemo()
    {
        Login.ErrorMessage = null;
        _authService.StartDemo();
    }

    private bool CanTryDemo() => !IsAuthenticated && !IsLoggingIn;

    /// <summary>
    /// Refreshes at once (F5, pull to refresh) and says so if it fails.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    public async Task RefreshStatusAsync()
    {
        var wasBackingOff = _systemControl.ConsecutiveRefreshFailures >= _pollingConfig.UnreachableAfterFailures;

        await RefreshCoreAsync();

        if (_systemControl.LastRefreshError is not ApiError.None and var error)
            Feedback = Feedback.Error(string.Format(Strings.AppStatusRefreshError, ErrorText.For(error)));
        else if (wasBackingOff && !_isPaused)
            StartAutoRefresh();
    }

    /// <summary>
    /// Stops polling while the app is in the background (mobile). <see cref="ResumeAsync"/> starts it again.
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
        StopAutoRefresh();
    }

    /// <summary>
    /// Back in the foreground: refreshes at once and polls again. Does nothing unless <see cref="Pause"/> was called.
    /// </summary>
    public async Task ResumeAsync()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        if (!IsAuthenticated)
            return;

        StartAutoRefresh();
        await RefreshCoreAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSubmitLogin))]
    private async Task SubmitLoginAsync()
    {
        if (IsLoggingIn) return;

        // A pasted address often brings a space along.
        Login.Username = Login.Username.Trim();
        if (string.IsNullOrWhiteSpace(Login.Username) || string.IsNullOrEmpty(Login.Password))
        {
            Login.ErrorMessage = Strings.LoginCredentialsRequired;
            return;
        }

        try
        {
            IsLoggingIn = true;
            Login.ErrorMessage = null;
            StatusMessage = Strings.AppStatusSigningIn;

            var result = await _authService.LoginAsync(Login.Username, Login.Password, Login.RememberMe);
            if (!result.Ok)
            {
                Login.ErrorMessage = result.Error == ApiError.Unauthorized ? Strings.LoginInvalidCredentials : ErrorText.For(result.Error);
                StatusMessage = Strings.AppStatusSignInPrompt;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign in failed");
            Login.ErrorMessage = Strings.ErrorUnexpected;
            StatusMessage = Strings.AppStatusSignInPrompt;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    private bool CanSubmitLogin() => !IsLoggingIn && !IsAuthenticated;

    [RelayCommand(CanExecute = nameof(CanCancelLogin))]
    private void CancelLogin()
    {
        Login.Password = string.Empty;
        Login.ErrorMessage = null;
        StatusMessage = Strings.AppStatusSignInPrompt;
    }

    private bool CanCancelLogin() => !IsLoggingIn;

    /// <summary>
    /// A rename dialog starting from the current portal name.
    /// </summary>
    public UnitNameEditorViewModel EditUnitName() => new(PortalUnitName);

    /// <summary>
    /// Sends the new name; on failure the editor shows why, so the dialog can stay open.
    /// </summary>
    public async Task<bool> SaveUnitNameAsync(UnitNameEditorViewModel editor)
    {
        if (!IsAuthenticated || !editor.CanSave)
            return false;

        editor.ErrorMessage = null;
        ApiResult result;
        try
        {
            result = await _systemControl.SetUnitNameAsync(editor.NewName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unit rename failed");
            result = ApiResult.Fail(ApiError.Unexpected);
        }

        if (!result.Ok)
            editor.ErrorMessage = string.Format(Strings.UnitNameSaveFailed, ErrorText.For(result.Error));

        return result.Ok;
    }

    #endregion

    #region Event Handlers

    private void OnLoginSubmitRequested(object? sender, EventArgs e) =>
        SubmitLoginCommand.ExecuteAsync(null).Forget(_logger, "Sign in");

    private void OnAuthenticationChanged(object? sender, bool isAuthenticated)
        => RunOnUiThread(() =>
        {
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsDemo));
            OnPropertyChanged(nameof(AccountName));
            OnPropertyChanged(nameof(IsLoginRequired));
            OnPropertyChanged(nameof(CanSendCommands));
            OnPropertyChanged(nameof(UnitName));
            OnPropertyChanged(nameof(PortalUnitName));
            OnPropertyChanged(nameof(LastUpdate));
            OnPropertyChanged(nameof(LastUpdateText));
            RefreshStatusCommand.NotifyCanExecuteChanged();
            SubmitLoginCommand.NotifyCanExecuteChanged();
            SignOutCommand.NotifyCanExecuteChanged();
            TryDemoCommand.NotifyCanExecuteChanged();
            CrashReporting.SetScope(IsDemo, unitModel: null);

            if (isAuthenticated)
            {
                StatusMessage = Strings.AppStatusConnected;
                Login.ErrorMessage = null;
                RefreshCoreAsync().Forget(_logger, "Status refresh");

                // Signed in while in the background (remembered login): polling starts on resume.
                if (!_isPaused)
                    StartAutoRefresh();
            }
            else
            {
                StopAutoRefresh();
                Unit = null;
                Feedback = null;
                StatusMessage = Strings.AppStatusDisconnected;
            }

            UpdateDataState();
        });

    private void OnStatusUpdated(object? sender, SystemStatus status)
    {
        OnPropertyChanged(nameof(OutsideTemp));
        OnPropertyChanged(nameof(FreshTemp));
        OnPropertyChanged(nameof(IndoorTemp));
        OnPropertyChanged(nameof(WasteTemp));
        OnPropertyChanged(nameof(Co2Level));
        OnPropertyChanged(nameof(CurrentFlow));
        OnPropertyChanged(nameof(Humidity));
        OnPropertyChanged(nameof(FreshFilterHealth));
        OnPropertyChanged(nameof(WasteFilterHealth));
        OnPropertyChanged(nameof(HasSensorData));
        OnPropertyChanged(nameof(UnitName));
        OnPropertyChanged(nameof(PortalUnitName));
        OnPropertyChanged(nameof(LastUpdate));
        OnPropertyChanged(nameof(LastUpdateText));
        OnPropertyChanged(nameof(IsSystemOnline));

        StatusMessage = _systemControl.IsSystemOnline
            ? Strings.AppStatusUnitOnline
            : Strings.AppStatusUnitOffline;
        UpdateDataState();

        // Also retries after a failed load, on the next status update.
        if (Unit is null)
            LoadUnitAsync().Forget(_logger, "Loading unit info");
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => RunOnUiThread(UpdateDataState);

    private async Task LoadUnitAsync()
    {
        if (_isLoadingUnit) return;

        _isLoadingUnit = true;
        try
        {
            var unit = await _systemControl.GetSystemInfoAsync();
            if (IsAuthenticated)
            {
                Unit = unit;
                CrashReporting.SetScope(IsDemo, unit?.Unit?.Model);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading unit info failed");
        }
        finally
        {
            _isLoadingUnit = false;
        }
    }

    private void OnOperatingModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperatingModeCardViewModel.IsManualMode))
            OnPropertyChanged(nameof(IsManualMode));
    }

    partial void OnFeedbackChanged(Feedback? value)
    {
        _feedbackClearCts?.Cancel();
        _feedbackClearCts?.Dispose();
        _feedbackClearCts = null;
        if (value is null || value.Kind == FeedbackKind.Progress)
            return;

        var delay = value.Kind == FeedbackKind.Success ? TimeSpan.FromSeconds(6)
            : value.HasAction ? TimeSpan.FromSeconds(30)
            : TimeSpan.FromSeconds(15);
        _feedbackClearCts = new CancellationTokenSource();
        ClearFeedbackLaterAsync(value, delay, _feedbackClearCts.Token).Forget(_logger, "Clearing feedback");
    }

    private async Task ClearFeedbackLaterAsync(Feedback feedback, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _time, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (ReferenceEquals(Feedback, feedback))
                Feedback = null;
        });
    }

    #endregion

    #region Polling

    private async Task RefreshCoreAsync()
    {
        try
        {
            await _systemControl.RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing status");
        }

        RunOnUiThread(UpdateDataState);
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        _autoRefreshCts = new CancellationTokenSource();
        AutoRefreshLoopAsync(_autoRefreshCts.Token).Forget(_logger, "Auto-refresh");
    }

    private async Task AutoRefreshLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(NextPollDelay(), _time, token);
                await RefreshCoreAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
    }

    /// <summary>
    /// The regular interval, or longer and longer ones while the server can't be reached.
    /// </summary>
    internal TimeSpan NextPollDelay()
    {
        var beyond = _systemControl.ConsecutiveRefreshFailures - _pollingConfig.UnreachableAfterFailures;
        var backoff = _pollingConfig.UnreachableBackoffMs;
        var ms = beyond >= 0 && backoff.Length > 0 ? backoff[Math.Min(beyond, backoff.Length - 1)] : _pollingConfig.StatusRefreshIntervalMs;
        return TimeSpan.FromMilliseconds(ms);
    }

    private void StopAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = null;
    }

    private void RunOnUiThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        _authService.AuthenticationChanged -= OnAuthenticationChanged;
        _systemControl.StatusUpdated -= OnStatusUpdated;
        _network.Changed -= OnNetworkChanged;
        OperatingMode.PropertyChanged -= OnOperatingModePropertyChanged;

        _dataStateTimer.Dispose();
        StopAutoRefresh();
        _feedbackClearCts?.Cancel();

        SystemControl.Dispose();
        OperatingMode.Dispose();
        FlowSpeed.Dispose();
        SpecialModes.Dispose();
        BoostMode.Dispose();

        _disposed = true;
        _logger.LogInformation("AppViewModel disposed");
    }
}
