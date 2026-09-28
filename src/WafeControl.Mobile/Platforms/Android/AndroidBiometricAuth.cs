using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using Java.Lang;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;

namespace WafeControl.Mobile;

/// <summary>
/// The system fingerprint or face dialog (AndroidX BiometricPrompt). It's a gate in front of the login in
/// SecureStorage, so "weak" (Class 2) face unlock counts too, as most phones have no other.
/// </summary>
public sealed class AndroidBiometricAuth(ILogger<AndroidBiometricAuth> logger) : IBiometricAuth
{
    private const int Authenticators = BiometricManager.Authenticators.BiometricWeak;

    public bool IsSupported => true;

    public BiometricAvailability Availability => BiometricManager.From(Platform.AppContext).CanAuthenticate(Authenticators) switch
    {
        BiometricManager.BiometricSuccess => BiometricAvailability.Available,
        BiometricManager.BiometricErrorNoneEnrolled => BiometricAvailability.NotEnrolled,
        BiometricManager.BiometricErrorNoHardware => BiometricAvailability.NoHardware,
        _ => BiometricAvailability.Unavailable,
    };

    public Task<BiometricResult> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        switch (Availability)
        {
            case BiometricAvailability.NotEnrolled or BiometricAvailability.NoHardware:
                return Task.FromResult(BiometricResult.Unavailable);
            case BiometricAvailability.Unavailable:
                // For now only (sensor busy, security update): the password still works.
                return Task.FromResult(BiometricResult.UsePassword);
        }

        return MainThread.InvokeOnMainThreadAsync(() => ShowPromptAsync(cancellationToken));
    }

    private async Task<BiometricResult> ShowPromptAsync(CancellationToken cancellationToken)
    {
        if (Platform.CurrentActivity is not FragmentActivity activity)
            return BiometricResult.UsePassword;

        var callback = new Callback(logger);
        var prompt = new BiometricPrompt(activity, ContextCompat.GetMainExecutor(activity)!, callback);
        var info = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle(Strings.BiometricPromptTitle)
            .SetSubtitle(Strings.BiometricPromptSubtitle)
            .SetNegativeButtonText(Strings.BiometricUsePassword)
            .SetAllowedAuthenticators(Authenticators)
            .SetConfirmationRequired(false)
            .Build();

        using var registration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(prompt.CancelAuthentication));
        prompt.Authenticate(info);
        return await callback.Result.Task;
    }

    private sealed class Callback(ILogger logger) : BiometricPrompt.AuthenticationCallback
    {
        public TaskCompletionSource<BiometricResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result) =>
            Result.TrySetResult(BiometricResult.Succeeded);

        public override void OnAuthenticationError(int errorCode, ICharSequence errString)
        {
            logger.LogInformation("Fingerprint or face dialog closed: {Code} {Message}", errorCode, errString?.ToString());
            Result.TrySetResult(errorCode switch
            {
                BiometricPrompt.ErrorLockout or BiometricPrompt.ErrorLockoutPermanent => BiometricResult.LockedOut,
                BiometricPrompt.ErrorNoBiometrics or BiometricPrompt.ErrorHwNotPresent => BiometricResult.Unavailable,

                // "Use password", back, cancelled, timed out, sensor busy: the password still works.
                _ => BiometricResult.UsePassword,
            });
        }

        // One unrecognised try; the dialog stays open for another.
        public override void OnAuthenticationFailed()
        {
        }
    }
}
