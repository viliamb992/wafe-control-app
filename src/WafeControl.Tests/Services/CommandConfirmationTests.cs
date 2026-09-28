using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WafeControl.Core.Configuration;
using WafeControl.Core.Services;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Tests.Services;

/// <summary>
/// The command lifecycle: rejected at once, confirmed, or pending and confirmed later; refresh failures counted.
/// </summary>
public class CommandConfirmationTests
{
    private readonly IWafeApiService _api = Substitute.For<IWafeApiService>();
    private readonly IAuthenticationService _auth = Substitute.For<IAuthenticationService>();

    public CommandConfirmationTests() => _auth.IsAuthenticated.Returns(true);

    private SystemControlService CreateSut(int lateConfirmationSeconds = 120, int[]? initialIntervals = null) =>
        new(_api, _auth, Options.Create(new PollingConfiguration
        {
            StateChangeIntervalMs = 10,
            StateChangeInitialIntervalsMs = initialIntervals ?? [],
            StateChangeTimeoutSeconds = 1,
            LateConfirmationSeconds = lateConfirmationSeconds,
        }), NullLogger<SystemControlService>.Instance);

    private static SystemStatus Status(int gen, int boost = 0) => new() { Gen = gen, BoostRemaining = boost, Temperatures = [20.0] };

    [Fact]
    public async Task Rejected_FailsWithTheReason_WithoutWaitingForTheUnit()
    {
        _api.SetBoostAsync(900, Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(ApiError.Offline));

        var outcome = await CreateSut().SetBoostAsync(900, TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Failed, outcome.Status);
        Assert.Equal(ApiError.Offline, outcome.Error);
        await _api.DidNotReceive().GetMainStatusAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotConfirmedInTime_IsPending_AndConfirmedByALaterStatus()
    {
        _api.SetBoostAsync(900, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        _api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(1));
        var sut = CreateSut();
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var outcome = await sut.SetBoostAsync(900, TestContext.Current.CancellationToken);
        Assert.Equal(CommandStatus.Pending, outcome.Status);
        Assert.False(outcome.LateConfirmation.IsCompleted);

        // The regular poll finally sees it.
        _api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(6, boost: 900));
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(await outcome.LateConfirmation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Pending_NeverConfirmed_EndsFalseAfterTheWatch()
    {
        _api.SetBoostAsync(900, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        _api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(1));
        var sut = CreateSut(lateConfirmationSeconds: 1);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var outcome = await sut.SetBoostAsync(900, TestContext.Current.CancellationToken);

        Assert.False(await outcome.LateConfirmation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FirstPolls_UseTheInitialIntervals()
    {
        _api.SetBoostAsync(900, Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        _api.GetMainStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(1), Status(2, boost: 900));
        var sut = CreateSut(initialIntervals: [1]);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);

        var outcome = await sut.SetBoostAsync(900, TestContext.Current.CancellationToken);

        Assert.Equal(CommandStatus.Confirmed, outcome.Status);
    }

    [Fact]
    public async Task RefreshFailures_AreCountedUntilASuccess()
    {
        _api.GetMainStatusAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResult<SystemStatus>.Fail(ApiError.Timeout), ApiResult<SystemStatus>.Fail(ApiError.Offline), Status(3));
        var sut = CreateSut();

        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);
        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, sut.ConsecutiveRefreshFailures);
        Assert.Equal(ApiError.Offline, sut.LastRefreshError);

        await sut.RefreshStatusAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, sut.ConsecutiveRefreshFailures);
        Assert.Equal(ApiError.None, sut.LastRefreshError);
    }
}
