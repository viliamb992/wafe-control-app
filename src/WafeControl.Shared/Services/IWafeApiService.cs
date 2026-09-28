using System.Threading;
using System.Threading.Tasks;
using WafeControl.Shared.Models;

namespace WafeControl.Shared.Services;

/// <summary>
/// The Wafe REST API. Calls don't throw on network or server failures: the result says what went wrong.
/// </summary>
public interface IWafeApiService
{
    /// <summary>
    /// Signs in and starts the session; <see cref="ApiError.Unauthorized"/> for wrong credentials.
    /// </summary>
    Task<ApiResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<ApiResult<SystemStatus>> GetMainStatusAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<HeaderInfo>> GetHeaderInfoAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<SystemInfo>> GetSystemInfoAsync(CancellationToken cancellationToken = default);
    Task<ApiResult> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default);
    Task<ApiResult> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default);
    Task<ApiResult> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<ApiResult> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<ApiResult> SetBoostAsync(int seconds, CancellationToken cancellationToken = default);
    Task<ApiResult> SetStopActiveAsync(bool stopActive, CancellationToken cancellationToken = default);
    Task<ApiResult<ScheduleResponse>> GetScheduleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the whole weekly plan (see <see cref="SchedulePlan"/>).
    /// </summary>
    Task<ApiResult> SetSchedulePlanAsync(string plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the unit in the Wafe portal. Refuses empty names and names over <see cref="AppConstants.MaxUnitNameLength"/> characters.
    /// </summary>
    Task<ApiResult> SetUnitNameAsync(string name, CancellationToken cancellationToken = default);
}
