using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RecuperationSystem.Desktop.Configuration;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Services;
using Serilog;

namespace RecuperationSystem.Desktop.Services;

public class SystemControlService : ISystemControlService
{
    private readonly IWafeApiService _apiService;
    private readonly IAuthenticationService _authService;
    private readonly PollingConfiguration _pollingConfig;
    private SystemStatus? _currentStatus;

    public event EventHandler<SystemStatus>? StatusUpdated;
    public event EventHandler<bool>? OperationInProgress;

    public SystemControlService(
        IWafeApiService apiService,
        IAuthenticationService authService,
        IOptions<PollingConfiguration> pollingConfig)
    {
        _apiService = apiService;
        _authService = authService;
        _pollingConfig = pollingConfig.Value;
        
        Log.Information("SystemControlService initialized with polling config: StatusRefresh={StatusRefresh}ms, StateChange={StateChange}ms, Timeout={Timeout}s",
            _pollingConfig.StatusRefreshIntervalMs, _pollingConfig.StateChangeIntervalMs, _pollingConfig.StateChangeTimeoutSeconds);
    }

    public SystemStatus? CurrentStatus => _currentStatus;
    public bool IsSystemRunning => _currentStatus?.IsSystemRunning ?? false;
    public bool IsSystemStopped => _currentStatus?.StopActive ?? true;
    
    // Check if system is online based on temperature sensor data
    public bool IsSystemOnline => _currentStatus?.Temperatures != null && 
                                   _currentStatus.Temperatures.Count > 0 && 
                                   _currentStatus.Temperatures.Any(t => t.HasValue);

    public async Task<SystemStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_authService.IsAuthenticated)
        {
            Log.Debug("Skipping status refresh because user is not authenticated.");
            return _currentStatus;
        }

        try
        {
            Log.Debug("Refreshing system status...");
            
            _currentStatus = await _apiService.GetMainStatusAsync(cancellationToken);
            
            if (_currentStatus != null)
            {
                StatusUpdated?.Invoke(this, _currentStatus);
                
                Log.Debug("Status refreshed - gen: {Gen}, Flow: {Flow}, Mode: {Mode}", 
                    _currentStatus.Gen, _currentStatus.FlowActual, _currentStatus.Authority);
            }

            return _currentStatus;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing status");
            throw;
        }
    }

    /// <summary>
    /// Generic polling method that waits for a state change to be reflected in the API.
    /// Uses gen value to detect when new data is available.
    /// </summary>
    private async Task<bool> PollForStateChangeAsync<T>(
        Func<SystemStatus?, T> stateGetter,
        T expectedValue,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var initialGen = _currentStatus?.Gen ?? 0;
        var maxDuration = TimeSpan.FromSeconds(_pollingConfig.StateChangeTimeoutSeconds);
        var startTime = DateTime.UtcNow;
        var pollCount = 0;

        while (DateTime.UtcNow - startTime < maxDuration)
        {
            await Task.Delay(_pollingConfig.StateChangeIntervalMs, cancellationToken);
            
            await RefreshStatusAsync(cancellationToken);
            pollCount++;

            var currentGen = _currentStatus?.Gen ?? 0;

            // Only process if gen has changed (indicates new data)
            if (currentGen != initialGen)
            {
                var currentValue = stateGetter(_currentStatus);
                
                if (EqualityComparer<T>.Default.Equals(currentValue, expectedValue))
                {
                    Log.Information("{Operation} confirmed to {Value} after {Duration}ms ({Polls} polls, gen: {InitialGen} → {CurrentGen})",
                        operationName, expectedValue, (DateTime.UtcNow - startTime).TotalMilliseconds, pollCount, initialGen, currentGen);
                    return true;
                }
            }
        }

        // Timeout reached
        Log.Warning("{Operation} to {Value} not confirmed after {Duration}s ({Polls} polls)",
            operationName, expectedValue, _pollingConfig.StateChangeTimeoutSeconds, pollCount);
        return false;
    }

    public Task<bool> StartSystemAsync(CancellationToken cancellationToken = default)
        => ToggleSystemAsync(true, cancellationToken);

    public Task<bool> StopSystemAsync(CancellationToken cancellationToken = default)
        => ToggleSystemAsync(false, cancellationToken);

    private async Task<bool> ToggleSystemAsync(bool start, CancellationToken cancellationToken = default)
    {
        try
        {
            OperationInProgress?.Invoke(this, true);
            
            var action = start ? "Starting" : "Stopping";
            Log.Information("{Action} system...", action);
            
            await _apiService.SetStopActiveAsync(!start, cancellationToken);
            
            // Poll for system state change using gen-based polling
            return await PollForStateChangeAsync(
                status => status?.StopActive ?? true,
                !start,
                "System state change",
                cancellationToken);
        }
        catch (TaskCanceledException)
        {
            Log.Debug("System toggle operation cancelled");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling system");
            throw;
        }
        finally
        {
            OperationInProgress?.Invoke(this, false);
        }
    }

    public async Task<bool> SetFlowSpeedAsync(int flowSpeed, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("Updating flow speed to: {FlowSpeed}", flowSpeed);
            
            await _apiService.SetFlowSpeedAsync(flowSpeed, cancellationToken);
            
            // Poll for flow speed change using gen-based polling
            return await PollForStateChangeAsync(
                status => status?.FlowRequested ?? 0,
                flowSpeed,
                "Flow speed change",
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating flow speed");
            throw;
        }
    }

    public async Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("Updating mode to: {Mode}", mode);
            
            await _apiService.SetAuthorityModeAsync(mode, cancellationToken);
            
            // Poll for mode change using gen-based polling
            return await PollForStateChangeAsync(
                status => status?.Authority ?? "",
                mode,
                "Mode change",
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating mode");
            throw;
        }
    }

    public async Task<bool> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("Setting silent mode to: {Enabled}", enabled);
            
            await _apiService.SetSilentModeAsync(enabled, cancellationToken);
            
            // Poll for silent mode change using gen-based polling
            return await PollForStateChangeAsync(
                status => status?.SilentActive ?? false,
                enabled,
                "Silent mode change",
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting silent mode");
            throw;
        }
    }

    public async Task<bool> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("Setting holiday mode to: {Enabled}", enabled);
            
            await _apiService.SetHolidayModeAsync(enabled, cancellationToken);
            
            // Poll for holiday mode change using gen-based polling
            return await PollForStateChangeAsync(
                status => status?.HolidayActive ?? false,
                enabled,
                "Holiday mode change",
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting holiday mode");
            throw;
        }
    }

    public async Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default)
    {
        try
        {
            var minutes = seconds / 60;
            Log.Information("Setting boost to {Seconds} seconds ({Minutes} minutes)", seconds, minutes);
            
            await _apiService.SetBoostAsync(seconds, cancellationToken);
            
            // Poll for boost change using gen-based polling
            return await PollForStateChangeAsync(
                status => status?.BoostRemaining ?? 0,
                seconds,
                "Boost change",
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting boost");
            throw;
        }
    }
}

