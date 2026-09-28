using WafeControl.Shared;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Core.Demo;

/// <summary>
/// Problems the demo unit can simulate, to try the app's error handling without the real unit (Debug builds).
/// </summary>
[Flags]
public enum DemoFaults
{
    None = 0,

    /// <summary>
    /// Commands apply after 40 s: longer than the confirmation timeout, so they are confirmed late.
    /// </summary>
    SlowConfirm = 1,

    /// <summary>
    /// Commands are accepted but never applied.
    /// </summary>
    NeverConfirm = 2,

    /// <summary>
    /// Commands are refused (HTTP 400).
    /// </summary>
    RejectCommands = 4,

    /// <summary>
    /// Every call fails as if the server couldn't be reached.
    /// </summary>
    ServerDown = 8,

    /// <summary>
    /// The unit is offline: no readings, header says offline.
    /// </summary>
    UnitOffline = 16,

    /// <summary>
    /// The unit's data time stops moving (10 minutes old).
    /// </summary>
    StaleHeader = 32,
}

/// <summary>
/// A simulated ventilation unit behind the <see cref="IWafeApiService"/> interface, for demo mode. Everything is
/// in memory: temperatures follow the time of day, CO₂ rises in the evening and falls with more air flow, and
/// commands apply after a short delay, like a real unit.
/// </summary>
public sealed class DemoWafeApi : IWafeApiService
{
    private static readonly TimeSpan ApplyDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SlowApplyDelay = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(250);

    // Weekdays: boost at 7:00 and 18:00; every night minimum; weekends automatic during the day.
    private const string SamplePlan =
        "boost-0:7:0-0:7:30 boost-0:18:0-0:19:0 min-0:23:0-1:6:0 boost-1:7:0-1:7:30 boost-1:18:0-1:19:0 min-1:23:0-2:6:0 "
        + "boost-2:7:0-2:7:30 boost-2:18:0-2:19:0 min-2:23:0-3:6:0 boost-3:7:0-3:7:30 boost-3:18:0-3:19:0 min-3:23:0-4:6:0 "
        + "boost-4:7:0-4:7:30 boost-4:18:0-4:19:0 min-4:23:0-5:6:0 auto-5:9:0-5:21:0 min-5:23:0-6:6:0 auto-6:9:0-6:21:0 min-6:23:0-0:6:0";

    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly List<(DateTimeOffset At, Action Apply)> _pending = [];
    private readonly DateTimeOffset _startedAt;

    private int _changes;
    private bool _stopActive;
    private string _authority = AppConstants.ModeIntelligent;
    private int _flowRequested = 120;
    private double _flowCurrent = 120;
    private DateTimeOffset _flowUpdatedAt;
    private DateTimeOffset? _boostUntil;
    private bool _silent;
    private bool _holiday;
    private string _name = "Demo";
    private string _plan = SamplePlan;

    public DemoWafeApi(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _startedAt = _flowUpdatedAt = _time.GetUtcNow();
    }

    /// <summary>
    /// Problems to simulate; changeable at any time.
    /// </summary>
    public DemoFaults Faults { get; set; }

    public const string SerialNumber = "DEMO-0001";

