namespace WafeControl.Core.Services;

/// <summary>
/// When a remembered login stops working or has to be unlocked again.
/// </summary>
public static class SignInPolicy
{
    /// <summary>
    /// With <see cref="SignInMethod.Biometric"/>, returning after this long in the background asks again.
    /// </summary>
    public static readonly TimeSpan BiometricLockAfter = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The time limit has passed since the password was typed. Never for <see cref="SignInMethod.Biometric"/>,
    /// without a limit, or for a login with no time yet.
    /// </summary>
    public static bool IsExpired(StoredLogin login, UserSettings settings, DateTimeOffset now) =>
        settings.SignInMethod == SignInMethod.StaySignedIn
        && settings.StaySignedInFor is { } duration
        && login.SavedAt is { } savedAt
        && now >= savedAt + TimeSpan.FromDays((int)duration);

    /// <summary>
    /// Back from the background after long enough that the fingerprint or face is asked again.
    /// </summary>
    public static bool NeedsUnlockOnResume(UserSettings settings, TimeSpan inBackground) =>
        settings.SignInMethod == SignInMethod.Biometric && inBackground >= BiometricLockAfter;
}
