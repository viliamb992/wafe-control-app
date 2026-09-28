using System.Globalization;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;

namespace WafeControl.Core.Diagnostics;

/// <summary>
/// What the app runs on, for the startup log line, crash reports and "Report a problem". Set by each app at startup.
/// </summary>
public sealed record DeviceDescription(string Platform, string OsVersion, string Architecture, string? Model, string InstallType)
{
    public static DeviceDescription Current { get; set; } = new(
        "unknown", Environment.OSVersion.VersionString, RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(), null, "dev");
}

/// <summary>
/// The log file setup shared by every app: one file per day, kept for a week, at most 2 MB each.
/// </summary>
public static class AppLogging
{
    private const string FileName = "wafe-.log";
    private const string FilePattern = "wafe-*.log";
    private const string OutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// A logger writing to <paramref name="logDirectory"/>; the app may add sinks before creating it.
    /// </summary>
    public static LoggerConfiguration Configure(string logDirectory) =>
        new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(logDirectory, FileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 2 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: OutputTemplate);

    /// <summary>
    /// One line at startup that tells a log reader what the app ran on.
    /// </summary>
    public static void LogEnvironment(string language)
    {
        var device = DeviceDescription.Current;
        Log.Information("Starting WAFE Control {Version} on {Platform} {Os} ({Architecture}{Model}), language {Language}, install: {Install}",
            AppVersion.Current, device.Platform, device.OsVersion, device.Architecture,
            device.Model is null ? string.Empty : ", " + device.Model, language, device.InstallType);
    }

    /// <summary>
    /// The log files, newest first.
    /// </summary>
    public static IReadOnlyList<string> LogFiles(string logDirectory) =>
        Directory.Exists(logDirectory)
            ? Directory.GetFiles(logDirectory, FilePattern).OrderByDescending(File.GetLastWriteTimeUtc).ToArray()
            : [];

    /// <summary>
    /// Copies the log files into one zip (for sharing); the open file of today is read while the app keeps writing it.
    /// </summary>
    public static string ZipLogs(string logDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var zipPath = Path.Combine(targetDirectory,
            $"wafe-control-logs-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.zip");

        using var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
        foreach (var file in LogFiles(logDirectory))
        {
            var entry = zip.CreateEntry(Path.GetFileName(file));
            using var target = entry.Open();
            using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            source.CopyTo(target);
        }

        return zipPath;
    }
}
