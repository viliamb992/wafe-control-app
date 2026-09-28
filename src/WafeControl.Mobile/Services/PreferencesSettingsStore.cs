using WafeControl.Core.Services;
using AppTheme = WafeControl.Core.Services.AppTheme;

namespace WafeControl.Mobile.Services;

/// <summary>
/// Keeps the settings that apply on mobile (language, theme, crash reports, sign-in) in MAUI <see cref="Preferences"/>.
/// The desktop-only ones (tray, window placement, updates) keep their defaults.
/// </summary>
public sealed class PreferencesSettingsStore : ISettingsStore
{
    private const string LanguageKey = "language";
    private const string ThemeKey = "theme";

    // -1 not asked yet, 0 no, 1 yes.
    private const string CrashReportsKey = "crash-reports";

    private const string SignInMethodKey = "sign-in-method";

    // Days; missing = the platform default.
    private const string StaySignedInForKey = "stay-signed-in-days";
    private const string BiometricOfferAnsweredKey = "biometric-offer-answered";

#if ANDROID
    private static readonly SignInDuration? DefaultStaySignedInFor = SignInDuration.ThirtyDays;
#else
    // iOS keeps a remembered login without a time limit, like Windows.
    private static readonly SignInDuration? DefaultStaySignedInFor = null;
#endif

    public UserSettings Load()
    {
        var theme = (AppTheme)Preferences.Default.Get(ThemeKey, (int)AppTheme.System);
        var crashReports = Preferences.Default.Get(CrashReportsKey, -1);
        var method = (SignInMethod)Preferences.Default.Get(SignInMethodKey, (int)SignInMethod.StaySignedIn);
        var days = (SignInDuration)Preferences.Default.Get(StaySignedInForKey, 0);
        return new UserSettings
        {
            Language = Preferences.Default.Get<string?>(LanguageKey, null),
            Theme = Enum.IsDefined(theme) ? theme : AppTheme.System,
            CrashReports = crashReports < 0 ? null : crashReports == 1,
            SignInMethod = Enum.IsDefined(method) ? method : SignInMethod.StaySignedIn,
            StaySignedInFor = Enum.IsDefined(days) ? days : DefaultStaySignedInFor,
            BiometricOfferAnswered = Preferences.Default.Get(BiometricOfferAnsweredKey, false),
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

        Preferences.Default.Set(SignInMethodKey, (int)settings.SignInMethod);
        if (settings.StaySignedInFor is { } days)
            Preferences.Default.Set(StaySignedInForKey, (int)days);
        else
            Preferences.Default.Remove(StaySignedInForKey);
        Preferences.Default.Set(BiometricOfferAnsweredKey, settings.BiometricOfferAnswered);
    }
}
