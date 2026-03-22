using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ReactiveUI;
using RecuperationSystem.Desktop.Configuration;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Desktop.ViewModels.Cards;
using RecuperationSystem.Shared.Models;
using Serilog;

namespace RecuperationSystem.Desktop.ViewModels;

/// <summary>
/// Application-level ViewModel coordinating card ViewModels and managing shared state
/// </summary>
public class AppViewModel : ReactiveObject, IDisposable, IAppContext
{
    private readonly IAuthenticationService _authService;
    private readonly ISystemControlService _systemControl;
    private readonly PollingConfiguration _pollingConfig;
    private CancellationTokenSource? _autoRefreshCts;
    private bool _disposed;

    // Private backing fields
    private string _statusMessage = "Not connected";
    private bool _isLoggingIn;
    private readonly LoginViewModel _login;

    public AppViewModel(
        IAuthenticationService authService,
        ISystemControlService systemControl,
        IOptions<PollingConfiguration> pollingConfig)
    {
        _authService = authService;
        _systemControl = systemControl;
        _pollingConfig = pollingConfig.Value;

        _login = new LoginViewModel();
        _login.SubmitRequested += async (_, _) => await SubmitLoginAsync();
        _login.CancelRequested += async (_, _) => await CancelLoginAsync();

        // Initialize card ViewModels
        SystemControl = new SystemControlCardViewModel(systemControl, this);
        OperatingMode = new OperatingModeCardViewModel(systemControl, this);
        FlowSpeed = new FlowSpeedCardViewModel(systemControl, this);
        SpecialModes = new SpecialModesCardViewModel(systemControl, this);
        BoostMode = new BoostModeCardViewModel(systemControl, this);

        // Subscribe to events
        _authService.AuthenticationChanged += OnAuthenticationChanged;
        _systemControl.StatusUpdated += OnStatusUpdated;

        RefreshStatusCommand = ReactiveCommand.CreateFromTask(
            RefreshStatusAsync,
            this.WhenAnyValue(x => x.IsAuthenticated));

        SubmitLoginCommand = ReactiveCommand.CreateFromTask(
            SubmitLoginAsync,
            this.WhenAnyValue(x => x.IsLoggingIn, x => x.IsAuthenticated, (loggingIn, auth) => !loggingIn && !auth));

        CancelLoginCommand = ReactiveCommand.CreateFromTask(
            CancelLoginAsync,
            this.WhenAnyValue(x => x.IsLoggingIn, loggingIn => !loggingIn));

        Log.Information("AppViewModel initialized with polling config: StatusRefresh={StatusRefresh}ms",
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

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsAuthenticated => _authService.IsAuthenticated;

    public LoginViewModel Login => _login;

    public bool IsLoginRequired => !IsAuthenticated;

    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        private set => this.RaiseAndSetIfChanged(ref _isLoggingIn, value);
    }

    // Expose properties from card VMs for convenience and bindings
    public bool IsManualMode => OperatingMode.IsManualMode;
    public bool IsSystemOnline => _systemControl.IsSystemOnline;

    // Temperature properties
    public double OutsideTemp => GetTemperature(0);
    public double FreshTemp => GetTemperature(1);
    public double IndoorTemp => GetTemperature(2);
    public double WasteTemp => GetTemperature(3);

    private double GetTemperature(int index)
    {
        var temps = _systemControl.CurrentStatus?.Temperatures;
        return temps != null && temps.Count > index ? temps[index] ?? 0 : 0;
    }

    public int Co2Level => _systemControl.CurrentStatus?.Co2 ?? 0;
    public int CurrentFlow => _systemControl.CurrentStatus?.FlowCurrent ?? 0;

    #endregion

    #region Commands

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshStatusCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SubmitLoginCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> CancelLoginCommand { get; }

    #endregion

    #region Event Handlers

    private async Task AutoLoginAsync()
    {
        try
        {
            StatusMessage = "Connecting to Wafe API...";
            Log.Information("Attempting auto-login...");
            
            var success = await _authService.TryAutoLoginAsync();
            
            if (!success)
            {
                StatusMessage = "Please sign in.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Auto-login failed");
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    private void OnAuthenticationChanged(object? sender, bool isAuthenticated)
    {
        this.RaisePropertyChanged(nameof(IsAuthenticated));
        this.RaisePropertyChanged(nameof(IsLoginRequired));
        
        if (isAuthenticated)
        {
            StatusMessage = "Connected successfully";
            _ = RefreshStatusAsync();
            _ = StartAutoRefreshAsync();
        }
        else
        {
            StopAutoRefresh();
            StatusMessage = "Disconnected";
        }
    }

    private async Task SubmitLoginAsync()
    {
        if (IsLoggingIn) return;

        if (string.IsNullOrWhiteSpace(_login.Username) || string.IsNullOrEmpty(_login.Password))
        {
            _login.ErrorMessage = "Username and password are required.";
            return;
        }

        try
        {
            IsLoggingIn = true;
            _login.ErrorMessage = null;
            StatusMessage = "Signing in...";

            var success = await _authService.LoginAsync(_login.Username, _login.Password, _login.RememberMe);
            if (!success)
            {
                _login.ErrorMessage = "Invalid username or password.";
                StatusMessage = "Please sign in.";
            }
        }
        catch
        {
            _login.ErrorMessage = "Sign in failed.";
            StatusMessage = "Please sign in.";
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    private Task CancelLoginAsync()
    {
        // Exit app if login is required and user cancels.
        // MainWindow can decide what to do with this state; we just clear fields.
        _login.Password = string.Empty;
        _login.ErrorMessage = null;
        StatusMessage = "Please sign in.";
        return Task.CompletedTask;
    }

    private void OnStatusUpdated(object? sender, SystemStatus status)
    {
        this.RaisePropertyChanged(nameof(OutsideTemp));
        this.RaisePropertyChanged(nameof(FreshTemp));
        this.RaisePropertyChanged(nameof(IndoorTemp));
        this.RaisePropertyChanged(nameof(WasteTemp));
        this.RaisePropertyChanged(nameof(Co2Level));
        this.RaisePropertyChanged(nameof(CurrentFlow));
        this.RaisePropertyChanged(nameof(IsManualMode));
        this.RaisePropertyChanged(nameof(IsSystemOnline));
        
        StatusMessage = _systemControl.IsSystemOnline 
            ? "System Online" 
            : "System Offline";
    }

    private async Task StartAutoRefreshAsync()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = new CancellationTokenSource();
        var token = _autoRefreshCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_pollingConfig.StatusRefreshIntervalMs, token);
                if (!token.IsCancellationRequested)
                {
                    await RefreshStatusAsync();
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in auto-refresh loop");
        }
    }

    private void StopAutoRefresh()
    {
        _autoRefreshCts?.Cancel();
        _autoRefreshCts?.Dispose();
        _autoRefreshCts = null;
    }

    #endregion

    #region Command Implementations

    public async Task RefreshStatusAsync()
    {
        try
        {
            await _systemControl.RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing status");
            StatusMessage = $"Refresh error: {ex.Message}";
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        _authService.AuthenticationChanged -= OnAuthenticationChanged;
        _systemControl.StatusUpdated -= OnStatusUpdated;

        StopAutoRefresh();
        
        // Dispose card ViewModels
        SystemControl?.Dispose();
        OperatingMode?.Dispose();
        FlowSpeed?.Dispose();
        SpecialModes?.Dispose();
        BoostMode?.Dispose();

        _disposed = true;
        Log.Information("AppViewModel disposed");
    }
}
