using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using WafeControl.Core.Configuration;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels;
using WafeControl.Tests.Localization;

namespace WafeControl.Tests.ViewModels;

// ─── Sign-in form and resume (AppViewModel) ────────────────────────────────────

public sealed class AppViewModelSignInTests : IDisposable
{
    private readonly IAuthenticationService _auth = Substitute.For<IAuthenticationService>();
    private readonly InMemorySettingsStore _settings = new();
    private readonly FakeTimeProvider _time = new();
    private readonly AppViewModel _app;

    public AppViewModelSignInTests()
    {
        _auth.Username.Returns("alice@example.com");
        _app = new AppViewModel(_auth, new FakeSystemControlService(), Options.Create(new PollingConfiguration()),
            NullLoggerFactory.Instance, new FakeNetworkStatus(), _time, _settings);
    }

    public void Dispose() => _app.Dispose();

    private void SignIn()
    {
        _auth.IsAuthenticated.Returns(true);
        _auth.AuthenticationChanged += Raise.Event<EventHandler<bool>>(_auth, true);
    }

    [Fact]
    public void RememberMeText_NoTimeLimit_IsTheUsualLabel()
    {
        Assert.Equal("Keep me signed in", _app.RememberMeText);
    }

    [Fact]
    public void RememberMeText_SaysHowLongOrHow()
    {
        _settings.Settings = new UserSettings { StaySignedInFor = SignInDuration.ThirtyDays };
        Assert.Equal("Stay signed in for 30 days", _app.RememberMeText);

        _settings.Settings = _settings.Settings with { SignInMethod = SignInMethod.Biometric };
        Assert.Equal("Sign in with fingerprint or face next time", _app.RememberMeText);
    }

    [Fact]
    public async Task StartAsync_Expired_FormHasEmailAndSaysWhy()
    {
        _settings.Settings = new UserSettings { StaySignedInFor = SignInDuration.TwoWeeks };
        _auth.TryAutoLoginAsync(Arg.Any<CancellationToken>()).Returns(new AutoLoginResult(AutoLoginOutcome.Expired));

        await _app.StartAsync();

        Assert.True(_app.IsLoginRequired);
        Assert.Equal("alice@example.com", _app.Login.Username);
        Assert.Equal("Your sign-in expired after 14 days. Enter your password again.", _app.Login.ErrorMessage);
    }

