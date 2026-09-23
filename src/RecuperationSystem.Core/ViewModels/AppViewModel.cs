using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecuperationSystem.Core.Configuration;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Core.ViewModels.Cards;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Core.ViewModels;

/// <summary>
/// Application-level ViewModel coordinating card ViewModels and managing shared state.
/// Create it on the UI thread: events raised on background threads are marshalled back to it.
/// </summary>
public sealed partial class AppViewModel : ObservableObject, IDisposable, IAppContext
{
    private readonly IAuthenticationService _authService;
    private readonly ISystemControlService _systemControl;
    private readonly PollingConfiguration _pollingConfig;
    private readonly ILogger<AppViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private CancellationTokenSource? _autoRefreshCts;
    private bool _disposed;

    public AppViewModel(
        IAuthenticationService authService,
        ISystemControlService systemControl,
        IOptions<PollingConfiguration> pollingConfig,
        ILoggerFactory loggerFactory)
    {
        _authService = authService;
        _systemControl = systemControl;
        _pollingConfig = pollingConfig.Value;
        _logger = loggerFactory.CreateLogger<AppViewModel>();
        _uiContext = SynchronizationContext.Current;

        StatusMessage = Strings.AppStatusNotConnected;

        Login = new LoginViewModel();
        Login.SubmitRequested += async (_, _) => await SubmitLoginAsync();
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
        OperatingMode.PropertyChanged += OnOperatingModePropertyChanged;

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

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitLoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoginCommand))]
    public partial bool IsLoggingIn { get; private set; }

    public bool IsAuthenticated => _authService.IsAuthenticated;

    /// <summary>
    /// True while <see cref="StartAsync"/> tries the remembered login; show neither the login form nor the dashboard.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoginRequired))]
    public partial bool IsStarting { get; private set; }

    public LoginViewModel Login { get; }

    public bool IsLoginRequired => !IsAuthenticated && !IsStarting;

    // Expose properties from card VMs for convenience and bindings
    public bool IsManualMode => OperatingMode.IsManualMode;
    public bool IsSystemOnline => _systemControl.IsSystemOnline;

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
            if (!await _authService.TryAutoLoginAsync())
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
    /// Signs out and forgets the remembered login.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    private async Task SignOutAsync()
    {
        await _authService.ClearRememberedLoginAsync();
        _authService.Logout();
        Login.Password = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(IsAuthenticated))]
    public async Task RefreshStatusAsync()
    {
        try
        {
            await _systemControl.RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing status");
            StatusMessage = string.Format(Strings.AppStatusRefreshError, ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmitLogin))]
    private async Task SubmitLoginAsync()
    {
        if (IsLoggingIn) return;

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

            var success = await _authService.LoginAsync(Login.Username, Login.Password, Login.RememberMe);
            if (!success)
            {
                Login.ErrorMessage = Strings.LoginInvalidCredentials;
                StatusMessage = Strings.AppStatusSignInPrompt;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign in failed");
            Login.ErrorMessage = Strings.LoginFailed;
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

    #endregion

    #region Event Handlers

    private void OnAuthenticationChanged(object? sender, bool isAuthenticated)
        => RunOnUiThread(() =>
        {
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsLoginRequired));
            RefreshStatusCommand.NotifyCanExecuteChanged();
            SubmitLoginCommand.NotifyCanExecuteChanged();
            SignOutCommand.NotifyCanExecuteChanged();

            if (isAuthenticated)
            {
                StatusMessage = Strings.AppStatusConnected;
                _ = RefreshStatusAsync();
                _ = StartAutoRefreshAsync();
            }
            else
            {
                StopAutoRefresh();
                StatusMessage = Strings.AppStatusDisconnected;
            }
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
        OnPropertyChanged(nameof(IsSystemOnline));

        StatusMessage = _systemControl.IsSystemOnline
            ? Strings.AppStatusUnitOnline
            : Strings.AppStatusUnitOffline;
    }

    private void OnOperatingModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OperatingModeCardViewModel.IsManualMode))
            OnPropertyChanged(nameof(IsManualMode));
    }

    private async Task StartAutoRefreshAsync()
    {
        StopAutoRefresh();
        _autoRefreshCts = new CancellationTokenSource();
        var token = _autoRefreshCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_pollingConfig.StatusRefreshIntervalMs, token);
                await RefreshStatusAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-refresh loop");
        }
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
        OperatingMode.PropertyChanged -= OnOperatingModePropertyChanged;

        StopAutoRefresh();

        SystemControl.Dispose();
        OperatingMode.Dispose();
        FlowSpeed.Dispose();
        SpecialModes.Dispose();
        BoostMode.Dispose();

        _disposed = true;
        _logger.LogInformation("AppViewModel disposed");
    }
}
