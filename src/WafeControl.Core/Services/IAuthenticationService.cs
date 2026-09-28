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

    /// <summary>
    /// A login is saved on this device (known once it was loaded or saved).
    /// </summary>
    bool HasRememberedLogin { get; }

    Task<ApiResult> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs in with the remembered login, unless it expired or the fingerprint or face check didn't pass.
    /// </summary>
    Task<AutoLoginResult> TryAutoLoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// What returning to the app after <paramref name="inBackground"/> requires. An expired login is forgotten here.
    /// </summary>
    Task<ResumeCheck> CheckResumeAsync(TimeSpan inBackground, CancellationToken cancellationToken = default);

    /// <summary>
    /// The remembered login's time limit counts from now (after switching away from fingerprint or face).
    /// </summary>
    Task RestartSignInPeriodAsync(CancellationToken cancellationToken = default);

    Task<ApiResult> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs in to the demo unit; nothing is sent to Wafe and no login is remembered.
    /// </summary>
    void StartDemo();

    void Logout();
    Task ClearRememberedLoginAsync(CancellationToken cancellationToken = default);
}

public enum AutoLoginOutcome
{
    /// <summary>
    /// No login is remembered.
    /// </summary>
    None,

    /// <summary>
    /// The time limit passed; the login was forgotten.
    /// </summary>
    Expired,

    /// <summary>
    /// "Use password" instead of the fingerprint or face; the login stays saved.
    /// </summary>
    BiometricDeclined,

    /// <summary>
    /// Too many attempts; the login stays saved.
    /// </summary>
    BiometricLockedOut,

    /// <summary>
    /// No fingerprint or face is set up any more: the login was forgotten and the method is back to Stay signed in.
    /// </summary>
    BiometricUnavailable,

    /// <summary>
    /// Signed in with the remembered login; <see cref="AutoLoginResult.SignIn"/> says how it went.
    /// </summary>
    Attempted,
}

public sealed record AutoLoginResult(AutoLoginOutcome Outcome, ApiResult? SignIn = null)
{
    public static AutoLoginResult None { get; } = new(AutoLoginOutcome.None);

    public bool Ok => SignIn?.Ok == true;

    public static AutoLoginResult Attempted(ApiResult signIn) => new(AutoLoginOutcome.Attempted, signIn);
}

public enum ResumeCheck
{
    /// <summary>
    /// Carry on.
    /// </summary>
    None,

    /// <summary>
    /// The time limit passed while away; the login was forgotten. Sign out and ask for the password.
    /// </summary>
    Expired,

    /// <summary>
    /// Away long enough that the fingerprint or face is asked again; the login stays saved.
    /// </summary>
    Unlock,
}
