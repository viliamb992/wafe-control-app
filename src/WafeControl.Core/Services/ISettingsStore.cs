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
