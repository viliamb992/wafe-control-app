using System;
using System.Threading;
using System.Threading.Tasks;
using RecuperationSystem.Shared.Services;
using Serilog;

namespace RecuperationSystem.Desktop.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IWafeApiService _apiService;
    private readonly ICredentialStore _credentialStore;
    private bool _isAuthenticated;

    private string _username = string.Empty;
    private string _password = string.Empty;

    public event EventHandler<bool>? AuthenticationChanged;

    public AuthenticationService(IWafeApiService apiService, ICredentialStore credentialStore)
    {
        _apiService = apiService;
        _credentialStore = credentialStore;
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
    public string Password => _password;

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("Authentication attempt for user: {Username}", _username);
            
            var success = await _apiService.AuthenticateAsync(_username, _password, cancellationToken);
            
            if (success)
            {
                IsAuthenticated = true;
                Log.Information("Authentication successful");
            }
            else
            {
                IsAuthenticated = false;
                Log.Warning("Authentication failed for user: {Username}", _username);
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Authentication error");
            IsAuthenticated = false;
            throw;
        }
    }

    public async Task<bool> TryAutoLoginAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _credentialStore.TryGetAsync(cancellationToken);
        if (stored is null)
        {
            Log.Information("No stored credentials found.");
            return false;
        }

        (_username, _password) = stored.Value;

        Log.Information("Auto-login enabled for user: {Username}", _username);
        await Task.Delay(200, cancellationToken);
        return await AuthenticateAsync(cancellationToken);
    }

    public void Logout()
    {
        IsAuthenticated = false;
        Log.Information("User logged out");
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
}

