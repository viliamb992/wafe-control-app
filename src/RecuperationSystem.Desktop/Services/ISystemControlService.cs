using System;
using System.Threading;
using System.Threading.Tasks;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Desktop.Services;

public interface ISystemControlService
{
    event EventHandler<SystemStatus>? StatusUpdated;
    event EventHandler<bool>? OperationInProgress;
    
    SystemStatus? CurrentStatus { get; }
    bool IsSystemRunning { get; }
    bool IsSystemStopped { get; }
    bool IsSystemOnline { get; }
    
    Task<SystemStatus?> RefreshStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default);
    Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default);
    Task<bool> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default);
    Task<bool> StartSystemAsync(CancellationToken cancellationToken = default);
    Task<bool> StopSystemAsync(CancellationToken cancellationToken = default);
}
