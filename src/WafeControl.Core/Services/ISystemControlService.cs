using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Core.Services;

public interface ISystemControlService
{
    event EventHandler<SystemStatus>? StatusUpdated;
    event EventHandler<bool>? OperationInProgress;

    SystemStatus? CurrentStatus { get; }

    /// <summary>
    /// The last /header response: unit name, online flag, time of the data.
    /// </summary>
    HeaderInfo? CurrentHeader { get; }
    bool IsSystemRunning { get; }
    bool IsSystemStopped { get; }
    bool IsSystemOnline { get; }

    /// <summary>
    /// The unit reports sensor readings. It doesn't while stopped or between scheduled runs, even when online.
    /// </summary>
    bool HasSensorData { get; }

    /// <summary>
    /// Why the last refresh failed; <see cref="ApiError.None"/> after a successful one.
    /// </summary>
    ApiError LastRefreshError { get; }

    /// <summary>
    /// Failed refreshes in a row; 0 after a successful one.
    /// </summary>
    int ConsecutiveRefreshFailures { get; }

    /// <summary>
    /// Refreshes the status and the header; <see cref="StatusUpdated"/> is raised when either arrives.
    /// A failure keeps the last known status and is reported in <see cref="LastRefreshError"/>.
    /// </summary>
    Task<SystemStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The unit's model, serial number and service contact (GET /info). Null when not signed in or on failure.
    /// </summary>
    Task<SystemInfo?> GetSystemInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the unit, then refreshes so <see cref="CurrentHeader"/> carries the new name.
    /// </summary>
    Task<ApiResult> SetUnitNameAsync(string name, CancellationToken cancellationToken = default);

    // Commands: send, then wait until the unit reports the new value.
    Task<CommandOutcome> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default);
    Task<CommandOutcome> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default);
    Task<CommandOutcome> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<CommandOutcome> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<CommandOutcome> SetBoostAsync(int seconds, CancellationToken cancellationToken = default);
    Task<CommandOutcome> StartSystemAsync(CancellationToken cancellationToken = default);
    Task<CommandOutcome> StopSystemAsync(CancellationToken cancellationToken = default);
}
