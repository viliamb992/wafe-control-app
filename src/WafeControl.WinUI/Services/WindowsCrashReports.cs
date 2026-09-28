using Sentry;
using Serilog;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Services;

namespace WafeControl.WinUI.Services;

/// <summary>
/// Crash reports on Windows: the Sentry SDK (sent to GlitchTip), started and stopped while the app runs.
/// The Serilog sink added in <see cref="App"/> turns error logs into reports while the SDK is on.
/// </summary>
public sealed class WindowsCrashReports(string? dsn, string cacheDirectory) : ICrashReports
{
    private IDisposable? _sdk;

    public bool IsAvailable => dsn is not null;

    public bool AppliesImmediately => true;

    public void Apply(bool enabled)
    {
        if (dsn is null)
            return;

        if (enabled && _sdk is null)
        {
            _sdk = SentrySdk.Init(options =>
            {
                CrashReporting.Configure(options, dsn, "windows", cacheDirectory);

                // A desktop app: one user, one scope for the whole process.
                options.IsGlobalModeEnabled = true;
            });
            Log.Information("Crash reports on");
        }
        else if (!enabled && _sdk is not null)
        {
            _sdk.Dispose();
            _sdk = null;
            Log.Information("Crash reports off");
        }
    }
}
