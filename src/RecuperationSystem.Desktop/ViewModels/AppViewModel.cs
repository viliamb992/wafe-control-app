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
public class AppViewModel : ReactiveObject, IDisposable
{
    private readonly IAuthenticationService _authService;
    private readonly ISystemControlService _systemControl;
    private readonly PollingConfiguration _pollingConfig;
    private CancellationTokenSource? _autoRefreshCts;
    private bool _disposed;

    // Private backing fields
    private string _statusMessage = "Not connected";

    public AppViewModel(
        IAuthenticationService authService,
        ISystemControlService systemControl,
        IOptions<PollingConfiguration> pollingConfig)
    {
        _authService = authService;
        _systemControl = systemControl;
        _pollingConfig = pollingConfig.Value;

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
