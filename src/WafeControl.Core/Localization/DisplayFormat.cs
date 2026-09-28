using System.Globalization;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Shared;
using WafeControl.Shared.Models;

namespace WafeControl.Core.Localization;

/// <summary>
/// How CO₂ readings are rated; each app maps it to its semantic colors (DESIGN.md, section 2.4).
/// </summary>
public enum Co2Rating
{
    NoReading,
    Good,
    Fair,
    Poor,
}

/// <summary>
/// Display text for values shown by every app, in the current app language.
/// </summary>
public static class DisplayFormat
{
    private const string NoValue = ModeNames.NoValue;

    /// <summary>
    /// Below this filter health (percent) the filter is shown as wearing out.
    /// </summary>
    public const int FilterLowThreshold = 20;

    public static string Temperature(double? value) =>
        value is { } v ? $"{v.ToString("0.0", CultureInfo.CurrentCulture)} °C" : NoValue;

    public static string Humidity(double? value) => value is { } v ? $"{v:0} %" : NoValue;

    public static string Flow(int value) => $"{value} m³/h";

    public static string Co2(int value) => value > 0 ? $"{value} ppm" : NoValue;

    public static Co2Rating RateCo2(int value) => value switch
    {
        <= 0 => Co2Rating.NoReading,
        < 800 => Co2Rating.Good,
        < 1200 => Co2Rating.Fair,
        _ => Co2Rating.Poor,
    };

    public static string Co2Quality(int value) => RateCo2(value) switch
    {
        Co2Rating.Good => Strings.Co2Good,
        Co2Rating.Fair => Strings.Co2Fair,
        Co2Rating.Poor => Strings.Co2Poor,
        _ => Strings.Co2NoReading,
    };

    public static string Percent(int? value) => value is { } v ? $"{v} %" : NoValue;

    public static bool IsFilterLow(int? health) => health < FilterLowThreshold;

    public static string SystemState(bool isRunning) => isRunning ? Strings.SystemRunning : Strings.SystemStopped;

    /// <summary>
    /// "Intelligent mode · 140 m³/h", or just the mode while the unit reports no sensor data.
    /// </summary>
    public static string SystemSummary(string? authority, int currentFlow, bool hasSensorData) =>
        hasSensorData
            ? string.Format(Strings.SystemSummary, ModeNames.Operating(authority), Flow(currentFlow))
            : string.Format(Strings.SystemSummaryModeOnly, ModeNames.Operating(authority));

    public static string NextStart(DateTime? nextStart) =>
        nextStart is { } next ? string.Format(Strings.SystemNextStart, ScheduleFormat.DateAndTime(next)) : string.Empty;

    /// <summary>
    /// The next scheduled start matters only while the unit runs on its schedule.
    /// </summary>
    public static bool ShowsNextStart(DateTime? nextStart, string? authority, bool isRunning) =>
        nextStart.HasValue && isRunning && authority == AppConstants.ModeSchedule;

    /// <summary>
    /// "Last update: 14:32:05" (today) or with the day and date; empty until the unit's data time is known.
    /// </summary>
    public static string LastUpdate(DateTimeOffset? lastUpdate)
    {
        if (lastUpdate is not { } time)
            return string.Empty;

        var local = time.LocalDateTime;
        var text = local.Date == DateTime.Today ? local.ToString("HH:mm:ss", CultureInfo.CurrentCulture) : ScheduleFormat.DateAndTime(local);
        return string.Format(Strings.TitleBarLastUpdate, text);
    }

    public static string Online(bool isOnline) => isOnline ? Strings.TitleBarOnline : Strings.TitleBarOffline;

    public static string BoostState(bool isActive, string remaining) =>
        isActive ? string.Format(Strings.BoostActive, remaining) : Strings.BoostOff;

    /// <summary>
    /// "12:05" for a number of seconds, as the boost countdown shows it.
    /// </summary>
    public static string Countdown(int seconds) => seconds <= 0 ? Strings.BoostOff : $"{seconds / 60:D2}:{seconds % 60:D2}";

    /// <summary>
    /// "30 days": how long a remembered login lasts.
    /// </summary>
    public static string SignInDuration(SignInDuration duration) => duration switch
    {
        Services.SignInDuration.OneDay => Strings.SignInDays1,
        Services.SignInDuration.TwoWeeks => Strings.SignInDays14,
        Services.SignInDuration.NinetyDays => Strings.SignInDays90,
        _ => Strings.SignInDays30,
    };

    public static string Version(string version) => string.Format(Strings.SettingsVersion, version);

    public static string About(string version) => $"WAFE Control · {Version(version)}";

    public static string UnitDetails(SystemInfo? info) =>
        string.Format(Strings.SettingsUnitDetails, info?.Unit?.Type ?? NoValue, info?.Unit?.Model ?? NoValue, info?.Unit?.SerialNumber ?? NoValue);

    public static string ServiceName(SystemInfo? info) => info?.Contacts?.Service?.Name ?? NoValue;

    public static string ServiceMail(SystemInfo? info) => info?.Contacts?.Service?.Mail ?? string.Empty;

    public static Uri? ServiceMailUri(SystemInfo? info) =>
        ServiceMail(info) is { Length: > 0 } mail && Uri.TryCreate($"mailto:{mail}", UriKind.Absolute, out var uri) ? uri : null;

    public static Uri? ServiceWebUri(SystemInfo? info) =>
        Uri.TryCreate(info?.Contacts?.Service?.Web, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;

    /// <summary>
    /// "wafe.eu" for "https://wafe.eu/".
    /// </summary>
    public static string ServiceWebText(SystemInfo? info) =>
        ServiceWebUri(info) is { } uri ? $"{uri.Host}{uri.PathAndQuery.TrimEnd('/')}" : string.Empty;
}
