using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecuperationSystem.Core.Configuration;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Services;

namespace RecuperationSystem.Core.Services;

public sealed class SystemControlService : ISystemControlService
{
    private readonly IWafeApiService _apiService;
    private readonly IAuthenticationService _authService;
    private readonly PollingConfiguration _pollingConfig;
    private readonly ILogger<SystemControlService> _logger;
    private SystemStatus? _currentStatus;

    public event EventHandler<SystemStatus>? StatusUpdated;
    public event EventHandler<bool>? OperationInProgress;

    public SystemControlService(
        IWafeApiService apiService,
        IAuthenticationService authService,
        IOptions<PollingConfiguration> pollingConfig,
        ILogger<SystemControlService> logger)
    {
        _apiService = apiService;
        _authService = authService;
        _pollingConfig = pollingConfig.Value;
        _logger = logger;
    }

    public SystemStatus? CurrentStatus => _currentStatus;
    public bool IsSystemRunning => _currentStatus?.IsSystemRunning ?? false;
    public bool IsSystemStopped => _currentStatus?.StopActive ?? true;

    // Check if system is online based on temperature sensor data
    public bool IsSystemOnline => _currentStatus?.Temperatures?.Any(t => t.HasValue) ?? false;

    public async Task<SystemStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_authService.IsAuthenticated)
        {
            _logger.LogDebug("Skipping status refresh because user is not authenticated.");
            return _currentStatus;
        }

        var status = await _apiService.GetMainStatusAsync(cancellationToken);
        if (status is null)
        {
            // Keep the last known status: a transient failure must not look like a state change.
            _logger.LogDebug("Status refresh returned no data; keeping last known status");
            return _currentStatus;
        }

        _currentStatus = status;
        StatusUpdated?.Invoke(this, status);

        _logger.LogDebug("Status refreshed - gen: {Gen}, Flow: {Flow}, Mode: {Mode}",
            status.Gen, status.FlowActual, status.Authority);

        return status;
    }

    public Task<bool> StartSystemAsync(CancellationToken cancellationToken = default)
        => ToggleSystemAsync(start: true, cancellationToken);

    public Task<bool> StopSystemAsync(CancellationToken cancellationToken = default)
        => ToggleSystemAsync(start: false, cancellationToken);

    private async Task<bool> ToggleSystemAsync(bool start, CancellationToken cancellationToken)
    {
        OperationInProgress?.Invoke(this, true);
        try
        {
            return await SendAndConfirmAsync(
                ct => _apiService.SetStopActiveAsync(!start, ct),
                status => status.StopActive,
                !start,
                start ? "System start" : "System stop",
                cancellationToken);
        }
        finally
        {
            OperationInProgress?.Invoke(this, false);
        }
    }

    public Task<bool> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetFlowSpeedAsync(speed, ct),
            status => status.FlowRequested,
            speed,
            "Flow speed change",
            cancellationToken);

    public Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetAuthorityModeAsync(mode, ct),
            status => status.Authority,
            mode,
            "Mode change",
            cancellationToken);

    public Task<bool> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetSilentModeAsync(enabled, ct),
            status => status.SilentActive,
            enabled,
            "Silent mode change",
            cancellationToken);

    public Task<bool> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetHolidayModeAsync(enabled, ct),
            status => status.HolidayActive,
            enabled,
            "Holiday mode change",
            cancellationToken);

    public Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetBoostAsync(seconds, ct),
            status => status.BoostRemaining,
            seconds,
            "Boost change",
            cancellationToken);

    /// <summary>
    /// Sends a command, then polls until the API reports the expected value.
    /// Returns false if the command is rejected or the change is not confirmed before the timeout.
    /// </summary>
    private async Task<bool> SendAndConfirmAsync<T>(
        Func<CancellationToken, Task<bool>> send,
        Func<SystemStatus, T> stateGetter,
        T expectedValue,
        string operationName,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Operation} to {Value} requested", operationName, expectedValue);

        if (!await send(cancellationToken))
        {
            _logger.LogWarning("{Operation} to {Value} rejected by the API", operationName, expectedValue);
            return false;
        }

        return await PollForStateChangeAsync(stateGetter, expectedValue, operationName, cancellationToken);
    }

    /// <summary>
    /// Waits for a state change to be reflected in the API.
    /// Uses the gen value to detect when new data is available.
    /// </summary>
    private async Task<bool> PollForStateChangeAsync<T>(
        Func<SystemStatus, T> stateGetter,
        T expectedValue,
        string operationName,
        CancellationToken cancellationToken)
    {
        var initialGen = _currentStatus?.Gen ?? 0;
        var maxDuration = TimeSpan.FromSeconds(_pollingConfig.StateChangeTimeoutSeconds);
        var startTime = DateTime.UtcNow;
        var pollCount = 0;

        while (DateTime.UtcNow - startTime < maxDuration)
        {
            await Task.Delay(_pollingConfig.StateChangeIntervalMs, cancellationToken);

            var status = await RefreshStatusAsync(cancellationToken);
            pollCount++;

            // Only a new gen carries new data.
            if (status is not null && status.Gen != initialGen
                && EqualityComparer<T>.Default.Equals(stateGetter(status), expectedValue))
            {
                _logger.LogInformation("{Operation} confirmed to {Value} after {Duration}ms ({Polls} polls, gen: {InitialGen} → {CurrentGen})",
                    operationName, expectedValue, (DateTime.UtcNow - startTime).TotalMilliseconds, pollCount, initialGen, status.Gen);
                return true;
            }
        }

        _logger.LogWarning("{Operation} to {Value} not confirmed after {Duration}s ({Polls} polls)",
            operationName, expectedValue, _pollingConfig.StateChangeTimeoutSeconds, pollCount);
        return false;
    }
}
