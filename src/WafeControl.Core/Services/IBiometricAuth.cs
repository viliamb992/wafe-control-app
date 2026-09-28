namespace WafeControl.Core.Services;

/// <summary>
/// The phone's fingerprint or face check, used to unlock the remembered login.
/// </summary>
public interface IBiometricAuth
{
    /// <summary>
    /// The platform offers sign-in methods at all (Android); false hides the Sign-in settings.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Whether the check can be shown now. It changes when the user adds or removes a fingerprint or face.
    /// </summary>
    BiometricAvailability Availability { get; }

    /// <summary>
    /// Shows the system fingerprint or face dialog and waits for its result.
    /// </summary>
    Task<BiometricResult> AuthenticateAsync(CancellationToken cancellationToken = default);
}

public enum BiometricAvailability
{
    Available,
    NoHardware,

    /// <summary>
    /// The phone has a sensor but no fingerprint or face is set up.
    /// </summary>
    NotEnrolled,

    /// <summary>
    /// Can't be used for now, e.g. the sensor is busy or needs a security update.
    /// </summary>
    Unavailable,
}

public enum BiometricResult
{
    Succeeded,

    /// <summary>
    /// The user chose "Use password" or closed the dialog.
    /// </summary>
    UsePassword,

    /// <summary>
    /// Too many attempts; the phone blocks the check for a while.
    /// </summary>
    LockedOut,

    /// <summary>
    /// No fingerprint or face is set up (any more), so the dialog can't be shown.
    /// </summary>
    Unavailable,
}

/// <summary>
/// No sign-in methods: Windows and iOS keep a remembered login without a check.
/// </summary>
public sealed class NoBiometricAuth : IBiometricAuth
{
    public bool IsSupported => false;

    public BiometricAvailability Availability => BiometricAvailability.NoHardware;

    public Task<BiometricResult> AuthenticateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BiometricResult.Unavailable);
}