    public Task<ApiResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Success);

    public async Task<ApiResult<SystemStatus>> GetMainStatusAsync(CancellationToken cancellationToken = default)
    {
        if (await FailAsync(cancellationToken) is { } error)
            return ApiResult<SystemStatus>.Fail(error);

        lock (_gate)
            return BuildStatus(ApplyDue());
    }

    public async Task<ApiResult<HeaderInfo>> GetHeaderInfoAsync(CancellationToken cancellationToken = default)
    {
        if (await FailAsync(cancellationToken) is { } error)
            return ApiResult<HeaderInfo>.Fail(error);

        lock (_gate)
        {
            var now = ApplyDue();
            var time = Faults.HasFlag(DemoFaults.StaleHeader) ? now.AddMinutes(-10) : now;
            return new HeaderInfo
            {
                Gen = Gen(now),
                Name = _name,
                SerialNumber = SerialNumber,
                Type = "W201 E",
                Timestamp = time.ToUnixTimeMilliseconds() / 1000.0,
                Online = !Faults.HasFlag(DemoFaults.UnitOffline),
            };
        }
    }

    public async Task<ApiResult<SystemInfo>> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        if (await FailAsync(cancellationToken) is { } error)
            return ApiResult<SystemInfo>.Fail(error);

        return new SystemInfo
        {
            Contacts = new SystemContacts
            {
                Service = new ContactInfo { Name = "Demo service", Mail = "service@example.com", Web = "https://example.com/" },
            },
            Unit = new UnitInfo { Model = "DEMO", SerialNumber = SerialNumber, Type = "W201 E" },
        };
    }

    public async Task<ApiResult<ScheduleResponse>> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        if (await FailAsync(cancellationToken) is { } error)
            return ApiResult<ScheduleResponse>.Fail(error);

        lock (_gate)
        {
            return new ScheduleResponse
            {
                Modes = [AppConstants.ScheduleModeMin, AppConstants.ScheduleModeAuto, AppConstants.ScheduleModeNominal, AppConstants.ScheduleModeBoost],
                Plan = _plan,
            };
        }
    }

    public Task<ApiResult> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default) =>
        speed is < AppConstants.MinFlowSpeed or > AppConstants.MaxFlowSpeed
            ? Task.FromResult(ApiResult.Fail(ApiError.Rejected, 400))
            : CommandAsync(() => _flowRequested = speed, cancellationToken);

    public Task<ApiResult> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default) =>
        CommandAsync(() => _authority = mode, cancellationToken);

    public Task<ApiResult> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default) =>
        CommandAsync(() => _silent = enabled, cancellationToken);

    public Task<ApiResult> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default) =>
        CommandAsync(() => _holiday = enabled, cancellationToken);

    public Task<ApiResult> SetBoostAsync(int seconds, CancellationToken cancellationToken = default) =>
        CommandAsync(() => _boostUntil = seconds > 0 ? _time.GetUtcNow().AddSeconds(seconds) : null, cancellationToken);

    public Task<ApiResult> SetStopActiveAsync(bool stopActive, CancellationToken cancellationToken = default) =>
        CommandAsync(() => _stopActive = stopActive, cancellationToken);

    public async Task<ApiResult> SetSchedulePlanAsync(string plan, CancellationToken cancellationToken = default)
    {
        if (await FailAsync(cancellationToken) is { } error)
            return ApiResult.Fail(error);
        if (Faults.HasFlag(DemoFaults.RejectCommands))
            return ApiResult.Fail(ApiError.Rejected, 400);

        lock (_gate)
            _plan = plan;
        return ApiResult.Success;
    }

    public async Task<ApiResult> SetUnitNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > AppConstants.MaxUnitNameLength)
            return ApiResult.Fail(ApiError.Rejected, 400);
        if (await FailAsync(cancellationToken) is { } error)
            return ApiResult.Fail(error);

        lock (_gate)
        {
            _name = name;
            _changes++;
        }

        return ApiResult.Success;
    }

    /// <summary>
    /// Accepts a command and applies it a little later, as the real unit does.
    /// </summary>
    private async Task<ApiResult> CommandAsync(Action apply, CancellationToken cancellationToken)
    {
        if (await FailAsync(cancellationToken) is { } error)
            return ApiResult.Fail(error);
        if (Faults.HasFlag(DemoFaults.RejectCommands))
            return ApiResult.Fail(ApiError.Rejected, 400);
        if (Faults.HasFlag(DemoFaults.NeverConfirm))
            return ApiResult.Success;

        var delay = Faults.HasFlag(DemoFaults.SlowConfirm) ? SlowApplyDelay : ApplyDelay;
        lock (_gate)
            _pending.Add((_time.GetUtcNow() + delay, apply));
        return ApiResult.Success;
    }

    private async Task<ApiError?> FailAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Latency, _time, cancellationToken);
        return Faults.HasFlag(DemoFaults.ServerDown) ? ApiError.Offline : null;
    }

    /// <summary>
    /// Applies the commands whose time has come; returns the current time.
    /// </summary>
    private DateTimeOffset ApplyDue()
    {
        var now = _time.GetUtcNow();
        foreach (var command in _pending.Where(p => p.At <= now).ToList())
        {
            UpdateFlow(now);
            command.Apply();
            _pending.Remove(command);
            _changes++;
        }

        UpdateFlow(now);
        return now;
    }

    // Fresh data every 5 s, like the real unit, plus one step for every change.
    private int Gen(DateTimeOffset now) => 10_000 + (int)((now - _startedAt).TotalSeconds / 5) + _changes;

    private int BoostRemaining(DateTimeOffset now) =>
        _boostUntil is { } until && until > now ? (int)Math.Ceiling((until - now).TotalSeconds) : 0;

    /// <summary>
    /// The flow the unit aims for in its current state.
    /// </summary>
    private int TargetFlow(DateTimeOffset now)
    {
        if (_stopActive)
            return 0;
        if (BoostRemaining(now) > 0)
            return AppConstants.MaxFlowSpeed;
        if (_holiday)
            return AppConstants.MinFlowSpeed;

        var flow = _authority switch
        {
            AppConstants.ModeManual => _flowRequested,
            AppConstants.ModeSchedule => 110,
            _ => Occupancy(now) > 0.6 ? 170 : 100,
        };
        return _silent ? Math.Min(flow, 90) : flow;
    }

    // The actual flow follows the target within about 10 s.
    private void UpdateFlow(DateTimeOffset now)
    {
        var elapsed = (now - _flowUpdatedAt).TotalSeconds;
        _flowCurrent += (TargetFlow(now) - _flowCurrent) * Math.Clamp(elapsed / 10, 0, 1);
        _flowUpdatedAt = now;
    }

    /// <summary>
    /// How many people are home, 0–1: low at night and during work hours, highest in the evening.
    /// </summary>
    private static double Occupancy(DateTimeOffset now)
    {
        var hour = now.ToLocalTime().TimeOfDay.TotalHours;
        return hour switch
        {
            < 6 => 0.5,
            < 8 => 0.8,
            < 16 => 0.2,
            < 23 => 0.9,
            _ => 0.6,
        };
    }

    private SystemStatus BuildStatus(DateTimeOffset now)
    {
        var reporting = !_stopActive && !Faults.HasFlag(DemoFaults.UnitOffline);
        var hour = now.ToLocalTime().TimeOfDay.TotalHours;

        // Outdoor 4–14 °C over the day, warmest mid-afternoon; heat recovery about 85 %.
        var outdoor = 9 + 5 * Math.Sin((hour - 9) / 24 * 2 * Math.PI);
        var indoor = 22.3 + 0.4 * Math.Sin((hour - 12) / 24 * 2 * Math.PI);
        var supply = outdoor + 0.85 * (indoor - outdoor);
        var exhaust = indoor - 0.85 * (indoor - outdoor);

        var flow = Math.Max(_flowCurrent, AppConstants.MinFlowSpeed);
        var co2 = (int)Math.Clamp(420 + Occupancy(now) * 900 * (100 / flow), 420, 1600);

        return new SystemStatus
        {
            Gen = Gen(now),
            Temperatures = reporting ? [Round(outdoor), Round(supply), Round(indoor), Round(exhaust)] : [null, null, null, null],
            Humidity = reporting ? Math.Round(45 + 5 * Math.Sin(hour / 24 * 2 * Math.PI)) : null,
            Co2 = reporting ? co2 : null,
            FlowMin = AppConstants.MinFlowSpeed,
            FlowMax = AppConstants.MaxFlowSpeed,
            FlowRequested = _flowRequested,
            FlowCurrent = reporting ? (int)Math.Round(_flowCurrent) : 0,
            Authority = _authority,
            AuthorityAvailable = [AppConstants.ModeIntelligent, AppConstants.ModeManual, AppConstants.ModeSchedule],
            Capabilities = ["boost", "silent", "holiday"],
            HolidayActive = _holiday,
            BoostDuration = 900,
            BoostRemaining = BoostRemaining(now),
            SilentActive = _silent,
            StopActive = _stopActive,
            Filters = new FilterStatus
            {
                Fresh = new FilterInfo { Health = 72, Status = "good" },
                Waste = new FilterInfo { Health = 80, Status = "good" },
            },
            Uptime = (int)(now - _startedAt).TotalSeconds,
        };
    }

    private static double Round(double value) => Math.Round(value, 1);
}
