namespace WafeControl.Core.Services;

/// <summary>
/// Preferences kept between app launches.
/// </summary>
public sealed record UserSettings
{
    /// <summary>
    /// Language code (e.g. "cs"); null until the user picks one.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Closing the window keeps the app running in the tray (true) or exits it (false).
    /// </summary>
    public bool MinimizeToTray { get; init; } = true;

    /// <summary>
    /// Launch with only the tray icon, without showing the window.
    /// </summary>
    public bool StartInTray { get; init; }

    /// <summary>
    /// Light or dark appearance, or the one set in the operating system.
    /// </summary>
    public AppTheme Theme { get; init; }

    /// <summary>
    /// Where the main window was when it last closed; null until then. Desktop only.
    /// </summary>
    public WindowPlacement? MainWindow { get; init; }

    /// <summary>
    /// Send crash reports: null until the user answered the question.
    /// </summary>
    public bool? CrashReports { get; init; }

    /// <summary>
    /// Download new versions in the background (Windows).
    /// </summary>
    public bool AutoDownloadUpdates { get; init; } = true;

    /// <summary>
    /// Offer pre-release versions too.
    /// </summary>
    public bool BetaUpdates { get; init; }

    /// <summary>
    /// The version that ran last, to say "Updated to …" once after an update (Windows).
    /// </summary>
    public string? LastRunVersion { get; init; }

    /// <summary>
    /// The new version whose banner the user closed, so it isn't shown again until a newer one (Android).
    /// </summary>
    public string? DismissedUpdateVersion { get; init; }

    /// <summary>
    /// Time of the newest crash already read from the Windows event log, so each is reported once (Windows).
    /// </summary>
    public DateTimeOffset? LastSeenCrashEventTime { get; init; }

    /// <summary>
    /// How a remembered login is used when the app opens (Android).
    /// </summary>
    public SignInMethod SignInMethod { get; init; }

    /// <summary>
    /// How long <see cref="SignInMethod.StaySignedIn"/> lasts after the password was typed; null = no limit
    /// (Windows and iOS).
    /// </summary>
    public SignInDuration? StaySignedInFor { get; init; }

    /// <summary>
    /// The one-time "Sign in with fingerprint or face?" question was answered (Android).
    /// </summary>
    public bool BiometricOfferAnswered { get; init; }
}

public enum SignInMethod
{
    /// <summary>
    /// Sign in without asking, until <see cref="UserSettings.StaySignedInFor"/> has passed.
    /// </summary>
    StaySignedIn,

    /// <summary>
    /// The phone's fingerprint or face unlocks the remembered login each time the app opens.
    /// </summary>
    Biometric,
}

/// <summary>
/// How long a remembered login lasts; the value is the number of days.
/// </summary>
public enum SignInDuration
{
    OneDay = 1,
    TwoWeeks = 14,
    ThirtyDays = 30,
    NinetyDays = 90,
}

public enum AppTheme
{
    /// <summary>
    /// Follow the operating system's light/dark setting.
    /// </summary>
    System,
    Light,
    Dark,
}

/// <summary>
/// A window's restored (not maximized) bounds in physical pixels, plus whether it was maximized.
/// </summary>
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool IsMaximized);

/// <summary>
/// Loads and saves <see cref="UserSettings"/> in platform storage. Each app registers its own implementation.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// The saved settings, or defaults when nothing is saved or the stored data can't be read.
    /// </summary>
    UserSettings Load();

    void Save(UserSettings settings);
}
