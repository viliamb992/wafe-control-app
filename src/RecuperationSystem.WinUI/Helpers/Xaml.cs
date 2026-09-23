using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.ViewModels.Schedule;
using RecuperationSystem.Shared;

namespace RecuperationSystem.WinUI.Helpers;

/// <summary>
/// Pure functions for x:Bind (e.g. <c>{x:Bind helpers:Xaml.Temperature(ViewModel.IndoorTemp), Mode=OneWay}</c>).
/// </summary>
public static class Xaml
{
    private const string NoValue = ModeNames.NoValue;

    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility CollapsedIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfHasValue(int? value) => VisibleIf(value.HasValue);

    public static Visibility CollapsedIfHasValue(int? value) => CollapsedIf(value.HasValue);

    public static Visibility VisibleIfHasReading(double? value) => VisibleIf(value.HasValue);

    public static bool Not(bool value) => !value;

    public static bool HasText(string? value) => !string.IsNullOrEmpty(value);

    public static bool CanAdjustFlow(bool isManualMode, bool isChanging) => isManualMode && !isChanging;

    public static string Temperature(double? value) =>
        value is { } v ? $"{v.ToString("0.0", CultureInfo.CurrentCulture)} °C" : NoValue;

    public static string Humidity(double? value) => value is { } v ? $"{v:0} %" : NoValue;

    public static string Flow(int value) => $"{value} m³/h";

    public static string Co2(int value) => value > 0 ? $"{value} ppm" : NoValue;

    public static string Co2Quality(int value) => value switch
    {
        <= 0 => Strings.Co2NoReading,
        < 800 => Strings.Co2Good,
        < 1200 => Strings.Co2Fair,
        _ => Strings.Co2Poor,
    };

    public static Brush Co2Brush(int value) => Resource<Brush>(value switch
    {
        <= 0 => "TextFillColorSecondaryBrush",
        < 800 => "SystemFillColorSuccessBrush",
        < 1200 => "SystemFillColorCautionBrush",
        _ => "SystemFillColorCriticalBrush",
    });

    public static double Percent(int? value) => value ?? 0;

    public static string PercentText(int? value) => value is { } v ? $"{v} %" : NoValue;

    public static string SystemState(bool isRunning) => isRunning ? Strings.SystemRunning : Strings.SystemStopped;

    public static Brush SystemStateBrush(bool isRunning) =>
        Resource<Brush>(isRunning ? "SystemFillColorSuccessBrush" : "ControlStrongFillColorDefaultBrush");

    public static string SystemSummary(string authority, int currentFlow, bool isOnline) =>
        isOnline
            ? string.Format(Strings.SystemSummary, ModeName(authority), Flow(currentFlow))
            : string.Format(Strings.SystemSummaryModeOnly, ModeName(authority));

    public static string NextStart(DateTime? nextStart) =>
        nextStart is { } next ? string.Format(Strings.SystemNextStart, ScheduleFormat.DateAndTime(next)) : string.Empty;

    /// <summary>
    /// The next scheduled start matters only while the unit runs on its schedule.
    /// </summary>
    public static Visibility VisibleIfNextStart(DateTime? nextStart, string authority, bool isRunning) =>
        VisibleIf(nextStart.HasValue && isRunning && authority == AppConstants.ModeSchedule);

    public static string OnlineText(bool isOnline) => isOnline ? Strings.TitleBarOnline : Strings.TitleBarOffline;

    public static Brush OnlineBrush(bool isOnline) =>
        Resource<Brush>(isOnline ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");

    public static string BoostState(bool isActive, string remaining) => isActive ? string.Format(Strings.BoostActive, remaining) : Strings.BoostOff;

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

    private static T Resource<T>(string key) => (T)Application.Current.Resources[key];
}
