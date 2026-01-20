using System.Threading;
using System.Threading.Tasks;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Shared.Services;

public interface IWafeApiService
{
    Task<bool> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<SystemStatus?> GetMainStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default);
    Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default);
    Task<bool> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default);
    Task<bool> SetStopActiveAsync(bool stopActive, CancellationToken cancellationToken = default);
}
