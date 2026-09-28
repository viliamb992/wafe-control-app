using NSubstitute;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Tests.ViewModels;

// ─── Command feedback ───────────────────────────────────────────────────────

public class CommandFeedbackTests : CardViewModelTestBase
{
    public CommandFeedbackTests()
    {
        SignIn();
        RaiseStatusUpdated(new SystemStatus { Authority = "intelligent", SilentActive = false, FlowRequested = 100 });
    }

    [Fact]
    public async Task Confirmed_ShowsSuccess()
    {
        await App.BoostMode.SetBoostCommand.ExecuteAsync("900");

        Assert.Equal(FeedbackKind.Success, App.Feedback?.Kind);
        Assert.Equal("Boost activated for 15 minutes", App.Feedback?.Text);
    }

    [Fact]
    public async Task Failed_Offline_ExplainsAndOffersRetry()
    {
        SystemControlFake.NextOutcome = CommandOutcome.Failed(ApiError.Offline);

        await App.BoostMode.SetBoostCommand.ExecuteAsync("900");

        Assert.Equal(FeedbackKind.Error, App.Feedback?.Kind);
        Assert.Equal("Couldn't change the boost. Can't connect to the Wafe server. Check your internet connection.", App.Feedback?.Text);
        Assert.NotNull(App.Feedback?.Retry);
    }

    [Fact]
    public async Task Failed_Rejected_HasNoRetry()
    {
        SystemControlFake.NextOutcome = CommandOutcome.Failed(ApiError.Rejected);

        await App.BoostMode.SetBoostCommand.ExecuteAsync("900");

        Assert.Null(App.Feedback?.Retry);
        Assert.False(App.Feedback?.OffersReport);
    }

    [Fact]
    public async Task Retry_SendsTheSameCommandAgain()
    {
        SystemControlFake.NextOutcome = CommandOutcome.Failed(ApiError.Timeout);
        await App.BoostMode.SetBoostCommand.ExecuteAsync("1800");
        SystemControlFake.NextOutcome = CommandOutcome.Confirmed;

        await App.Feedback!.Retry!.ExecuteAsync(null);

        Assert.Equal(["boost:1800", "boost:1800"], SentCommands);
        Assert.Equal(FeedbackKind.Success, App.Feedback?.Kind);
    }

    [Fact]
    public async Task NotConfirmedInTime_SaysNothing_UntilTheUnitConfirms()
    {
        var late = new TaskCompletionSource<bool>();
        SystemControlFake.NextOutcome = CommandOutcome.Pending(late.Task);

        await App.BoostMode.SetBoostCommand.ExecuteAsync("900");

        Assert.Null(App.Feedback);

        late.SetResult(true);
        await WaitUntil(() => App.Feedback?.Kind == FeedbackKind.Success);
        Assert.Equal("Boost activated for 15 minutes", App.Feedback?.Text);
    }

    [Fact]
    public async Task NeverConfirmed_StaysQuiet()
    {
        var late = new TaskCompletionSource<bool>();
        SystemControlFake.NextOutcome = CommandOutcome.Pending(late.Task);
        await App.BoostMode.SetBoostCommand.ExecuteAsync("900");

        late.SetResult(false);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Null(App.Feedback);
    }

    [Fact]
    public async Task ModeChange_Failed_GoesBackToTheUnitsMode()
    {
        SystemControlFake.NextOutcome = CommandOutcome.Failed(ApiError.ServerError);

        await App.OperatingMode.ChangeModeAsync("manual");

        Assert.Equal("intelligent", App.OperatingMode.SelectedMode);
        Assert.Equal(["mode:manual"], SentCommands);
    }

    [Fact]
    public async Task SilentMode_Failed_SwitchFollowsTheUnit()
    {
        SystemControlFake.NextOutcome = CommandOutcome.Failed(ApiError.ServerError);
        var raised = new List<string?>();
        App.SpecialModes.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        await App.SpecialModes.SetSilentModeCommand.ExecuteAsync(true);

        Assert.False(App.SpecialModes.IsSilentMode);
        // Raised even though the value didn't change, so a switch the user flipped goes back.
        Assert.Contains("IsSilentMode", raised);
    }

    [Fact]
    public async Task Sending_IsFollowedByTheResult()
    {
        var shown = new List<string?>();
        App.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppViewModel.Feedback))
                shown.Add(App.Feedback?.Text);
        };

        await App.BoostMode.SetBoostCommand.ExecuteAsync("0");

        Assert.Equal(["Stopping boost…", "Boost stopped"], shown);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);
    }
}

// ─── Data state ─────────────────────────────────────────────────────────────

public class DataStateTests : CardViewModelTestBase
{
    public DataStateTests() => SignIn();

