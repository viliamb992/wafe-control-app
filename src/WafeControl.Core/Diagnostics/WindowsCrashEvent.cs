namespace WafeControl.Core.Diagnostics;

/// <summary>
/// Reads the crash records Windows writes to the Application event log when a process dies without .NET noticing
/// (e.g. a WinUI "stowed exception" 0xc000027b) or with a .NET exception its handlers never saw (stack overflow).
/// </summary>
public static class WindowsCrashEvent
{
    /// <summary>
    /// "Application Error": faulting app, version, module, module version, exception code, offset.
    /// </summary>
    public const int ApplicationError = 1000;

    /// <summary>
    /// ".NET Runtime": the process ended because of an unhandled .NET exception; the data is one text block.
    /// </summary>
    public const int DotNetRuntime = 1026;

    private const int MaxFrames = 20;

    /// <summary>
    /// A crash record from one event's data (as strings), or null when the event isn't about <paramref name="exeName"/>.
    /// </summary>
    public static CrashRecord? TryParse(int eventId, IReadOnlyList<string> data, DateTimeOffset time, string exeName)
    {
        return eventId switch
        {
            ApplicationError => ParseApplicationError(data, time, exeName),
            DotNetRuntime => ParseDotNetRuntime(data, time, exeName),
            _ => null,
        };
    }

    // 0 app name, 1 app version, 2 app timestamp, 3 module name, 4 module version, 5 module timestamp,
    // 6 exception code, 7 fault offset, ...
    private static CrashRecord? ParseApplicationError(IReadOnlyList<string> data, DateTimeOffset time, string exeName)
    {
        if (data.Count < 8 || !data[0].Equals(exeName, StringComparison.OrdinalIgnoreCase))
            return null;

        var module = data[3];
        // Written as "c000027b".
        var code = data[6].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? data[6] : "0x" + data[6];
        return new CrashRecord(
            time,
            data[1],
            "Windows (Application Error)",
            $"{code} in {module}",
            $"Faulting module {module} {data[4]}, exception code {code}, offset {data[7]}",
            string.Empty,
            null);
    }

    // One text: "Application: X.exe\nCoreCLR Version: …\nDescription: …\nException Info: System.X: message\n   at …"
    private static CrashRecord? ParseDotNetRuntime(IReadOnlyList<string> data, DateTimeOffset time, string exeName)
    {
        if (data.Count == 0)
            return null;

        var lines = data[0].Replace("\r", string.Empty).Split('\n');
        var application = Value(lines, "Application:");
        if (application is null || !application.Equals(exeName, StringComparison.OrdinalIgnoreCase))
            return null;

        var exceptionInfo = Value(lines, "Exception Info:") ?? Value(lines, "Description:") ?? "Unknown";
        var colon = exceptionInfo.IndexOf(':');
        var type = colon > 0 ? exceptionInfo[..colon] : exceptionInfo;
        var message = colon > 0 ? exceptionInfo[(colon + 1)..].Trim() : string.Empty;
        var frames = lines.Where(l => l.TrimStart().StartsWith("at ", StringComparison.Ordinal)).Take(MaxFrames);

        // The event doesn't carry the app's version.
        return new CrashRecord(
            time,
            string.Empty,
            "Windows (.NET Runtime)",
            type,
            message,
            string.Join('\n', frames.Select(f => f.Trim())),
            null);
    }

    private static string? Value(IEnumerable<string> lines, string prefix) =>
        lines.FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) is { } line
            ? line[prefix.Length..].Trim()
            : null;
}
