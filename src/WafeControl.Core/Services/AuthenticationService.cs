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

    public bool IsDemo => _session.IsDemo;

    public string Username => _username;

    public async Task<ApiResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Authentication attempt for user: {Username}", LogRedaction.Email(_username));

        var result = await _apiService.AuthenticateAsync(_username, _password, cancellationToken);

        if (result.Ok)
            _logger.LogInformation("Authentication successful");
        else
            _logger.LogWarning("Authentication failed for user {Username}: {Error}", LogRedaction.Email(_username), result.Error);

        IsAuthenticated = result.Ok;
        return result;
    }

    public async Task<ApiResult?> TryAutoLoginAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _credentialStore.TryGetAsync(cancellationToken);
        if (stored is null)
        {
            _logger.LogInformation("No stored credentials found.");
            return null;
        }

        (_username, _password) = stored.Value;

        _logger.LogInformation("Auto-login for user: {Username}", LogRedaction.Email(_username));
        return await AuthenticateAsync(cancellationToken);
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

        var result = await AuthenticateAsync(cancellationToken);

        if (result.Ok && rememberMe)
        {
            await _credentialStore.SaveAsync(username, password, cancellationToken);
        }

        return result;
    }

    public Task ClearRememberedLoginAsync(CancellationToken cancellationToken = default)
        => _credentialStore.ClearAsync(cancellationToken);

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        _logger.LogWarning("Session expired for user: {Username}", LogRedaction.Email(_username));
        IsAuthenticated = false;
    }

    public void Dispose() => _session.Expired -= OnSessionExpired;
}