    [Fact]
    public void FreshOnlineData_IsLive()
    {
        SetHeader(new HeaderInfo { Online = true, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

        RaiseStatusUpdated(new SystemStatus { Temperatures = [20.0] });

        Assert.Equal(DataState.Live, App.DataState);
        Assert.Null(App.DataBannerTitle);
        Assert.True(App.CanSendCommands);
    }

    [Fact]
    public void OldData_IsStale()
    {
        SetHeader(new HeaderInfo { Online = true, Timestamp = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds() });

        RaiseStatusUpdated(new SystemStatus { Temperatures = [20.0] });

        Assert.Equal(DataState.Stale, App.DataState);
        Assert.Equal("Data 6 min old", App.DataStateText);
        Assert.Equal("The unit last reported 6 min ago.", App.DataBannerMessage);
        Assert.False(App.IsDataCurrent);
        Assert.True(App.CanSendCommands);
    }

    [Fact]
    public void HeaderOffline_IsUnitOffline()
    {
        SetHeader(new HeaderInfo { Online = false, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

        RaiseStatusUpdated(new SystemStatus());

        Assert.Equal(DataState.UnitOffline, App.DataState);
        Assert.True(App.IsDataBannerError);
    }

    [Fact]
    public async Task RepeatedFailures_AreServerUnreachable_AndBlockCommands()
    {
        RaiseStatusUpdated(new SystemStatus { Temperatures = [20.0] });
        SystemControlFake.ConsecutiveRefreshFailures = 3;
        SystemControlFake.LastRefreshError = ApiError.Timeout;

        await App.RefreshStatusCommand.ExecuteAsync(null);

        Assert.Equal(DataState.ServerUnreachable, App.DataState);
        Assert.Equal("No connection", App.DataStateText);
        Assert.False(App.CanSendCommands);
        Assert.False(App.BoostMode.SetBoostCommand.CanExecute("900"));
        // A manual refresh says why it failed.
        Assert.Equal("Couldn't refresh. The Wafe server didn't answer in time. Try again.", App.Feedback?.Text);
    }

    [Fact]
    public async Task NoNetwork_AfterAFailure_IsNoInternet()
    {
        RaiseStatusUpdated(new SystemStatus { Temperatures = [20.0] });
        SystemControlFake.ConsecutiveRefreshFailures = 1;
        Network.IsAvailable = false;

        Network.Raise();
        await Task.Yield();

        Assert.Equal(DataState.NoInternet, App.DataState);
        Assert.Equal("No internet connection", App.DataBannerTitle);
    }

    [Fact]
    public void NoNetwork_ButTheApiAnswers_StaysLive()
    {
        Network.IsAvailable = false;

        RaiseStatusUpdated(new SystemStatus { Temperatures = [20.0] });

        Assert.Equal(DataState.Live, App.DataState);
    }

    [Theory]
    [InlineData(0, 3000)]
    [InlineData(2, 3000)]
    [InlineData(3, 10_000)]
    [InlineData(4, 30_000)]
    [InlineData(5, 60_000)]
    [InlineData(20, 60_000)]
    public void PollDelay_BacksOffWhileUnreachable(int failures, int expectedMs)
    {
        SystemControlFake.ConsecutiveRefreshFailures = failures;

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), App.NextPollDelay());
    }
}

// ─── Sign-in and demo ───────────────────────────────────────────────────────

public class SignInTests : CardViewModelTestBase
{
    [Theory]
    [InlineData(ApiError.Unauthorized, "Invalid username or password.")]
    [InlineData(ApiError.Offline, "Can't connect to the Wafe server. Check your internet connection.")]
    [InlineData(ApiError.ServerError, "The Wafe server has a problem right now. Try again later.")]
    public async Task FailedSignIn_SaysWhy(ApiError error, string message)
    {
        AuthService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(error));
        App.Login.Username = "alice@example.com";
        App.Login.Password = "secret";

        await App.SubmitLoginCommand.ExecuteAsync(null);

        Assert.Equal(message, App.Login.ErrorMessage);
    }

    [Fact]
    public async Task SignIn_TrimsTheEmail()
    {
        AuthService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        App.Login.Username = "  alice@example.com ";
        App.Login.Password = "secret";

        await App.SubmitLoginCommand.ExecuteAsync(null);

        await AuthService.Received(1).LoginAsync("alice@example.com", "secret", true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RememberedLoginFails_FormShowsWhyAndTheEmail()
    {
        AuthService.TryAutoLoginAsync(Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(ApiError.Offline));
        AuthService.Username.Returns("alice@example.com");

        await App.StartAsync();

        Assert.True(App.IsLoginRequired);
        Assert.Equal("alice@example.com", App.Login.Username);
        Assert.Equal("Can't connect to the Wafe server. Check your internet connection.", App.Login.ErrorMessage);
    }

    [Fact]
    public void TryDemo_StartsTheDemo()
    {
        App.TryDemoCommand.Execute(null);

        AuthService.Received(1).StartDemo();
    }

    [Fact]
    public async Task SignOut_InDemo_KeepsTheRememberedLogin()
    {
        AuthService.IsDemo.Returns(true);
        SignIn();
        Assert.True(App.IsDemo);

        await App.SignOutCommand.ExecuteAsync(null);

        await AuthService.DidNotReceive().ClearRememberedLoginAsync(Arg.Any<CancellationToken>());
        AuthService.Received(1).Logout();
    }
}
