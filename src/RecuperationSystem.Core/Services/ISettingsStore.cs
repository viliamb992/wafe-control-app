namespace RecuperationSystem.Core.Services;

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
}

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
