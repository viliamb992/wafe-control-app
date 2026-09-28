using Microsoft.Extensions.Logging;
using WafeControl.Shared.Diagnostics;
using WafeControl.Shared.Services;
using WafeControl.Shared.Services.Http;

namespace WafeControl.Core.Services;

public sealed class AuthenticationService : IAuthenticationService, IDisposable
{
    public const string DemoUsername = "demo";

    private readonly IWafeApiService _apiService;
    private readonly ICredentialStore _credentialStore;
    private readonly WafeSession _session;
    private readonly ISettingsStore _settings;
    private readonly IBiometricAuth _biometric;
    private readonly TimeProvider _time;
    private readonly ILogger<AuthenticationService> _logger;
    private bool _isAuthenticated;

    private string _username = string.Empty;
    private string _password = string.Empty;

    public event EventHandler<bool>? AuthenticationChanged;

    public AuthenticationService(
        IWafeApiService apiService,
        ICredentialStore credentialStore,
        WafeSession session,
        ISettingsStore settings,
        IBiometricAuth biometric,
        TimeProvider time,
        ILogger<AuthenticationService> logger)
    {
        _apiService = apiService;
        _credentialStore = credentialStore;
        _session = session;
        _settings = settings;
        _biometric = biometric;
        _time = time;
        _logger = logger;

        _session.Expired += OnSessionExpired;
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (_isAuthenticated != value)
            {
                _isAuthenticated = value;
                AuthenticationChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsDemo => _session.IsDemo;

    public string Username => _username;

    public bool HasRememberedLogin { get; private set; }

    public async Task<ApiResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendSignInAsync(cancellationToken);
        IsAuthenticated = result.Ok;
        return result;
    }

    private async Task<ApiResult> SendSignInAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Authentication attempt for user: {Username}", LogRedaction.Email(_username));

        var result = await _apiService.AuthenticateAsync(_username, _password, cancellationToken);

        if (result.Ok)
            _logger.LogInformation("Authentication successful");
        else
            _logger.LogWarning("Authentication failed for user {Username}: {Error}", LogRedaction.Email(_username), result.Error);

        return result;
    }

    public async Task<AutoLoginResult> TryAutoLoginAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _credentialStore.TryGetAsync(cancellationToken);
        HasRememberedLogin = stored is not null;
        if (stored is null)
        {
            _logger.LogInformation("No stored credentials found.");
            return AutoLoginResult.None;
        }

        _username = stored.Username;
        _password = string.Empty;
        var settings = _settings.Load();
        var now = _time.GetUtcNow();

        // Saved by a version without time limits: the limit counts from the first start with this one.
        if (stored.SavedAt is null && settings.StaySignedInFor is not null)
        {
            stored = stored with { SavedAt = now };
            await _credentialStore.SaveAsync(stored, cancellationToken);
        }

        if (UsesBiometric(settings))
        {
            var check = await _biometric.AuthenticateAsync(cancellationToken);
            _logger.LogInformation("Fingerprint or face check: {Result}", check);
            switch (check)
            {
                case BiometricResult.UsePassword:
                    return new AutoLoginResult(AutoLoginOutcome.BiometricDeclined);
                case BiometricResult.LockedOut:
                    return new AutoLoginResult(AutoLoginOutcome.BiometricLockedOut);
                case BiometricResult.Unavailable:
                    // Nothing can unlock the login any more, so it doesn't sign in on its own either.
                    _settings.Save(_settings.Load() with { SignInMethod = SignInMethod.StaySignedIn });
                    await ForgetAsync(cancellationToken);
                    return new AutoLoginResult(AutoLoginOutcome.BiometricUnavailable);
            }
        }
        else if (SignInPolicy.IsExpired(stored, settings, now))
        {
            _logger.LogInformation("Remembered login expired after {Days} days; forgetting it", (int?)settings.StaySignedInFor);
            await ForgetAsync(cancellationToken);
            return new AutoLoginResult(AutoLoginOutcome.Expired);
        }

        _password = stored.Password;
        _logger.LogInformation("Auto-login for user: {Username}", LogRedaction.Email(_username));
        return AutoLoginResult.Attempted(await AuthenticateAsync(cancellationToken));
    }

    public async Task<ResumeCheck> CheckResumeAsync(TimeSpan inBackground, CancellationToken cancellationToken = default)
    {
        // Signed in without "Stay signed in" (or in the demo): nothing is saved to expire or unlock.
        if (!IsAuthenticated || IsDemo || !HasRememberedLogin)
            return ResumeCheck.None;

        var settings = _settings.Load();
        if (UsesBiometric(settings))
            return SignInPolicy.NeedsUnlockOnResume(settings, inBackground) ? ResumeCheck.Unlock : ResumeCheck.None;

        var stored = await _credentialStore.TryGetAsync(cancellationToken);
        if (stored is null)
        {
            HasRememberedLogin = false;
            return ResumeCheck.None;
        }

        if (!SignInPolicy.IsExpired(stored, settings, _time.GetUtcNow()))
            return ResumeCheck.None;

        _logger.LogInformation("Remembered login expired while in the background; forgetting it");
        await ForgetAsync(cancellationToken);
        return ResumeCheck.Expired;
    }

    public async Task RestartSignInPeriodAsync(CancellationToken cancellationToken = default)
    {
        if (await _credentialStore.TryGetAsync(cancellationToken) is { } stored)
            await _credentialStore.SaveAsync(stored with { SavedAt = _time.GetUtcNow() }, cancellationToken);
    }

    // Only where the platform can show the check; a copied settings file can't lock anyone out elsewhere.
    private bool UsesBiometric(UserSettings settings) =>
        settings.SignInMethod == SignInMethod.Biometric && _biometric.IsSupported;

    private async Task ForgetAsync(CancellationToken cancellationToken)
    {
        await _credentialStore.ClearAsync(cancellationToken);
        HasRememberedLogin = false;
    }

    public void StartDemo()
    {
        // A real session in progress ends first, so its key can't leak into demo requests.
        if (IsAuthenticated)
            Logout();

        _username = DemoUsername;
        _password = string.Empty;
        _session.StartDemo();
        _logger.LogInformation("Demo mode started");
        IsAuthenticated = true;
    }

    public void Logout()
    {
        var wasDemo = _session.IsDemo;
        _session.Clear();
        if (wasDemo)
        {
            _username = string.Empty;
            _logger.LogInformation("Demo mode ended");
        }

        IsAuthenticated = false;
        _logger.LogInformation("User logged out");
    }

    public async Task<ApiResult> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken = default)
    {
        _username = username;
        _password = password;

        var result = await SendSignInAsync(cancellationToken);

        // Saved before AuthenticationChanged, whose handlers ask about the saved login (the fingerprint or face offer).
        if (result.Ok && rememberMe)
        {
            await _credentialStore.SaveAsync(new StoredLogin(username, password, _time.GetUtcNow()), cancellationToken);
            HasRememberedLogin = true;
        }

        IsAuthenticated = result.Ok;
        return result;
    }

    public Task ClearRememberedLoginAsync(CancellationToken cancellationToken = default)
        => ForgetAsync(cancellationToken);

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        _logger.LogWarning("Session expired for user: {Username}", LogRedaction.Email(_username));
        IsAuthenticated = false;
    }

    public void Dispose() => _session.Expired -= OnSessionExpired;
}
