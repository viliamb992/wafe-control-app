using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WafeControl.Core.Localization;
using WafeControl.Shared;
using WafeControl.Shared.Models;

namespace WafeControl.WinUI.Helpers;

/// <summary>
/// Pure functions for x:Bind (e.g. <c>{x:Bind helpers:Xaml.Temperature(ViewModel.IndoorTemp), Mode=OneWay}</c>).
/// </summary>
public static class Xaml
{
    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility CollapsedIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfHasValue(int? value) => VisibleIf(value.HasValue);

    public static Visibility CollapsedIfHasValue(int? value) => CollapsedIf(value.HasValue);

    public static Visibility VisibleIfHasReading(double? value) => VisibleIf(value.HasValue);

    public static Visibility VisibleIfNotNull(object? value) => VisibleIf(value is not null);

    public static bool Not(bool value) => !value;

    public static bool IsIndex(int value, int index) => value == index;

    public static bool HasText(string? value) => !string.IsNullOrEmpty(value);

    public static bool CanAdjustFlow(bool isManualMode, bool isChanging) => isManualMode && !isChanging;

    public static string Temperature(double? value) => DisplayFormat.Temperature(value);

    public static string Humidity(double? value) => DisplayFormat.Humidity(value);

    public static string Co2(int value) => DisplayFormat.Co2(value);

    public static string Co2Quality(int value) => DisplayFormat.Co2Quality(value);

    public static Brush Co2Brush(int value) => Resource<Brush>(DisplayFormat.RateCo2(value) switch
    {
        Co2Rating.Good => "SystemFillColorSuccessBrush",
        Co2Rating.Fair => "SystemFillColorCautionBrush",
        Co2Rating.Poor => "SystemFillColorCriticalBrush",
        _ => "TextFillColorSecondaryBrush",
    });

    public static double Percent(int? value) => value ?? 0;

    public static string PercentText(int? value) => DisplayFormat.Percent(value);

    public static string SystemState(bool isRunning) => DisplayFormat.SystemState(isRunning);

    public static Brush SystemStateBrush(bool isRunning) =>
        Resource<Brush>(isRunning ? "SystemFillColorSuccessBrush" : "ControlStrongFillColorDefaultBrush");

    public static string SystemSummary(string authority, int currentFlow, bool hasSensorData) =>
        DisplayFormat.SystemSummary(authority, currentFlow, hasSensorData);

    public static string NextStart(DateTime? nextStart) => DisplayFormat.NextStart(nextStart);

    /// <summary>
    /// The next scheduled start matters only while the unit runs on its schedule.
    /// </summary>
    public static Visibility VisibleIfNextStart(DateTime? nextStart, string authority, bool isRunning) =>
        VisibleIf(DisplayFormat.ShowsNextStart(nextStart, authority, isRunning));

    /// <summary>
    /// "Ventilation unit connection", plus "Last update: 14:32:05" once the unit's data time is known.
    /// </summary>
    public static string ConnectionToolTip(DateTimeOffset? lastUpdate) =>
        lastUpdate is null ? Strings.TitleBarConnection : $"{Strings.TitleBarConnection}\n{DisplayFormat.LastUpdate(lastUpdate)}";

    public static string OnlineText(bool isOnline) => DisplayFormat.Online(isOnline);

    public static Brush OnlineBrush(bool isOnline) =>
        Resource<Brush>(isOnline ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");

    public static string BoostState(bool isActive, string remaining) => DisplayFormat.BoostState(isActive, remaining);

    public static string ModeName(string? mode) => ModeNames.Operating(mode);

    public static Visibility VisibleIfAnd(bool first, bool second) => VisibleIf(first && second);

    public static Visibility VisibleIfAndNot(bool first, bool second) => VisibleIf(first && !second);

    public static Visibility VisibleIfAndNeither(bool value, bool first, bool second) => VisibleIf(value && !first && !second);

    public static Visibility VisibleIfBothAndNot(bool first, bool second, bool third) => VisibleIf(first && second && !third);

    public static bool Either(bool first, bool second) => first || second;

    public static bool IsNotScheduleMode(string mode) => mode != AppConstants.ModeSchedule;

    public static Brush ScheduleModeBrush(string mode) =>
        ScheduleModeBrushes.TryGetValue(mode, out var brush) ? brush : ScheduleModeBrushes[""];

    // Readable with white text in light and dark theme; boost matches the Wafe web app.
    private static readonly Dictionary<string, SolidColorBrush> ScheduleModeBrushes = new()
    {
        [AppConstants.ScheduleModeMin] = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2F, 0x6F, 0xD6)),
        [AppConstants.ScheduleModeAuto] = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2E, 0x8B, 0x57)),
        [AppConstants.ScheduleModeNominal] = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xB8, 0x6E, 0x0A)),
        [AppConstants.ScheduleModeBoost] = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xBC, 0x3F, 0x3A)),
        [""] = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
    };

    public static string VersionText(string version) => DisplayFormat.Version(version);

    public static string UnitDetails(SystemInfo? info) => DisplayFormat.UnitDetails(info);

    public static string ServiceName(SystemInfo? info) => DisplayFormat.ServiceName(info);

    public static string ServiceMail(SystemInfo? info) => DisplayFormat.ServiceMail(info);

    public static Uri? ServiceMailUri(SystemInfo? info) => DisplayFormat.ServiceMailUri(info);

    public static Visibility VisibleIfServiceMail(SystemInfo? info) => VisibleIf(ServiceMailUri(info) is not null);

    public static Uri? ServiceWebUri(SystemInfo? info) => DisplayFormat.ServiceWebUri(info);

    /// <summary>
    /// "wafe.eu" for "https://wafe.eu/".
    /// </summary>
    public static string ServiceWebText(SystemInfo? info) => DisplayFormat.ServiceWebText(info);

    public static Visibility VisibleIfServiceWeb(SystemInfo? info) => VisibleIf(ServiceWebUri(info) is not null);

    public static string AboutText(string version) => DisplayFormat.About(version);

    private static T Resource<T>(string key) => (T)Application.Current.Resources[key];
}
