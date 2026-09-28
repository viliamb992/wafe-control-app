using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using WafeControl.Shared.Diagnostics;

namespace WafeControl.Core.Diagnostics;

/// <summary>
/// A crash, remembered until the next start: the app shows "closed unexpectedly" and offers to report it.
/// </summary>
public sealed record CrashRecord(
    DateTimeOffset Time,
    string Version,
    string Source,
    string ExceptionType,
    string Message,
    string StackTrace,
    string? ReportId);

[JsonSerializable(typeof(CrashRecord))]
internal sealed partial class CrashJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Last words of a dying app: logs the exception, sends it (when crash reports are on) and leaves a marker file
/// for the next start. Hooked to the platform's unhandled exception events by each app.
/// </summary>
public static class CrashHandler
{
    private const int MaxFrames = 20;

    private static string? _markerPath;
    private static int _handled;

    /// <summary>
    /// The crash of the previous session (or found in the Windows event log); null when there was none.
    /// Kept for "Report a problem" during this session.
    /// </summary>
    public static CrashRecord? LastCrash { get; private set; }

    /// <summary>
    /// Call first thing at startup; the marker lives next to the logs.
    /// </summary>
    public static void Initialize(string directory)
    {
        Directory.CreateDirectory(directory);
        _markerPath = Path.Combine(directory, "last-crash.json");
    }

    /// <summary>
    /// The process is going down because of <paramref name="exception"/>. Never throws; runs once per process.
    /// </summary>
    public static void OnFatal(Exception exception, string source)
    {
        if (Interlocked.Exchange(ref _handled, 1) == 1)
            return;

        try
        {
            Log.Fatal(exception, "Unhandled exception ({Source})", source);
            var reportId = CrashReporting.CaptureFatal(exception, source);
            Write(new CrashRecord(
                DateTimeOffset.Now,
                AppVersion.Current,
                source,
                exception.GetType().FullName ?? exception.GetType().Name,
                LogRedaction.Scrub(exception.Message) ?? string.Empty,
                Frames(exception),
                reportId));
            Log.CloseAndFlush();
        }
        catch
        {
            // Nothing left to tell anyone: the app is crashing already.
        }
    }

    /// <summary>
    /// A task failed and nobody looked. Not fatal on .NET, but a bug worth logging.
    /// </summary>
    public static void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    /// <summary>
    /// Reads and deletes the marker left by a crash in the previous session.
    /// </summary>
    public static CrashRecord? TryTakeLastCrash()
    {
        if (_markerPath is null || !File.Exists(_markerPath))
            return null;

        try
        {
            var crash = JsonSerializer.Deserialize(File.ReadAllText(_markerPath), CrashJsonContext.Default.CrashRecord);
            File.Delete(_markerPath);
            if (crash is not null)
                LastCrash = crash;
            return crash;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warning(ex, "Could not read the crash marker");
            TryDelete(_markerPath);
            return null;
        }
    }

    /// <summary>
    /// A crash found another way (the Windows event log); it becomes <see cref="LastCrash"/>.
    /// </summary>
    public static void Remember(CrashRecord crash) => LastCrash = crash;

    private static void Write(CrashRecord crash)
    {
        if (_markerPath is null)
            return;

        File.WriteAllText(_markerPath, JsonSerializer.Serialize(crash, CrashJsonContext.Default.CrashRecord));
    }

    private static string Frames(Exception exception)
    {
        var lines = (exception.StackTrace ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('\n', lines.Take(MaxFrames).Select(l => l.TrimEnd('\r')));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Read next time again; harmless.
        }
    }
}
