using System.Globalization;

namespace WafeControl.Core.Diagnostics;

/// <summary>
/// What "Report a problem" hands over: the app, device and last crash, never the email, unit name or serial number.
/// </summary>
public sealed class ProblemReport
{
    public const string RepositoryUrl = "https://github.com/viliamb992/wafe-control-app";

    private ProblemReport(IReadOnlyList<KeyValuePair<string, string>> fields, string platform, CrashRecord? crash)
    {
        Fields = fields;
        Platform = platform;
        Crash = crash;
    }

    public IReadOnlyList<KeyValuePair<string, string>> Fields { get; }

    private string Platform { get; }

    private CrashRecord? Crash { get; }

    public static ProblemReport Create(DeviceDescription device, string language, bool isDemo, string? unitModel, CrashRecord? crash)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("App version", AppVersion.Current),
            new("Platform", $"{device.Platform} ({device.Architecture})"),
            new("OS", device.OsVersion),
            new("Language", language),
            new("Install", device.InstallType),
            new("Demo mode", isDemo ? "yes" : "no"),
        };
        if (device.Model is { Length: > 0 } model)
            fields.Add(new("Device", model));
        if (unitModel is { Length: > 0 })
            fields.Add(new("Unit model", unitModel));
        if (crash is not null)
        {
            fields.Add(new("Last crash", crash.Time.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)
                + $" · {crash.Version} · {crash.Source} · {crash.ExceptionType}"));
            if (crash.ReportId is { Length: > 0 } id)
                fields.Add(new("Crash report ID", id));
        }

        return new ProblemReport(fields, device.Platform, crash);
    }

    /// <summary>
    /// "App version: 1.2.0" lines, to paste anywhere.
    /// </summary>
    public string Text => string.Join(Environment.NewLine, Fields.Select(f => $"{f.Key}: {f.Value}"));

    /// <summary>
    /// A new GitHub issue from the bug form (.github/ISSUE_TEMPLATE/bug.yml) with the fields filled in.
    /// </summary>
    public Uri IssueUrl
    {
        get
        {
            var query = new Dictionary<string, string>
            {
                ["template"] = "bug.yml",
                ["app-version"] = AppVersion.Current,
                ["platform"] = Platform,
                ["diagnostics"] = Text,
            };
            if (Crash?.ReportId is { Length: > 0 } id)
                query["crash-id"] = id;

            var encoded = string.Join('&', query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            return new Uri($"{RepositoryUrl}/issues/new?{encoded}");
        }
    }
}
