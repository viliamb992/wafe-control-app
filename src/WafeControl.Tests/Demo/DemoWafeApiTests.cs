using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using WafeControl.Core.Configuration;
using WafeControl.Core.Demo;
using WafeControl.Core.Services;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;
using WafeControl.Shared.Services.Http;

namespace WafeControl.Tests.Demo;

public class DemoWafeApiTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 18, 0, 0, TimeSpan.Zero));
    private readonly DemoWafeApi _sut;

    public DemoWafeApiTests() => _sut = new DemoWafeApi(_time);

    // Every call waits a simulated network latency; move time past it.
    private async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        var task = call();
        _time.Advance(TimeSpan.FromSeconds(1));
        return await task;
    }

    [Fact]
    public async Task Status_LooksLikeARunningUnit()
    {
        var status = (await CallAsync(() => _sut.GetMainStatusAsync(TestContext.Current.CancellationToken))).Value!;

        Assert.False(status.StopActive);
        Assert.Equal(4, status.Temperatures!.Count(t => t.HasValue));
        Assert.InRange(status.Co2 ?? 0, 420, 1600);
        Assert.Equal(["intelligent", "manual", "schedule"], status.AuthorityAvailable!);
    }

    [Fact]
    public async Task Command_AppliesAfterAShortDelay()
    {
        Assert.True((await CallAsync(() => _sut.SetBoostAsync(900, TestContext.Current.CancellationToken))).Ok);

        var before = (await CallAsync(() => _sut.GetMainStatusAsync(TestContext.Current.CancellationToken))).Value!;
        _time.Advance(TimeSpan.FromSeconds(3));
        var after = (await CallAsync(() => _sut.GetMainStatusAsync(TestContext.Current.CancellationToken))).Value!;

        Assert.Equal(0, before.BoostRemaining);
        Assert.InRange(after.BoostRemaining, 890, 900);
        Assert.NotEqual(before.Gen, after.Gen);
    }

    [Fact]
    public async Task Stopped_ReportsNoReadings()
    {
        await CallAsync(() => _sut.SetStopActiveAsync(true, TestContext.Current.CancellationToken));
        _time.Advance(TimeSpan.FromSeconds(3));

        var status = (await CallAsync(() => _sut.GetMainStatusAsync(TestContext.Current.CancellationToken))).Value!;

        Assert.True(status.StopActive);
        Assert.All(status.Temperatures!, t => Assert.Null(t));
    }

    [Fact]
    public async Task SamplePlan_IsUnderstoodByTheApp()
    {
        var schedule = (await CallAsync(() => _sut.GetScheduleAsync(TestContext.Current.CancellationToken))).Value!;

        Assert.True(SchedulePlan.TryParse(schedule.Plan, out var entries));
        Assert.True(entries.Count > 10);
    }

    [Fact]
    public async Task Info_UsesNoRealContacts()
    {
        var info = (await CallAsync(() => _sut.GetSystemInfoAsync(TestContext.Current.CancellationToken))).Value!;

        Assert.EndsWith("@example.com", info.Contacts!.Service!.Mail);
        Assert.Equal(DemoWafeApi.SerialNumber, info.Unit!.SerialNumber);
    }

    [Fact]
    public async Task ServerDownFault_FailsAsOffline()
    {
        _sut.Faults = DemoFaults.ServerDown;

        var result = await CallAsync(() => _sut.GetMainStatusAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ApiError.Offline, result.Error);
    }

    [Fact]
    public async Task RejectFault_RefusesCommands()
    {
        _sut.Faults = DemoFaults.RejectCommands;

        var result = await CallAsync(() => _sut.SetSilentModeAsync(true, TestContext.Current.CancellationToken));

        Assert.Equal(ApiError.Rejected, result.Error);
    }

    [Fact]
    public async Task UnitOfflineFault_HeaderSaysOffline()
    {
        _sut.Faults = DemoFaults.UnitOffline;

        var header = (await CallAsync(() => _sut.GetHeaderInfoAsync(TestContext.Current.CancellationToken))).Value!;

        Assert.False(header.Online);
    }

    [Fact]
    public async Task Router_UsesTheDemoOnlyInDemoMode()
    {
        var session = new WafeSession();
        var real = new WafeApiService(new HttpClient(new Helpers.StubHttpHandler(_ => Helpers.StubHttpHandler.Json("""{"gen":7}""")))
            { BaseAddress = new Uri("https://example.com/api/") }, session, NullLogger<WafeApiService>.Instance);
        var router = new DemoAwareWafeApi(real, _sut, session);

        Assert.Equal(7, (await router.GetMainStatusAsync(TestContext.Current.CancellationToken)).Value!.Gen);

        session.StartDemo();
        var demoGen = (await CallAsync(() => router.GetMainStatusAsync(TestContext.Current.CancellationToken))).Value!.Gen;
        Assert.True(demoGen >= 10_000);
    }
}

public class DemoUnitCommandTests
{
    [Fact]
    public async Task CommandsToTheDemoUnit_AreConfirmed()
    {
        // Real time: the demo unit applies commands after about 2 s, well within the timeout.
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        var demo = new DemoWafeApi();
        var sut = new SystemControlService(demo, auth, Options.Create(new PollingConfiguration
        {
            StateChangeIntervalMs = 250,
            StateChangeTimeoutSeconds = 10,
        }), NullLogger<SystemControlService>.Instance);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var outcome = await sut.SetAuthorityModeAsync("manual", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, outcome.Status);
        Assert.Equal("manual", sut.CurrentStatus?.Authority);
    }
}
