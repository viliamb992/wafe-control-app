using System.Reflection;
using Sentry;
using WafeControl.Shared.Diagnostics;
using WafeControl.Shared.Services;

namespace WafeControl.Core.Diagnostics;

/// <summary>
/// Crash reports through the Sentry SDK, sent to GlitchTip. Opt-in: nothing leaves the device unless the user
/// agreed (<see cref="IsAllowed"/>) and the build carries a reporting address (DSN, release builds only).
/// Emails are scrubbed; the same error is sent at most once per <see cref="RepeatWindow"/>, so a failure inside the
/// polling loop can't use up the monthly quota.
/// </summary>
public static class CrashReporting
{
    /// <summary>
    /// The assembly metadata key the release build writes the DSN to (-p:CrashReportsDsn=…).
    /// </summary>
    public const string DsnMetadataKey = "CrashReportsDsn";

    public static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(10);

    private static readonly RepeatGuard Repeats = new(RepeatWindow);

    /// <summary>
    /// The user's current choice; checked for every event, so turning reports off applies at once.
    /// </summary>
    public static Func<bool> IsAllowed { get; set; } = () => false;

    /// <summary>
    /// The DSN built into <paramref name="assembly"/>; null in development builds.
    /// </summary>
    public static string? ReadDsn(Assembly assembly) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == DsnMetadataKey)?.Value is { Length: > 0 } dsn ? dsn : null;

    /// <summary>
    /// The options every app uses. <paramref name="platform"/> is "windows" or "android".
    /// </summary>
    public static void Configure(SentryOptions options, string dsn, string platform, string cacheDirectory)
    {
        var device = DeviceDescription.Current;

        options.Dsn = dsn;
        options.Release = $"wafe-control-{platform}@{AppVersion.Current}";
#if DEBUG
        // Testing crash reports locally (-p:CrashReportsDsn=…) doesn't mix with real ones.
        options.Environment = "dev";
#else
        options.Environment = EnvironmentName(AppVersion.Current);
#endif
        options.SendDefaultPii = false;
        options.AttachStacktrace = true;
        options.MaxBreadcrumbs = 50;

        // GlitchTip ignores sessions; don't spend requests on them.
        options.AutoSessionTracking = false;

        // A crash while offline is sent at the next start.
        options.CacheDirectoryPath = cacheDirectory;

        options.DefaultTags["arch"] = device.Architecture;
        options.DefaultTags["install"] = device.InstallType;
        options.SetBeforeSend((evt, _) => BeforeSend(evt, DateTimeOffset.UtcNow));
        options.SetBeforeBreadcrumb((crumb, _) => ScrubBreadcrumb(crumb));
    }

    /// <summary>
    /// Development builds, betas (a suffix like -beta.1) and releases apart.
    /// </summary>
    public static string EnvironmentName(string version) =>
        version.StartsWith("0.0.0", StringComparison.Ordinal) ? "dev"
        : version.Contains('-') ? "beta"
        : "production";

    /// <summary>
    /// Last filter before an event leaves: consent, scrubbing, grouping and the repeat guard.
    /// </summary>
    public static SentryEvent? BeforeSend(SentryEvent evt, DateTimeOffset now)
    {
        if (!IsAllowed())
            return null;

        Scrub(evt);

        // One issue per endpoint when the API changes, whatever the stack trace.
        if (evt.Message is { Message: WafeApiService.InvalidResponseMessage } message)
            evt.SetFingerprint("api-drift", message.Formatted ?? message.Message);

        if (evt.Level != SentryLevel.Fatal && !Repeats.ShouldSend(RepeatKey(evt), now))
            return null;

        return evt;
    }

    /// <summary>
    /// Tags that change while the app runs.
    /// </summary>
    public static void SetScope(bool isDemo, string? unitModel)
    {
        if (!SentrySdk.IsEnabled)
            return;

        SentrySdk.ConfigureScope(scope =>
        {
            scope.SetTag("demo", isDemo ? "true" : "false");
            if (unitModel is { Length: > 0 })
                scope.SetTag("unit_model", unitModel);
        });
    }

    /// <summary>
    /// Sends a crash and waits briefly for it to leave. Returns the report ID, or null when reports are off.
    /// </summary>
    public static string? CaptureFatal(Exception exception, string source)
    {
        if (!SentrySdk.IsEnabled)
            return null;

        SentrySdk.CaptureException(exception, handled: false, terminal: true, scope =>
        {
            scope.Level = SentryLevel.Fatal;
            scope.SetTag("crash_source", source);
        });
        SentrySdk.Flush(TimeSpan.FromSeconds(2));
        return ReportId();
    }

    /// <summary>
    /// Sends a crash found after the fact (the Windows event log). Returns the report ID, or null.
    /// </summary>
    public static string? CaptureEarlierCrash(CrashRecord crash, IReadOnlyDictionary<string, string> details)
    {
        if (!SentrySdk.IsEnabled)
            return null;

        SentrySdk.CaptureMessage($"Previous session crashed: {crash.ExceptionType} ({crash.Source})", scope =>
        {
            scope.SetFingerprint("native-crash", crash.Source, crash.ExceptionType);
            scope.SetTag("crashed_version", crash.Version);
            foreach (var (key, value) in details)
                scope.SetExtra(key, value);
        }, SentryLevel.Fatal);
        return ReportId();
    }

    private static string? ReportId() =>
        SentrySdk.LastEventId is var id && id != SentryId.Empty ? id.ToString() : null;

    private static void Scrub(SentryEvent evt)
    {
        if (evt.Message is { } message)
        {
            message.Message = LogRedaction.Scrub(message.Message);
            message.Formatted = LogRedaction.Scrub(message.Formatted);
        }

        foreach (var exception in evt.SentryExceptions ?? [])
            exception.Value = LogRedaction.Scrub(exception.Value);

        foreach (var (key, value) in evt.Extra.ToList())
        {
            if (value is string text)
                evt.SetExtra(key, LogRedaction.Scrub(text));
        }

        // SendDefaultPii is off, but be sure the machine name stays home.
        evt.ServerName = null;
    }

    // Runs as the breadcrumb is added, so the new one's time is the same.
    private static Breadcrumb ScrubBreadcrumb(Breadcrumb crumb) => new(
        LogRedaction.Scrub(crumb.Message) ?? string.Empty,
        crumb.Type ?? "default",
        crumb.Data?.ToDictionary(p => p.Key, p => LogRedaction.Scrub(p.Value) ?? string.Empty),
        crumb.Category,
        crumb.Level);

    private static string RepeatKey(SentryEvent evt) =>
        evt.Fingerprint.Count > 0
            ? string.Join('|', evt.Fingerprint)
            : string.Join('|',
                evt.Message?.Message,
                evt.SentryExceptions?.LastOrDefault()?.Type,
                evt.SentryExceptions?.LastOrDefault()?.Value);

    /// <summary>
    /// Remembers what was sent recently.
    /// </summary>
    internal sealed class RepeatGuard(TimeSpan window)
    {
        private readonly Dictionary<string, DateTimeOffset> _sent = [];
        private readonly Lock _gate = new();

        public bool ShouldSend(string key, DateTimeOffset now)
        {
            lock (_gate)
            {
                if (_sent.TryGetValue(key, out var at) && now - at < window)
                    return false;

                _sent[key] = now;
                return true;
            }
        }
    }
}
