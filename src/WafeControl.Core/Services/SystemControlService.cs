using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WafeControl.Core.Configuration;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Core.Services;

public sealed class SystemControlService : ISystemControlService
{
    private readonly IWafeApiService _apiService;
    private readonly IAuthenticationService _authService;
    private readonly PollingConfiguration _pollingConfig;
    private readonly ILogger<SystemControlService> _logger;
    private readonly TimeProvider _time;
    private SystemStatus? _currentStatus;
    private HeaderInfo? _currentHeader;

    public event EventHandler<SystemStatus>? StatusUpdated;
    public event EventHandler<bool>? OperationInProgress;

    public SystemControlService(
        IWafeApiService apiService,
        IAuthenticationService authService,
        IOptions<PollingConfiguration> pollingConfig,
        ILogger<SystemControlService> logger,
        TimeProvider? timeProvider = null)
    {
        _apiService = apiService;
        _authService = authService;
        _pollingConfig = pollingConfig.Value;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public SystemStatus? CurrentStatus => _currentStatus;
    public HeaderInfo? CurrentHeader => _currentHeader;
    public bool IsSystemRunning => _currentStatus?.IsSystemRunning ?? false;
    public bool IsSystemStopped => _currentStatus?.StopActive ?? true;

    // The unit's own flag from /header; until that arrives, sensor data is the best guess.
    public bool IsSystemOnline => _currentHeader?.Online ?? HasSensorData;

    public bool HasSensorData => _currentStatus?.Temperatures?.Any(t => t.HasValue) ?? false;

    public ApiError LastRefreshError { get; private set; }

    public int ConsecutiveRefreshFailures { get; private set; }

    public Task<SystemStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default)
        => RefreshAsync(includeHeader: true, cancellationToken);

    public async Task<SystemInfo?> GetSystemInfoAsync(CancellationToken cancellationToken = default)
        => _authService.IsAuthenticated ? (await _apiService.GetSystemInfoAsync(cancellationToken)).Value : null;

    public async Task<ApiResult> SetUnitNameAsync(string name, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Unit rename to {Name} requested", name);

        var result = await _apiService.SetUnitNameAsync(name, cancellationToken);
        if (!result.Ok)
        {
            _logger.LogWarning("Unit rename failed: {Error}", result.Error);
            return result;
        }

        await RefreshStatusAsync(cancellationToken);
        return result;
    }

    private async Task<SystemStatus?> RefreshAsync(bool includeHeader, CancellationToken cancellationToken)
    {
        if (!_authService.IsAuthenticated)
        {
            _logger.LogDebug("Skipping status refresh because user is not authenticated.");
            return _currentStatus;
        }

        var headerTask = includeHeader
            ? _apiService.GetHeaderInfoAsync(cancellationToken)
            : Task.FromResult<ApiResult<HeaderInfo>>(default);
        var status = await _apiService.GetMainStatusAsync(cancellationToken);
        var header = await headerTask;

        if (header.Ok)
            _currentHeader = header.Value;

        if (!status.Ok)
        {
            // Keep the last known status: a transient failure must not look like a state change.
            LastRefreshError = status.Error == ApiError.None ? ApiError.InvalidResponse : status.Error;
            ConsecutiveRefreshFailures++;
            _logger.LogDebug("Status refresh failed ({Error}, {Failures} in a row); keeping last known status",
                LastRefreshError, ConsecutiveRefreshFailures);

            // A new header alone (e.g. the unit went offline) still needs to reach the UI.
            if (header.Ok && _currentStatus is not null)
                StatusUpdated?.Invoke(this, _currentStatus);
            return _currentStatus;
        }

        LastRefreshError = ApiError.None;
        ConsecutiveRefreshFailures = 0;
        _currentStatus = status.Value;
        StatusUpdated?.Invoke(this, status.Value!);

        _logger.LogDebug("Status refreshed - gen: {Gen}, Flow: {Flow}, Mode: {Mode}, Online: {Online}",
            status.Value!.Gen, status.Value.FlowActual, status.Value.Authority, IsSystemOnline);

        return _currentStatus;
    }

    public Task<CommandOutcome> StartSystemAsync(Action? onSent = null, CancellationToken cancellationToken = default)
        => ToggleSystemAsync(start: true, onSent, cancellationToken);

    public Task<CommandOutcome> StopSystemAsync(Action? onSent = null, CancellationToken cancellationToken = default)
        => ToggleSystemAsync(start: false, onSent, cancellationToken);

    private async Task<CommandOutcome> ToggleSystemAsync(bool start, Action? onSent, CancellationToken cancellationToken)
    {
        OperationInProgress?.Invoke(this, true);
        try
        {
            return await SendAndConfirmAsync(
                ct => _apiService.SetStopActiveAsync(!start, ct),
                status => status.StopActive,
                !start,
                start ? "System start" : "System stop",
                onSent,
                cancellationToken);
        }
        finally
        {
            OperationInProgress?.Invoke(this, false);
        }
    }

