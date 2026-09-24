using WafeControl.Core.Services;
using AppTheme = WafeControl.Core.Services.AppTheme;

namespace WafeControl.Mobile.Services;

/// <summary>
/// Keeps the settings that apply on mobile (language, theme) in MAUI <see cref="Preferences"/>.
/// The desktop-only ones (tray, window placement) keep their defaults.
/// </summary>
public sealed class PreferencesSettingsStore : ISettingsStore
{
    private const string LanguageKey = "language";
    private const string ThemeKey = "theme";

    public UserSettings Load()
    {
        var theme = (AppTheme)Preferences.Default.Get(ThemeKey, (int)AppTheme.System);
        return new UserSettings
        {
            Language = Preferences.Default.Get<string?>(LanguageKey, null),
            Theme = Enum.IsDefined(theme) ? theme : AppTheme.System,
        };
    }

    public void Save(UserSettings settings)
    {
        if (settings.Language is null)
            Preferences.Default.Remove(LanguageKey);
        else
            Preferences.Default.Set(LanguageKey, settings.Language);

        Preferences.Default.Set(ThemeKey, (int)settings.Theme);
    }
}
