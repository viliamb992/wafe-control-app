using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Shared.Services;
using RecuperationSystem.Shared.Services.Http;

namespace RecuperationSystem.Tests.Services;

public class AuthenticationServiceTests
{
    private readonly IWafeApiService _api;
    private readonly ICredentialStore _store;
    private readonly WafeSession _session = new();
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _api = Substitute.For<IWafeApiService>();
        _store = Substitute.For<ICredentialStore>();
        _sut = new AuthenticationService(_api, _store, _session, NullLogger<AuthenticationService>.Instance);
    }

    // ── TryAutoLoginAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task TryAutoLoginAsync_NoStoredCredentials_ReturnsFalse()
    {
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns((ValueTuple<string, string>?)null);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
        await _api.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_StoredCredentials_ApiSucceeds_ReturnsTrue()
    {
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns(("alice", "secret"));
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task TryAutoLoginAsync_StoredCredentials_ApiFails_ReturnsFalse()
    {
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns(("alice", "wrong"));
        _api.AuthenticateAsync("alice", "wrong", Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.TryAutoLoginAsync(TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(_sut.IsAuthenticated);
    }

    // ── LoginAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_RememberMe_SavesCredentials()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.LoginAsync("alice", "secret", rememberMe: true, TestContext.Current.CancellationToken);

        await _store.Received(1).SaveAsync("alice", "secret", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_NoRememberMe_DoesNotSaveCredentials()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_SetsIsAuthenticated()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        Assert.True(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.LoginAsync("alice", "wrong", rememberMe: false, TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_DoesNotSaveCredentials()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.LoginAsync("alice", "wrong", rememberMe: true, TestContext.Current.CancellationToken);

        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_Success_RaisesAuthenticationChangedWithTrue()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        bool? raised = null;
        _sut.AuthenticationChanged += (_, v) => raised = v;

        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        Assert.True(raised);
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_AfterLogin_SetsIsAuthenticatedFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);

        _sut.Logout();

        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task Logout_RaisesAuthenticationChangedWithFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
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
        await _sut.ClearRememberedLoginAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    // ── Session ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SessionExpired_SetsIsAuthenticatedFalse_AndRaisesAuthenticationChanged()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        await _sut.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken);
        bool? raised = null;
        _sut.AuthenticationChanged += (_, v) => raised = v;

        _session.Expire();

        Assert.False(_sut.IsAuthenticated);
        Assert.False(raised);
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
