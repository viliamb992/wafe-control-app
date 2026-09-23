using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Core.Services;

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
    /// Refreshes the status and the header; <see cref="StatusUpdated"/> is raised when either arrives.
    /// </summary>
    Task<SystemStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The unit's model, serial number and service contact (GET /info). Null when not signed in or on failure.
    /// </summary>
    Task<SystemInfo?> GetSystemInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the unit, then refreshes so <see cref="CurrentHeader"/> carries the new name. False when the API refuses it.
    /// </summary>
    Task<bool> SetUnitNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default);
    Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default);
    Task<bool> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default);
    Task<bool> StartSystemAsync(CancellationToken cancellationToken = default);
    Task<bool> StopSystemAsync(CancellationToken cancellationToken = default);
}
