using WafeControl.Shared;

namespace WafeControl.Mobile.Helpers;

/// <summary>
/// Color tokens from Resources/Styles/Colors.xaml for controls built in code (DESIGN.md, section 2).
/// </summary>
public static class Theme
{
    /// <summary>
    /// Binds <paramref name="property"/> to a token's light and dark value, so it follows theme changes.
    /// </summary>
    public static T SetColor<T>(this T target, BindableProperty property, string token)
        where T : BindableObject
    {
        if (property.ReturnType == typeof(Brush))
            target.SetAppTheme<Brush>(property, new SolidColorBrush(Light(token)), new SolidColorBrush(Dark(token)));
        else
            target.SetAppThemeColor(property, Light(token), Dark(token));
        return target;
    }

    /// <summary>
    /// Replaces a token set with <see cref="SetColor{T}"/> by a fixed value.
    /// </summary>
    public static T SetFixed<T>(this T target, BindableProperty property, object value)
        where T : BindableObject
    {
        target.RemoveBinding(property);
        target.SetValue(property, value);
        return target;
    }

    public static Color Light(string token) => Resource(token + "Light");

    public static Color Dark(string token) => Resource(token + "Dark");

    /// <summary>
    /// A token's value in the theme shown now, for platform views that can't bind to the theme.
    /// </summary>
    public static Color Current(string token) =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? Dark(token) : Light(token);

    public static Color ScheduleMode(string mode) => Resource(mode switch
    {
        AppConstants.ScheduleModeMin => "ScheduleModeMin",
        AppConstants.ScheduleModeAuto => "ScheduleModeAuto",
        AppConstants.ScheduleModeNominal => "ScheduleModeNominal",
        AppConstants.ScheduleModeBoost => "ScheduleModeBoost",
        _ => "ScheduleModeUnknown",
    });

    public static Style Style(string key) => (Style)Find(key);

    private static Color Resource(string key) => (Color)Find(key);

    private static object Find(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Resource '{key}' is missing from Resources/Styles.");
}
