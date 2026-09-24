using System.Threading;
using System.Threading.Tasks;
using WafeControl.Shared.Models;

namespace WafeControl.Shared.Services;

public interface IWafeApiService
{
    Task<bool> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<SystemStatus?> GetMainStatusAsync(CancellationToken cancellationToken = default);
    Task<HeaderInfo?> GetHeaderInfoAsync(CancellationToken cancellationToken = default);
    Task<SystemInfo?> GetSystemInfoAsync(CancellationToken cancellationToken = default);
    Task<bool> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default);
    Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default);
    Task<bool> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default);
    Task<bool> SetStopActiveAsync(bool stopActive, CancellationToken cancellationToken = default);
    Task<ScheduleResponse?> GetScheduleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the whole weekly plan (see <see cref="SchedulePlan"/>).
    /// </summary>
    Task<bool> SetSchedulePlanAsync(string plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the unit in the Wafe portal. Refuses empty names and names over <see cref="AppConstants.MaxUnitNameLength"/> characters.
    /// </summary>
    Task<bool> SetUnitNameAsync(string name, CancellationToken cancellationToken = default);
}
