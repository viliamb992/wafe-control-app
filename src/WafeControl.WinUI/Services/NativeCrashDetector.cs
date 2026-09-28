using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using Serilog;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Services;

namespace WafeControl.WinUI.Services;

/// <summary>
/// Finds crashes of this app that .NET never saw (e.g. the XAML stowed exception 0xc000027b) in the Windows
/// Application event log, which any user can read. Each is reported once: the newest time seen is kept in
/// <see cref="UserSettings.LastSeenCrashEventTime"/>.
/// </summary>
public static class NativeCrashDetector
{
    private const string Query =
        "*[System[(EventID=1000 or EventID=1026) and TimeCreated[timediff(@SystemTime) <= 604800000]]]";

    /// <summary>
    /// New crashes since the last look, oldest first. The first run only remembers where the log is, so crashes
    /// from before this version (or of older installs) aren't reported.
    /// </summary>
    public static IReadOnlyList<CrashRecord> FindNew(ISettingsStore settings, CrashRecord? handledCrash)
    {
        var saved = settings.Load();
        var now = DateTimeOffset.Now;
        if (saved.LastSeenCrashEventTime is not { } since)
        {
            settings.Save(saved with { LastSeenCrashEventTime = now });
            return [];
        }

        var exeName = Path.GetFileName(Environment.ProcessPath) ?? "WafeControl.WinUI.exe";
        var found = new List<CrashRecord>();
        try
        {
            using var reader = new EventLogReader(new EventLogQuery("Application", PathType.LogName, Query) { ReverseDirection = true });
            for (var record = reader.ReadEvent(); record is not null; record = reader.ReadEvent())
            {
                using (record)
                {
                    if (record.TimeCreated is not { } created || new DateTimeOffset(created) <= since)
                        break;

                    var data = record.Properties.Select(p => Text(p.Value)).ToList();
                    if (WindowsCrashEvent.TryParse(record.Id, data, new DateTimeOffset(created), exeName) is not { } crash)
                        continue;

                    // The .NET handlers already recorded this one (the marker file).
                    if (handledCrash is not null && (crash.Time - handledCrash.Time).Duration() < TimeSpan.FromMinutes(2))
                        continue;

                    found.Add(crash);
                }
            }
        }
        catch (EventLogException ex)
        {
            Log.Warning(ex, "Could not read the Application event log");
        }

        settings.Save(settings.Load() with { LastSeenCrashEventTime = now });
        found.Reverse();
        return found;
    }

    private static string Text(object? value) => value switch
    {
        null => string.Empty,
        uint code => code.ToString("x8", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
