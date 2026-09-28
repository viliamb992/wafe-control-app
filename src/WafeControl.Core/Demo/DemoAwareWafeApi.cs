using WafeControl.Shared.Models;
using WafeControl.Shared.Services;
using WafeControl.Shared.Services.Http;

namespace WafeControl.Core.Demo;

/// <summary>
/// The <see cref="IWafeApiService"/> the app uses: the Wafe API normally, the demo unit while
/// <see cref="WafeSession.IsDemo"/>. Every call is routed on its own, so switching needs no restart.
/// </summary>
public sealed class DemoAwareWafeApi(WafeApiService real, DemoWafeApi demo, WafeSession session) : IWafeApiService
{
    private IWafeApiService Current => session.IsDemo ? demo : real;

    public Task<ApiResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default) =>
        real.AuthenticateAsync(username, password, cancellationToken);

    public Task<ApiResult<SystemStatus>> GetMainStatusAsync(CancellationToken cancellationToken = default) =>
        Current.GetMainStatusAsync(cancellationToken);

    public Task<ApiResult<HeaderInfo>> GetHeaderInfoAsync(CancellationToken cancellationToken = default) =>
        Current.GetHeaderInfoAsync(cancellationToken);

    public Task<ApiResult<SystemInfo>> GetSystemInfoAsync(CancellationToken cancellationToken = default) =>
        Current.GetSystemInfoAsync(cancellationToken);

    public Task<ApiResult> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default) =>
        Current.SetFlowSpeedAsync(speed, cancellationToken);

    public Task<ApiResult> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default) =>
        Current.SetAuthorityModeAsync(mode, cancellationToken);

    public Task<ApiResult> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default) =>
        Current.SetSilentModeAsync(enabled, cancellationToken);

    public Task<ApiResult> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default) =>
        Current.SetHolidayModeAsync(enabled, cancellationToken);

    public Task<ApiResult> SetBoostAsync(int seconds, CancellationToken cancellationToken = default) =>
        Current.SetBoostAsync(seconds, cancellationToken);

    public Task<ApiResult> SetStopActiveAsync(bool stopActive, CancellationToken cancellationToken = default) =>
        Current.SetStopActiveAsync(stopActive, cancellationToken);

    public Task<ApiResult<ScheduleResponse>> GetScheduleAsync(CancellationToken cancellationToken = default) =>
        Current.GetScheduleAsync(cancellationToken);

    public Task<ApiResult> SetSchedulePlanAsync(string plan, CancellationToken cancellationToken = default) =>
        Current.SetSchedulePlanAsync(plan, cancellationToken);

    public Task<ApiResult> SetUnitNameAsync(string name, CancellationToken cancellationToken = default) =>
        Current.SetUnitNameAsync(name, cancellationToken);
}