    public Task<CommandOutcome> SetFlowSpeedAsync(int speed, Action? onSent = null, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetFlowSpeedAsync(speed, ct),
            status => status.FlowRequested,
            speed,
            "Flow speed change",
            onSent,
            cancellationToken);

    public Task<CommandOutcome> SetAuthorityModeAsync(string mode, Action? onSent = null, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetAuthorityModeAsync(mode, ct),
            status => status.Authority,
            mode,
            "Mode change",
            onSent,
            cancellationToken);

    public Task<CommandOutcome> SetSilentModeAsync(bool enabled, Action? onSent = null, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetSilentModeAsync(enabled, ct),
            status => status.SilentActive,
            enabled,
            "Silent mode change",
            onSent,
            cancellationToken);

    public Task<CommandOutcome> SetHolidayModeAsync(bool enabled, Action? onSent = null, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetHolidayModeAsync(enabled, ct),
            status => status.HolidayActive,
            enabled,
            "Holiday mode change",
            onSent,
            cancellationToken);

    public Task<CommandOutcome> SetBoostAsync(int seconds, Action? onSent = null, CancellationToken cancellationToken = default)
        => SendAndConfirmAsync(
            ct => _apiService.SetBoostAsync(seconds, ct),
            status => status.BoostRemaining,
            seconds,
            "Boost change",
            onSent,
            cancellationToken);

    /// <summary>
    /// Sends a command, then polls until the API reports the expected value. A rejected command fails at once;
    /// one the unit doesn't confirm before the timeout is pending, and stays watched for a while.
    /// </summary>
    private async Task<CommandOutcome> SendAndConfirmAsync<T>(
        Func<CancellationToken, Task<ApiResult>> send,
        Func<SystemStatus, T> stateGetter,
        T expectedValue,
        string operationName,
        Action? onSent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Operation} to {Value} requested", operationName, expectedValue);

        var result = await send(cancellationToken);
        if (!result.Ok)
        {
            _logger.LogWarning("{Operation} to {Value} failed: {Error}", operationName, expectedValue, result.Error);
            return CommandOutcome.Failed(result.Error);
        }

        onSent?.Invoke();

        if (await PollForStateChangeAsync(stateGetter, expectedValue, operationName, cancellationToken))
            return CommandOutcome.Confirmed;

        return CommandOutcome.Pending(WatchForLateConfirmation(stateGetter, expectedValue, operationName));
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
        var startTime = _time.GetTimestamp();
        var pollCount = 0;

        while (_time.GetElapsedTime(startTime) < maxDuration)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(PollInterval(pollCount)), _time, cancellationToken);

            // Only /main carries the changed value; skip the header while polling fast.
            var status = await RefreshAsync(includeHeader: false, cancellationToken);
            pollCount++;

            // Only a new gen carries new data.
            if (status is not null && status.Gen != initialGen
                && EqualityComparer<T>.Default.Equals(stateGetter(status), expectedValue))
            {
                _logger.LogInformation("{Operation} confirmed to {Value} after {Duration}ms ({Polls} polls, gen: {InitialGen} → {CurrentGen})",
                    operationName, expectedValue, _time.GetElapsedTime(startTime).TotalMilliseconds, pollCount, initialGen, status.Gen);
                return true;
            }
        }

        _logger.LogWarning("{Operation} to {Value} not confirmed after {Duration}s ({Polls} polls)",
            operationName, expectedValue, _pollingConfig.StateChangeTimeoutSeconds, pollCount);
        return false;
    }

    private int PollInterval(int pollCount) =>
        pollCount < _pollingConfig.StateChangeInitialIntervalsMs.Length
            ? _pollingConfig.StateChangeInitialIntervalsMs[pollCount]
            : _pollingConfig.StateChangeIntervalMs;

    /// <summary>
    /// After a timeout, watches the regular status updates for the requested value.
    /// </summary>
    private Task<bool> WatchForLateConfirmation<T>(Func<SystemStatus, T> stateGetter, T expectedValue, string operationName)
    {
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITimer? timer = null;

        void OnStatus(object? sender, SystemStatus status)
        {
            if (EqualityComparer<T>.Default.Equals(stateGetter(status), expectedValue))
                Finish(confirmed: true);
        }

        void Finish(bool confirmed)
        {
            if (!result.TrySetResult(confirmed))
                return;

            StatusUpdated -= OnStatus;
            timer?.Dispose();
            if (confirmed)
                _logger.LogInformation("{Operation} to {Value} confirmed late", operationName, expectedValue);
            else
                _logger.LogWarning("{Operation} to {Value} never confirmed", operationName, expectedValue);
        }

        timer = _time.CreateTimer(_ => Finish(confirmed: false), null,
            TimeSpan.FromSeconds(_pollingConfig.LateConfirmationSeconds), Timeout.InfiniteTimeSpan);
        StatusUpdated += OnStatus;
        return result.Task;
    }
}
