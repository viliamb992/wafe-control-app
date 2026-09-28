using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WafeControl.Core.Configuration;
using WafeControl.Core.Services;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Tests.Services;

public class SystemControlServiceTests
{
    // Fast polling config so tests don't wait on real-world intervals.
    private static IOptions<PollingConfiguration> FastConfig(int timeoutSeconds = 2) =>
        Options.Create(new PollingConfiguration
        {
            StatusRefreshIntervalMs = 10,
            StateChangeIntervalMs = 10,
            StateChangeTimeoutSeconds = timeoutSeconds
        });

    private static SystemControlService CreateSut(IWafeApiService api, IAuthenticationService auth, int timeoutSeconds = 2)
        => new(api, auth, FastConfig(timeoutSeconds), NullLogger<SystemControlService>.Instance);

    private static SystemStatus MakeStatus(int gen, bool stopActive = false,
        string authority = "intelligent", bool silentActive = false,
        bool holidayActive = false, int boostRemaining = 0, int flowRequested = 100)
        => new()
        {
            Gen = gen,
            StopActive = stopActive,
            Authority = authority,
            SilentActive = silentActive,
            HolidayActive = holidayActive,
            BoostRemaining = boostRemaining,
            FlowRequested = flowRequested,
            Temperatures = [20.0]
        };

    // ── RefreshStatusAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RefreshStatusAsync_WhenAuthenticated_UpdatesCurrentStatus()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        var status = MakeStatus(1);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var sut = CreateSut(api, auth);
        var result = await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(status, result);
        Assert.Equal(status, sut.CurrentStatus);
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenNotAuthenticated_ReturnsCurrentStatusWithoutApiCall()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(false);

        var sut = CreateSut(api, auth);
        var result = await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
        await api.DidNotReceive().GetMainStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshStatusAsync_RaisesStatusUpdatedEvent()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(MakeStatus(1));

