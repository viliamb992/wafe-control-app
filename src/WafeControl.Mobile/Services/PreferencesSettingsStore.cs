using WafeControl.Core.Services;
using AppTheme = WafeControl.Core.Services.AppTheme;

namespace WafeControl.Mobile.Services;

/// <summary>
/// Keeps the settings that apply on mobile (language, theme, crash reports) in MAUI <see cref="Preferences"/>.
/// The desktop-only ones (tray, window placement, updates) keep their defaults.
/// </summary>
public sealed class PreferencesSettingsStore : ISettingsStore
{
    private const string LanguageKey = "language";
    private const string ThemeKey = "theme";

    // -1 not asked yet, 0 no, 1 yes.
    private const string CrashReportsKey = "crash-reports";

    public UserSettings Load()
    {
        var theme = (AppTheme)Preferences.Default.Get(ThemeKey, (int)AppTheme.System);
        var crashReports = Preferences.Default.Get(CrashReportsKey, -1);
        return new UserSettings
        {
            Language = Preferences.Default.Get<string?>(LanguageKey, null),
            Theme = Enum.IsDefined(theme) ? theme : AppTheme.System,
            CrashReports = crashReports < 0 ? null : crashReports == 1,
        };
    }

    public void Save(UserSettings settings)
    {
        if (settings.Language is null)
            Preferences.Default.Remove(LanguageKey);
        else
            Preferences.Default.Set(LanguageKey, settings.Language);

        Preferences.Default.Set(ThemeKey, (int)settings.Theme);
        Preferences.Default.Set(CrashReportsKey, settings.CrashReports switch { null => -1, true => 1, false => 0 });
    }
}
