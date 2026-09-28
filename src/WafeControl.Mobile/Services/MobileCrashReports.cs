using Sentry;
using WafeControl.Core.Services;

namespace WafeControl.Mobile.Services;

/// <summary>
/// Crash reports on mobile: Sentry.Maui (sent to GlitchTip) is set up while the app is built, so turning reports on
/// takes effect at the next start; turning them off applies at once.
/// </summary>
public sealed class MobileCrashReports(string? dsn) : ICrashReports
{
    public bool IsAvailable => dsn is not null;

    public bool AppliesImmediately => false;

    public void Apply(bool enabled)
    {
        if (!enabled && SentrySdk.IsEnabled)
            SentrySdk.Close();
    }
}