        var sut = CreateSut(api, auth);
        SystemStatus? raised = null;
        sut.StatusUpdated += (_, s) => raised = s;

        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(raised);
    }

    // ── SetFlowSpeedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SetFlowSpeedAsync_StateConfirmedByPoll_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetFlowSpeedAsync(150, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        // Initial status, then updated status with new gen + new flow value.
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, flowRequested: 100),
                    MakeStatus(gen: 2, flowRequested: 150));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken); // prime with gen=1

        var result = await sut.SetFlowSpeedAsync(150, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, result.Status);
    }

    [Fact]
    public async Task SetFlowSpeedAsync_StateNeverConfirmed_ReturnsFalse()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetFlowSpeedAsync(150, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        // Gen never changes → timeout.
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, flowRequested: 100));

        var sut = CreateSut(api, auth, timeoutSeconds: 1);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.SetFlowSpeedAsync(150, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Pending, result.Status);
    }

    // ── SetAuthorityModeAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SetAuthorityModeAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetAuthorityModeAsync("manual", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, authority: "intelligent"),
                    MakeStatus(gen: 2, authority: "manual"));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.SetAuthorityModeAsync("manual", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, result.Status);
    }

    // ── SetSilentModeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SetSilentModeAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetSilentModeAsync(true, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, silentActive: false),
                    MakeStatus(gen: 2, silentActive: true));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.SetSilentModeAsync(true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, result.Status);
    }

    // ── SetHolidayModeAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SetHolidayModeAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetHolidayModeAsync(true, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, holidayActive: false),
                    MakeStatus(gen: 2, holidayActive: true));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.SetHolidayModeAsync(true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, result.Status);
    }

    // ── SetBoostAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetBoostAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetBoostAsync(900, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, boostRemaining: 0),
                    MakeStatus(gen: 2, boostRemaining: 900));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.SetBoostAsync(900, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, result.Status);
    }

    // ── StartSystemAsync / StopSystemAsync ─────────────────────────────────

    [Fact]
    public async Task StartSystemAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetStopActiveAsync(false, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        // Initial: stopped (stopActive=true, gen=1) → confirmed: running (stopActive=false, gen=2)
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, stopActive: true),
                    MakeStatus(gen: 2, stopActive: false));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.StartSystemAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, result.Status);
    }

    [Fact]
    public async Task StopSystemAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetStopActiveAsync(true, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, stopActive: false),
                    MakeStatus(gen: 2, stopActive: true));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.StopSystemAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, result.Status);
    }

    [Fact]
    public async Task StartSystemAsync_NotConfirmed_ReturnsFalse()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetStopActiveAsync(false, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        // Gen never changes → timeout
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, stopActive: true));

        var sut = CreateSut(api, auth, timeoutSeconds: 1);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.StartSystemAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Pending, result.Status);
    }

    // ── IsSystemOnline ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsSystemOnline_WithTemperatureData_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        var status = MakeStatus(gen: 1);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(sut.IsSystemOnline);
    }

    [Fact]
    public async Task IsSystemOnline_WithNoTemperatureData_ReturnsFalse()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        var status = MakeStatus(gen: 1);
        status.Temperatures = null;
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.False(sut.IsSystemOnline);
    }

    [Theory]
    [InlineData(true, null, true)]      // online, but stopped or between scheduled runs
    [InlineData(false, 20.0, false)]    // the header's flag wins over stale readings
    public async Task IsSystemOnline_FollowsHeader(bool headerOnline, double? temperature, bool expected)
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        var status = MakeStatus(gen: 1);
        status.Temperatures = [temperature];
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(status);
        api.GetHeaderInfoAsync(Arg.Any<CancellationToken>()).Returns(new HeaderInfo { Online = headerOnline });

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, sut.IsSystemOnline);
        Assert.Equal(temperature.HasValue, sut.HasSensorData);
    }

    [Fact]
    public async Task RefreshStatusAsync_OnlyHeaderArrives_RaisesStatusUpdatedWithLastStatus()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        var status = MakeStatus(gen: 1);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(status, (SystemStatus?)null);
        api.GetHeaderInfoAsync(Arg.Any<CancellationToken>()).Returns(new HeaderInfo { Online = true }, new HeaderInfo { Online = false });

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);
        SystemStatus? raised = null;
        sut.StatusUpdated += (_, s) => raised = s;

        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.Same(status, raised);
        Assert.False(sut.IsSystemOnline);
    }

    [Fact]
    public async Task SetFlowSpeedAsync_WhilePolling_DoesNotFetchHeader()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetFlowSpeedAsync(150, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, flowRequested: 100),
                    MakeStatus(gen: 1, flowRequested: 100),
                    MakeStatus(gen: 2, flowRequested: 150));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, (await sut.SetFlowSpeedAsync(150, cancellationToken: TestContext.Current.CancellationToken)).Status);
        await api.Received(3).GetMainStatusAsync(Arg.Any<CancellationToken>());
        await api.Received(1).GetHeaderInfoAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetUnitNameAsync_Accepted_RefreshesHeader()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetUnitNameAsync("Chata", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(MakeStatus(gen: 1));
        api.GetHeaderInfoAsync(Arg.Any<CancellationToken>()).Returns(new HeaderInfo { Name = "Chata" });

        var sut = CreateSut(api, auth);

        Assert.True((await sut.SetUnitNameAsync("Chata", TestContext.Current.CancellationToken)).Ok);
        Assert.Equal("Chata", sut.CurrentHeader?.Name);
    }

    [Fact]
    public async Task SetUnitNameAsync_Rejected_ReturnsFalseWithoutRefresh()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetUnitNameAsync("Chata", Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(ApiError.Rejected, 400));

        var sut = CreateSut(api, auth);

        Assert.False((await sut.SetUnitNameAsync("Chata", TestContext.Current.CancellationToken)).Ok);
        await api.DidNotReceive().GetHeaderInfoAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSystemInfoAsync_WhenNotAuthenticated_SkipsApi()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(false);

        var result = await CreateSut(api, auth).GetSystemInfoAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
        await api.DidNotReceive().GetSystemInfoAsync(Arg.Any<CancellationToken>());
    }

    // ── Failure handling ───────────────────────────────────────────────────

    [Fact]
    public async Task SetFlowSpeedAsync_CommandRejected_ReturnsFalseWithoutPolling()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetFlowSpeedAsync(150, Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(ApiError.Rejected, 400));
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(MakeStatus(gen: 1));

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.SetFlowSpeedAsync(150, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Failed, result.Status);
        await api.Received(1).GetMainStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshStatusAsync_FetchFails_KeepsLastKnownStatus()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        var status = MakeStatus(gen: 1);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(status, (SystemStatus?)null);

        var sut = CreateSut(api, auth);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);
        var result = await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.Same(status, result);
        Assert.Same(status, sut.CurrentStatus);
        Assert.True(sut.IsSystemOnline);
    }

    [Fact]
    public async Task StopSystemAsync_FetchFailsWhilePolling_IsNotConfirmed()
    {
        // Regression: a failed fetch used to null the status, and "StopActive ?? true" then read as confirmed.
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetStopActiveAsync(true, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(MakeStatus(gen: 1, stopActive: false), (SystemStatus?)null);

        var sut = CreateSut(api, auth, timeoutSeconds: 1);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var result = await sut.StopSystemAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Pending, result.Status);
    }
}
