using Microsoft.Extensions.Options;
using NSubstitute;
using RecuperationSystem.Desktop.Configuration;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Services;

namespace RecuperationSystem.Tests.Services;

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

        var sut = new SystemControlService(api, auth, FastConfig());
        var result = await sut.RefreshStatusAsync();

        Assert.Equal(status, result);
        Assert.Equal(status, sut.CurrentStatus);
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenNotAuthenticated_ReturnsCurrentStatusWithoutApiCall()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(false);

        var sut = new SystemControlService(api, auth, FastConfig());
        var result = await sut.RefreshStatusAsync();

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

        var sut = new SystemControlService(api, auth, FastConfig());
        SystemStatus? raised = null;
        sut.StatusUpdated += (_, s) => raised = s;

        await sut.RefreshStatusAsync();

        Assert.NotNull(raised);
    }

    // ── SetFlowSpeedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SetFlowSpeedAsync_StateConfirmedByPoll_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetFlowSpeedAsync(150, Arg.Any<CancellationToken>()).Returns(true);

        // Initial status, then updated status with new gen + new flow value.
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, flowRequested: 100),
                    MakeStatus(gen: 2, flowRequested: 150));

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync(); // prime with gen=1

        var result = await sut.SetFlowSpeedAsync(150);

        Assert.True(result);
    }

    [Fact]
    public async Task SetFlowSpeedAsync_StateNeverConfirmed_ReturnsFalse()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetFlowSpeedAsync(150, Arg.Any<CancellationToken>()).Returns(true);

        // Gen never changes → timeout.
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, flowRequested: 100));

        var sut = new SystemControlService(api, auth, FastConfig(timeoutSeconds: 1));
        await sut.RefreshStatusAsync();

        var result = await sut.SetFlowSpeedAsync(150);

        Assert.False(result);
    }

    // ── SetAuthorityModeAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SetAuthorityModeAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetAuthorityModeAsync("manual", Arg.Any<CancellationToken>()).Returns(true);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, authority: "intelligent"),
                    MakeStatus(gen: 2, authority: "manual"));

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

        var result = await sut.SetAuthorityModeAsync("manual");

        Assert.True(result);
    }

    // ── SetSilentModeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SetSilentModeAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetSilentModeAsync(true, Arg.Any<CancellationToken>()).Returns(true);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, silentActive: false),
                    MakeStatus(gen: 2, silentActive: true));

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

        var result = await sut.SetSilentModeAsync(true);

        Assert.True(result);
    }

    // ── SetHolidayModeAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SetHolidayModeAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetHolidayModeAsync(true, Arg.Any<CancellationToken>()).Returns(true);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, holidayActive: false),
                    MakeStatus(gen: 2, holidayActive: true));

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

        var result = await sut.SetHolidayModeAsync(true);

        Assert.True(result);
    }

    // ── SetBoostAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetBoostAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetBoostAsync(900, Arg.Any<CancellationToken>()).Returns(true);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, boostRemaining: 0),
                    MakeStatus(gen: 2, boostRemaining: 900));

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

        var result = await sut.SetBoostAsync(900);

        Assert.True(result);
    }

    // ── StartSystemAsync / StopSystemAsync ─────────────────────────────────

    [Fact]
    public async Task StartSystemAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetStopActiveAsync(false, Arg.Any<CancellationToken>()).Returns(true);

        // Initial: stopped (stopActive=true, gen=1) → confirmed: running (stopActive=false, gen=2)
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, stopActive: true),
                    MakeStatus(gen: 2, stopActive: false));

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

        var result = await sut.StartSystemAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task StopSystemAsync_Confirmed_ReturnsTrue()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetStopActiveAsync(true, Arg.Any<CancellationToken>()).Returns(true);

        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, stopActive: false),
                    MakeStatus(gen: 2, stopActive: true));

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

        var result = await sut.StopSystemAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task StartSystemAsync_NotConfirmed_ReturnsFalse()
    {
        var api = Substitute.For<IWafeApiService>();
        var auth = Substitute.For<IAuthenticationService>();
        auth.IsAuthenticated.Returns(true);
        api.SetStopActiveAsync(false, Arg.Any<CancellationToken>()).Returns(true);

        // Gen never changes → timeout
        api.GetMainStatusAsync(Arg.Any<CancellationToken>())
           .Returns(MakeStatus(gen: 1, stopActive: true));

        var sut = new SystemControlService(api, auth, FastConfig(timeoutSeconds: 1));
        await sut.RefreshStatusAsync();

        var result = await sut.StartSystemAsync();

        Assert.False(result);
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

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

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

        var sut = new SystemControlService(api, auth, FastConfig());
        await sut.RefreshStatusAsync();

        Assert.False(sut.IsSystemOnline);
    }
}