    [Theory]
    [InlineData(AutoLoginOutcome.BiometricDeclined, null)]
    [InlineData(AutoLoginOutcome.BiometricLockedOut, "Too many attempts. Enter your password.")]
    [InlineData(AutoLoginOutcome.BiometricUnavailable, "Fingerprint or face is no longer set up on this phone. Enter your password.")]
    public async Task StartAsync_BiometricNotPassed_FormHasEmail(AutoLoginOutcome outcome, string? message)
    {
        _auth.TryAutoLoginAsync(Arg.Any<CancellationToken>()).Returns(new AutoLoginResult(outcome));

        await _app.StartAsync();

        Assert.True(_app.IsLoginRequired);
        Assert.Equal("alice@example.com", _app.Login.Username);
        Assert.Equal(message, _app.Login.ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_NothingRemembered_LeavesTheFormEmpty()
    {
        _auth.TryAutoLoginAsync(Arg.Any<CancellationToken>()).Returns(AutoLoginResult.None);

        await _app.StartAsync();

        Assert.Equal(string.Empty, _app.Login.Username);
        Assert.Null(_app.Login.ErrorMessage);
    }

    [Fact]
    public async Task ResumeAsync_PassesTheTimeInTheBackground()
    {
        SignIn();

        _app.Pause();
        _time.Advance(TimeSpan.FromMinutes(7));
        await _app.ResumeAsync();

        await _auth.Received(1).CheckResumeAsync(TimeSpan.FromMinutes(7), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResumeAsync_Expired_SignsOutWithTheReason()
    {
        _settings.Settings = new UserSettings { StaySignedInFor = SignInDuration.OneDay };
        SignIn();
        _auth.CheckResumeAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(ResumeCheck.Expired);

        _app.Pause();
        await _app.ResumeAsync();

        _auth.Received(1).Logout();
        Assert.Equal("alice@example.com", _app.Login.Username);
        Assert.Equal("Your sign-in expired after 1 day. Enter your password again.", _app.Login.ErrorMessage);
    }

    [Fact]
    public async Task ResumeAsync_Unlock_SignsOutAndSignsInAgainAsOnStart()
    {
        SignIn();
        _auth.CheckResumeAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(ResumeCheck.Unlock);
        _auth.TryAutoLoginAsync(Arg.Any<CancellationToken>()).Returns(new AutoLoginResult(AutoLoginOutcome.BiometricDeclined));

        _app.Pause();
        await _app.ResumeAsync();

        _auth.Received(1).Logout();
        await _auth.Received(1).TryAutoLoginAsync(Arg.Any<CancellationToken>());
    }
}

// ─── Settings → Sign-in and the biometric offer (SettingsViewModel) ─────────────

public class SettingsViewModelSignInTests
{
    private readonly ILocalizationService _localization = Substitute.For<ILocalizationService>();
    private readonly InMemorySettingsStore _store = new();
    private readonly IStartupRegistration _startup = Substitute.For<IStartupRegistration>();
    private readonly IBiometricAuth _biometric = Substitute.For<IBiometricAuth>();
    private readonly IAuthenticationService _auth = Substitute.For<IAuthenticationService>();

    public SettingsViewModelSignInTests()
    {
        _localization.Languages.Returns([LocalizationService.Czech, LocalizationService.Slovak, LocalizationService.English]);
        _localization.Current.Returns(LocalizationService.Czech);
        _biometric.IsSupported.Returns(true);
        _biometric.Availability.Returns(BiometricAvailability.Available);
        _auth.HasRememberedLogin.Returns(true);
        _store.Settings = new UserSettings { StaySignedInFor = SignInDuration.ThirtyDays };
    }

    private SettingsViewModel Create() => new(_localization, _store, _startup, biometric: _biometric, authentication: _auth);

    private void Prompt(BiometricResult result) =>
        _biometric.AuthenticateAsync(Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public void Card_HiddenWhereUnsupported()
    {
        _biometric.IsSupported.Returns(false);

        Assert.False(Create().IsSignInCardVisible);
        Assert.False(new SettingsViewModel(_localization, _store, _startup).IsSignInCardVisible);
    }

    [Fact]
    public void Duration_IsSaved()
    {
        var sut = Create();
        Assert.Equal(2, sut.StaySignedInForIndex);

        sut.StaySignedInForIndex = 0;

        Assert.Equal(SignInDuration.OneDay, _store.Settings.StaySignedInFor);
    }

    [Fact]
    public async Task SwitchToBiometric_PromptPasses_IsSaved()
    {
        Prompt(BiometricResult.Succeeded);
        var sut = Create();

        Assert.True(await sut.SetSignInMethodAsync(SignInMethod.Biometric));

        Assert.Equal(SignInMethod.Biometric, sut.SignInMethod);
        Assert.Equal(SignInMethod.Biometric, _store.Settings.SignInMethod);
    }

    [Theory]
    [InlineData(BiometricResult.UsePassword)]
    [InlineData(BiometricResult.LockedOut)]
    public async Task SwitchToBiometric_PromptFails_StaysAsItWas(BiometricResult result)
    {
        Prompt(result);
        var sut = Create();

        Assert.False(await sut.SetSignInMethodAsync(SignInMethod.Biometric));

        Assert.Equal(SignInMethod.StaySignedIn, sut.SignInMethod);
        Assert.Equal(SignInMethod.StaySignedIn, _store.Settings.SignInMethod);
    }

    [Fact]
    public async Task SwitchToBiometric_NotSetUp_IsRefusedWithReason()
    {
        _biometric.Availability.Returns(BiometricAvailability.NotEnrolled);
        var sut = Create();

        Assert.False(await sut.SetSignInMethodAsync(SignInMethod.Biometric));

        Assert.False(sut.CanChooseBiometric);
        Assert.Equal("Set up a fingerprint or face in Android settings first.", sut.BiometricUnavailableReason);
        await _biometric.DidNotReceive().AuthenticateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchAwayFromBiometric_NeedsThePromptToo()
    {
        _store.Settings = _store.Settings with { SignInMethod = SignInMethod.Biometric };
        Prompt(BiometricResult.UsePassword);
        var sut = Create();

        Assert.False(await sut.SetSignInMethodAsync(SignInMethod.StaySignedIn));

        Assert.Equal(SignInMethod.Biometric, _store.Settings.SignInMethod);
        await _auth.DidNotReceive().RestartSignInPeriodAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchAwayFromBiometric_RestartsTheTimeLimit()
    {
        _store.Settings = _store.Settings with { SignInMethod = SignInMethod.Biometric };
        Prompt(BiometricResult.Succeeded);
        var sut = Create();

        Assert.True(await sut.SetSignInMethodAsync(SignInMethod.StaySignedIn));

        Assert.Equal(SignInMethod.StaySignedIn, _store.Settings.SignInMethod);
        await _auth.Received(1).RestartSignInPeriodAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Offer_DueForSettingsFromAnOlderVersion()
    {
        // No BiometricOfferAnswered saved: new users and users updating from an earlier version alike.
        Assert.True(Create().NeedsBiometricOffer);
    }

    [Fact]
    public void Offer_NotDue_WhenAnswered()
    {
        _store.Settings = _store.Settings with { BiometricOfferAnswered = true };

        Assert.False(Create().NeedsBiometricOffer);
    }

    [Fact]
    public void Offer_NotDue_WhenNotSetUp()
    {
        _biometric.Availability.Returns(BiometricAvailability.NotEnrolled);

        Assert.False(Create().NeedsBiometricOffer);
    }

    [Fact]
    public void Offer_NotDue_WhenNoLoginSaved()
    {
        _auth.HasRememberedLogin.Returns(false);

        Assert.False(Create().NeedsBiometricOffer);
    }

    [Fact]
    public void Offer_NotDue_InDemo()
    {
        _auth.IsDemo.Returns(true);

        Assert.False(Create().NeedsBiometricOffer);
    }

    [Fact]
    public async Task Offer_Accepted_TurnsBiometricOn()
    {
        Prompt(BiometricResult.Succeeded);
        var sut = Create();

        Assert.True(await sut.AcceptBiometricOfferAsync());

        Assert.Equal(SignInMethod.Biometric, _store.Settings.SignInMethod);
        Assert.True(_store.Settings.BiometricOfferAnswered);
        Assert.False(sut.NeedsBiometricOffer);
    }

    [Fact]
    public async Task Offer_AcceptedButPromptFails_StaysSignedInAndCountsAsAnswered()
    {
        Prompt(BiometricResult.UsePassword);
        var sut = Create();

        Assert.False(await sut.AcceptBiometricOfferAsync());

        Assert.Equal(SignInMethod.StaySignedIn, _store.Settings.SignInMethod);
        Assert.True(_store.Settings.BiometricOfferAnswered);
    }

    [Fact]
    public void Offer_Declined_IsAnswered()
    {
        var sut = Create();

        sut.DeclineBiometricOffer();

        Assert.True(_store.Settings.BiometricOfferAnswered);
        Assert.Equal(SignInMethod.StaySignedIn, _store.Settings.SignInMethod);
        Assert.False(sut.NeedsBiometricOffer);
    }
}
