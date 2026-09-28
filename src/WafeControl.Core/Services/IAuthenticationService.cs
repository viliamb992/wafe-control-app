using WafeControl.Shared.Services;

namespace WafeControl.Core.Services;

public interface IAuthenticationService
{
    /// <summary>
    /// Raised when <see cref="IsAuthenticated"/> changes. May be raised on a background thread
    /// (e.g. when the session expires during a background request).
    /// </summary>
    event EventHandler<bool>? AuthenticationChanged;

    bool IsAuthenticated { get; }

    /// <summary>
    /// Signed in to the simulated demo unit rather than a Wafe account.
    /// </summary>
    bool IsDemo { get; }

    string Username { get; }

    Task<ApiResult> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs in with the remembered login. Null when there is none; otherwise the result of the sign-in.
    /// </summary>
    Task<ApiResult?> TryAutoLoginAsync(CancellationToken cancellationToken = default);

    Task<ApiResult> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs in to the demo unit; nothing is sent to Wafe and no login is remembered.
    /// </summary>
    void StartDemo();

    void Logout();
    Task ClearRememberedLoginAsync(CancellationToken cancellationToken = default);
}
