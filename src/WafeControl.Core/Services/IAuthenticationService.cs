namespace WafeControl.Core.Services;

public interface IAuthenticationService
{
    /// <summary>
    /// Raised when <see cref="IsAuthenticated"/> changes. May be raised on a background thread
    /// (e.g. when the session expires during a background request).
    /// </summary>
    event EventHandler<bool>? AuthenticationChanged;

    bool IsAuthenticated { get; }
    string Username { get; }

    Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<bool> TryAutoLoginAsync(CancellationToken cancellationToken = default);

    Task<bool> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken = default);
    void Logout();
    Task ClearRememberedLoginAsync(CancellationToken cancellationToken = default);
}
