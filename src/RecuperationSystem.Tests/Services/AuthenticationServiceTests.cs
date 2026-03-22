using Microsoft.Extensions.Options;
using NSubstitute;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Shared.Services;

namespace RecuperationSystem.Tests.Services;

public class AuthenticationServiceTests
{
    private readonly IWafeApiService _api;
    private readonly ICredentialStore _store;
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _api = Substitute.For<IWafeApiService>();
        _store = Substitute.For<ICredentialStore>();
        _sut = new AuthenticationService(_api, _store);
    }

    // ── TryAutoLoginAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task TryAutoLoginAsync_NoStoredCredentials_ReturnsFalse()
    {
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns((ValueTuple<string, string>?)null);

        var result = await _sut.TryAutoLoginAsync();

        Assert.False(result);
        await _api.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task TryAutoLoginAsync_StoredCredentials_ApiSucceeds_ReturnsTrue()
    {
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns(("alice", "secret"));
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.TryAutoLoginAsync();

        Assert.True(result);
        Assert.True(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task TryAutoLoginAsync_StoredCredentials_ApiFails_ReturnsFalse()
    {
        _store.TryGetAsync(Arg.Any<CancellationToken>()).Returns(("alice", "wrong"));
        _api.AuthenticateAsync("alice", "wrong", Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.TryAutoLoginAsync();

        Assert.False(result);
        Assert.False(_sut.IsAuthenticated);
    }

    // ── LoginAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_RememberMe_SavesCredentials()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.LoginAsync("alice", "secret", rememberMe: true);

        await _store.Received(1).SaveAsync("alice", "secret", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_NoRememberMe_DoesNotSaveCredentials()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.LoginAsync("alice", "secret", rememberMe: false);

        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_SetsIsAuthenticated()
    {
        _api.AuthenticateAsync("alice", "secret", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.LoginAsync("alice", "secret", rememberMe: false);

        Assert.True(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.LoginAsync("alice", "wrong", rememberMe: false);

        Assert.False(result);
        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_DoesNotSaveCredentials()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.LoginAsync("alice", "wrong", rememberMe: true);

        await _store.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_Success_RaisesAuthenticationChangedWithTrue()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        bool? raised = null;
        _sut.AuthenticationChanged += (_, v) => raised = v;

        await _sut.LoginAsync("alice", "secret", rememberMe: false);

        Assert.True(raised);
    }

    // ── Logout ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_AfterLogin_SetsIsAuthenticatedFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        await _sut.LoginAsync("alice", "secret", rememberMe: false);

        _sut.Logout();

        Assert.False(_sut.IsAuthenticated);
    }

    [Fact]
    public async Task Logout_RaisesAuthenticationChangedWithFalse()
    {
        _api.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        await _sut.LoginAsync("alice", "secret", rememberMe: false);

        bool? raised = null;
        _sut.AuthenticationChanged += (_, v) => raised = v;
        _sut.Logout();

        Assert.False(raised);
    }

    // ── ClearRememberedLoginAsync ───────────────────────────────────────────

    [Fact]
    public async Task ClearRememberedLoginAsync_DelegatesToStore()
    {
        await _sut.ClearRememberedLoginAsync();

        await _store.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }
}
