using System.Globalization;
using WafeControl.Core.Localization;
using WafeControl.Shared.Models;

namespace WafeControl.Mobile.Helpers;

/// <summary>
/// Value converters for compiled bindings, e.g. <c>{Binding IndoorTemp, Converter={x:Static helpers:Converters.Temperature}}</c>.
/// The text comes from <see cref="DisplayFormat"/>, shared with the Windows app.
/// </summary>
public static class Converters
{
    public static IValueConverter Not { get; } = Create<bool>(v => !v);

    public static IValueConverter HasText { get; } = Create<string?>(v => !string.IsNullOrEmpty(v));

    public static IValueConverter IsNotNull { get; } = Create<object?>(v => v is not null);

    public static IValueConverter IsNull { get; } = Create<object?>(v => v is null);

    public static IValueConverter Temperature { get; } = Create<double?>(DisplayFormat.Temperature);

    public static IValueConverter Humidity { get; } = Create<double?>(DisplayFormat.Humidity);

    public static IValueConverter Co2 { get; } = Create<int>(DisplayFormat.Co2);

    public static IValueConverter Co2Quality { get; } = Create<int>(DisplayFormat.Co2Quality);

    public static IValueConverter Co2Rating { get; } = Create<int>(v => DisplayFormat.RateCo2(v));

    public static IValueConverter Percent { get; } = Create<int?>(DisplayFormat.Percent);

    /// <summary>
    /// Filter health in percent → progress bar value (0–1).
    /// </summary>
    public static IValueConverter Progress { get; } = Create<int?>(v => Math.Clamp(v ?? 0, 0, 100) / 100.0);

    public static IValueConverter IsFilterLow { get; } = Create<int?>(v => DisplayFormat.IsFilterLow(v));

    public static IValueConverter SystemState { get; } = Create<bool>(DisplayFormat.SystemState);

    public static IValueConverter Online { get; } = Create<bool>(DisplayFormat.Online);

    public static IValueConverter LastUpdate { get; } = Create<DateTimeOffset?>(DisplayFormat.LastUpdate);

    public static IValueConverter NextStart { get; } = Create<DateTime?>(DisplayFormat.NextStart);

    public static IValueConverter UnitDetails { get; } = Create<SystemInfo?>(DisplayFormat.UnitDetails);

    public static IValueConverter ServiceName { get; } = Create<SystemInfo?>(DisplayFormat.ServiceName);

    public static IValueConverter ServiceMail { get; } = Create<SystemInfo?>(DisplayFormat.ServiceMail);

    public static IValueConverter HasServiceMail { get; } = Create<SystemInfo?>(v => DisplayFormat.ServiceMailUri(v) is not null);

    public static IValueConverter ServiceWeb { get; } = Create<SystemInfo?>(DisplayFormat.ServiceWebText);

    public static IValueConverter HasServiceWeb { get; } = Create<SystemInfo?>(v => DisplayFormat.ServiceWebUri(v) is not null);

    public static IValueConverter About { get; } = Create<string>(DisplayFormat.About);

    /// <summary>
    /// The unit's name, or the app name while it's unknown.
    /// </summary>
    public static IValueConverter UnitTitle { get; } = Create<string?>(v => string.IsNullOrEmpty(v) ? "WAFE Control" : v);

    /// <summary>
    /// Values: operating mode (authority), current flow, has sensor data.
    /// </summary>
    public static IMultiValueConverter SystemSummary { get; } =
        Create<string?, int, bool>(DisplayFormat.SystemSummary);

    /// <summary>
    /// Values: next start, operating mode (authority), is running.
    /// </summary>
    public static IMultiValueConverter ShowsNextStart { get; } =
        Create<DateTime?, string?, bool>((next, authority, running) => DisplayFormat.ShowsNextStart(next, authority, running));

    /// <summary>
    /// True when every value is true (bools; apply <see cref="Not"/> on single bindings to negate them).
    /// </summary>
    public static IMultiValueConverter AllTrue { get; } = new AllTrueConverter();

    private static IValueConverter Create<T>(Func<T, object?> convert) => new OneWayConverter<T>(convert);

    private static IMultiValueConverter Create<T1, T2, T3>(Func<T1, T2, T3, object?> convert) => new OneWayConverter<T1, T2, T3>(convert);

    private static T Cast<T>(object? value) => value is T typed ? typed : default!;

    private sealed class OneWayConverter<T>(Func<T, object?> convert) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => convert(Cast<T>(value));

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class AllTrueConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
            values.All(value => value is true);

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class OneWayConverter<T1, T2, T3>(Func<T1, T2, T3, object?> convert) : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
            values.Length == 3 ? convert(Cast<T1>(values[0]), Cast<T2>(values[1]), Cast<T3>(values[2])) : null;

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
