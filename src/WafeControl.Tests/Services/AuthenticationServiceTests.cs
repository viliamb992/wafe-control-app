using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using WafeControl.Core.Services;
using WafeControl.Shared.Services;
using WafeControl.Shared.Services.Http;
using WafeControl.Tests.Localization;

namespace WafeControl.Tests.Services;

public class AuthenticationServiceTests
{
    private readonly IWafeApiService _api;
    private readonly ICredentialStore _store;
    private readonly WafeSession _session = new();
    private readonly InMemorySettingsStore _settings = new();
    private readonly IBiometricAuth _biometric = Substitute.For<IBiometricAuth>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _api = Substitute.For<IWafeApiService>();
        _store = Substitute.For<ICredentialStore>();
        _sut = new AuthenticationService(_api, _store, _session, _settings, _biometric, _time, NullLogger<AuthenticationService>.Instance);
    }

    private DateTimeOffset Now => _time.GetUtcNow();

    private void Remember(DateTimeOffset? savedAt, string password = "secret") =>
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns(new StoredLogin("alice", password, savedAt));

    private void UseAndroid(SignInMethod method = SignInMethod.StaySignedIn, SignInDuration duration = SignInDuration.ThirtyDays)
    {
        _biometric.IsSupported.Returns(true);
        _settings.Settings = new UserSettings { SignInMethod = method, StaySignedInFor = duration };
    }

    // ── TryAutoLoginAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task TryAutoLoginAsync_NoStoredCredentials_ReturnsNone()
    {
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns((StoredLogin?)null);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AutoLoginOutcome.None, result.Outcome);
        Assert.False(_sut.HasRememberedLogin);
        await _api.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_StoredCredentials_ApiSucceeds_ReturnsTrue()
    {
        Remember(savedAt: null);
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.True(_sut.IsAuthenticated);
        Assert.True(_sut.HasRememberedLogin);
    }

    [Fact]
    public async Task TryAutoLoginAsync_StoredCredentials_ApiFails_ReturnsFalse()
    {
        Remember(savedAt: null, password: "wrong");
        _api.AuthenticateAsync("alice", "wrong", Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(ApiError.Unauthorized, 401));

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ApiError.Unauthorized, result.SignIn?.Error);
        Assert.False(_sut.IsAuthenticated);
    }

    // ── Time limit and fingerprint or face (Android) ─────────────────────────

    [Fact]
    public async Task TryAutoLoginAsync_NoTimeLimit_NeverStampsOrChecks()
    {
        // Windows and iOS: StaySignedInFor is null.
        Remember(savedAt: null);
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        await _store.DidNotReceive().SaveAsync(Arg.Any<StoredLogin>(), Arg.Any<CancellationToken>());
        await _biometric.DidNotReceive().AuthenticateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_LegacyLogin_IsStampedWithNowAndSignsIn()
    {
        UseAndroid();
        Remember(savedAt: null);
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        await _store.Received(1).SaveAsync(new StoredLogin("alice", "secret", Now), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_Expired_ForgetsLoginWithoutCallingApi()
    {
        UseAndroid(duration: SignInDuration.OneDay);
        Remember(savedAt: Now - TimeSpan.FromDays(1));

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AutoLoginOutcome.Expired, result.Outcome);
        Assert.Equal("alice", _sut.Username);
        Assert.False(_sut.HasRememberedLogin);
        await _store.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        await _api.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_NotYetExpired_SignsIn()
    {
        UseAndroid(duration: SignInDuration.OneDay);
        Remember(savedAt: Now - TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        await _store.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_Biometric_Succeeded_SignsInWithoutTimeLimit()
    {
        UseAndroid(SignInMethod.Biometric, SignInDuration.OneDay);
        Remember(savedAt: Now - TimeSpan.FromDays(400));
        _biometric.AuthenticateAsync(Arg.Any<CancellationToken>()).Returns(BiometricResult.Succeeded);
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        await _store.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(BiometricResult.UsePassword, AutoLoginOutcome.BiometricDeclined)]
    [InlineData(BiometricResult.LockedOut, AutoLoginOutcome.BiometricLockedOut)]
    public async Task TryAutoLoginAsync_Biometric_NotPassed_KeepsLoginWithoutCallingApi(BiometricResult check, AutoLoginOutcome expected)
    {
        UseAndroid(SignInMethod.Biometric);
        Remember(savedAt: Now);
        _biometric.AuthenticateAsync(Arg.Any<CancellationToken>()).Returns(check);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal("alice", _sut.Username);
        Assert.True(_sut.HasRememberedLogin);
        await _store.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
        await _api.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_Biometric_Removed_FallsBackAndForgetsLogin()
    {
        UseAndroid(SignInMethod.Biometric);
        Remember(savedAt: Now);
        _biometric.AuthenticateAsync(Arg.Any<CancellationToken>()).Returns(BiometricResult.Unavailable);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AutoLoginOutcome.BiometricUnavailable, result.Outcome);
        Assert.Equal(SignInMethod.StaySignedIn, _settings.Settings.SignInMethod);
        await _store.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        await _api.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_BiometricSettingWhereUnsupported_IsIgnored()
    {
        _settings.Settings = new UserSettings { SignInMethod = SignInMethod.Biometric };
        Remember(savedAt: null);
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        await _biometric.DidNotReceive().AuthenticateAsync(Arg.Any<CancellationToken>());
    }

    // ── CheckResumeAsync ─────────────────────────────────────────────────────

    private async Task SignInRememberedAsync()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        await _sut.LoginAsync("alice", "secret", rememberMe: true, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(299, ResumeCheck.None)]
    [InlineData(300, ResumeCheck.Unlock)]
    public async Task CheckResumeAsync_Biometric_LocksAfterFiveMinutes(int seconds, ResumeCheck expected)
    {
        UseAndroid(SignInMethod.Biometric);
        await SignInRememberedAsync();

        var check = await _sut.CheckResumeAsync(TimeSpan.FromSeconds(seconds), TestContext.Current.CancellationToken);

        Assert.Equal(expected, check);
    }

    [Fact]
    public async Task CheckResumeAsync_ExpiredWhileAway_ForgetsLogin()
    {
        UseAndroid(duration: SignInDuration.OneDay);
        await SignInRememberedAsync();
        Remember(savedAt: Now);
        _time.Advance(TimeSpan.FromDays(1));

        var check = await _sut.CheckResumeAsync(TimeSpan.FromDays(1), TestContext.Current.CancellationToken);

        Assert.Equal(ResumeCheck.Expired, check);
        Assert.False(_sut.HasRememberedLogin);
        await _store.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckResumeAsync_StillValid_CarriesOn()
    {
        UseAndroid(duration: SignInDuration.OneDay);
        await SignInRememberedAsync();
        Remember(savedAt: Now);
        _time.Advance(TimeSpan.FromHours(23));

        var check = await _sut.CheckResumeAsync(TimeSpan.FromHours(23), TestContext.Current.CancellationToken);

        Assert.Equal(ResumeCheck.None, check);
        await _store.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckResumeAsync_NothingRemembered_CarriesOn()
    {
        UseAndroid(SignInMethod.Biometric);
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        var check = await _sut.CheckResumeAsync(TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        Assert.Equal(ResumeCheck.None, check);
    }

    [Fact]
    public async Task CheckResumeAsync_Demo_CarriesOn()
    {
        UseAndroid(SignInMethod.Biometric);
        _sut.StartDemo();

        var check = await _sut.CheckResumeAsync(TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        Assert.Equal(ResumeCheck.None, check);
    }

    [Fact]
    public async Task RestartSignInPeriodAsync_StampsTheSavedLoginWithNow()
    {
        Remember(savedAt: Now - TimeSpan.FromDays(60));

        await _sut.RestartSignInPeriodAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).SaveAsync(new StoredLogin("alice", "secret", Now), Arg.Any<CancellationToken>());
    }

    // ── LoginAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_RememberMe_SavesCredentialsWithTheTime()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        await _sut.LoginAsync("alice", "secret", rememberMe: true, TestContext.Current.CancellationToken);

        await _store.Received(1).SaveAsync(new StoredLogin("alice", "secret", Now), Arg.Any<CancellationToken>());
        Assert.True(_sut.HasRememberedLogin);
    }

    [Fact]
    public async Task LoginAsync_RememberMe_LoginIsSavedBeforeAuthenticationChanged()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        bool? rememberedWhenRaised = null;
        _sut.AuthenticationChanged += (_, _) => rememberedWhenRaised = _sut.HasRememberedLogin;

        await _sut.LoginAsync("alice", "secret", rememberMe: true, TestContext.Current.CancellationToken);

        Assert.True(rememberedWhenRaised);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_NoRememberMe_DoesNotSaveCredentials()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        await _store.DidNotReceive().SaveAsync(Arg.Any<StoredLogin>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_SetsIsAuthenticated()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(ApiResult.Success);

        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        Assert.True(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(ApiError.Unauthorized, 401));

        var result = await _sut.LoginAsync("alice", "wrong", rememberMe: false, TestContext.Current.CancellationToken);

        Assert.Equal(ApiError.Unauthorized, result.Error);
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_DoesNotSaveCredentials()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Fail(ApiError.Unauthorized, 401));

        await _sut.LoginAsync("alice", "wrong", rememberMe: true, TestContext.Current.CancellationToken);

        await _store.DidNotReceive().SaveAsync(Arg.Any<StoredLogin>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_Success_RaisesAuthenticationChangedWithTrue()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        bool? raised = null;
        _sut.AuthenticationChanged += (_, v) => raised = v;

        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        Assert.True(raised);
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_AfterLogin_SetsIsAuthenticatedFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        _sut.Logout();

        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task Logout_RaisesAuthenticationChangedWithFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        bool? raised = null;
        _sut.AuthenticationChanged += (_, v) => raised = v;
        _sut.Logout();

        Assert.False(raised);
    }

    // ── ClearRememberedLoginAsync ───────────────────────────────────────────

    [Fact]
    public async Task ClearRememberedLoginAsync_DelegatesToStore()
    {
        await SignInRememberedAsync();

        await _sut.ClearRememberedLoginAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        Assert.False(_sut.HasRememberedLogin);
    }

    // ── Session ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SessionExpired_SetsIsAuthenticatedFalse_AndRaisesAuthenticationChanged()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);
        bool? raised = null;
        _sut.AuthenticationChanged += (_, v) => raised = v;

        _session.Expire();

        Assert.False(_sut.IsAuthenticated);
        Assert.False(raised);
    }

    // ── Demo ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartDemo_SignsInWithoutApiOrCredentialStore()
    {
        _sut.StartDemo();

        Assert.True(_sut.IsAuthenticated);
        Assert.True(_sut.IsDemo);
        Assert.True(_session.IsDemo);
        await _api.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().SaveAsync(Arg.Any<StoredLogin>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Logout_FromDemo_LeavesDemo()
    {
        _sut.StartDemo();

        _sut.Logout();

        Assert.False(_sut.IsAuthenticated);
        Assert.False(_sut.IsDemo);
        Assert.Equal(string.Empty, _sut.Username);
    }

    [Fact]
    public async Task StartDemo_AfterRealSignIn_EndsTheRealSessionFirst()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiResult.Success);
        _session.Start("alice", "secret", "k1");
        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        _sut.StartDemo();

        Assert.True(_sut.IsDemo);
        Assert.Null(_session.Key);
        Assert.Null(_session.Credentials);
    }

    [Fact]
    public void Logout_ClearsSession()
    {
        _session.Start("alice", "secret", "k1");

        _sut.Logout();

        Assert.Null(_session.Key);
        Assert.Null(_session.Credentials);
    }
}
