using Microsoft.Extensions.Logging;
using WafeControl.Shared.Services;
using WafeControl.Shared.Services.Http;

namespace WafeControl.Core.Services;

public sealed class AuthenticationService : IAuthenticationService, IDisposable
{
    private readonly IWafeApiService _apiService;
    private readonly ICredentialStore _credentialStore;
    private readonly WafeSession _session;
    private readonly ILogger<AuthenticationService> _logger;
    private bool _isAuthenticated;

    private string _username = string.Empty;
    private string _password = string.Empty;

    public event EventHandler<bool>? AuthenticationChanged;

    public AuthenticationService(
        IWafeApiService apiService,
        ICredentialStore credentialStore,
        WafeSession session,
        ILogger<AuthenticationService> logger)
    {
        _apiService = apiService;
        _credentialStore = credentialStore;
        _session = session;
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

    public string Username => _username;

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Authentication attempt for user: {Username}", _username);

            var success = await _apiService.AuthenticateAsync(_username, _password, cancellationToken);

            if (success)
                _logger.LogInformation("Authentication successful");
            else
                _logger.LogWarning("Authentication failed for user: {Username}", _username);

            IsAuthenticated = success;
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error");
            IsAuthenticated = false;
            throw;
        }
    }

    public async Task<bool> TryAutoLoginAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _credentialStore.TryGetAsync(cancellationToken);
        if (stored is null)
        {
            _logger.LogInformation("No stored credentials found.");
            return false;
        }

        (_username, _password) = stored.Value;

        _logger.LogInformation("Auto-login for user: {Username}", _username);
        return await AuthenticateAsync(cancellationToken);
    }

    public void Logout()
    {
        _session.Clear();
        IsAuthenticated = false;
        _logger.LogInformation("User logged out");
    }

    public async Task<bool> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken = default)
    {
        _username = username;
        _password = password;

        var success = await AuthenticateAsync(cancellationToken);

        if (success && rememberMe)
        {
            await _credentialStore.SaveAsync(username, password, cancellationToken);
        }

        return success;
    }

    public Task ClearRememberedLoginAsync(CancellationToken cancellationToken = default)
        => _credentialStore.ClearAsync(cancellationToken);

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        _logger.LogWarning("Session expired for user: {Username}", _username);
        IsAuthenticated = false;
    }

    public void Dispose() => _session.Expired -= OnSessionExpired;
}
