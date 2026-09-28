namespace WafeControl.Core.Services;

public enum UpdateStage
{
    /// <summary>
    /// Nothing known: not checked yet, or up to date.
    /// </summary>
    None,
    Checking,

    /// <summary>
    /// A newer version exists but isn't downloaded (automatic downloads are off, or a download failed).
    /// </summary>
    Available,
    Downloading,

    /// <summary>
    /// Downloaded and prepared; a restart applies it.
    /// </summary>
    ReadyToRestart,

    /// <summary>
    /// The last check failed (no network, GitHub unavailable).
    /// </summary>
    Failed,
}

/// <summary>
/// Finds, downloads and applies new versions of the app. Windows only; elsewhere <see cref="NoUpdates"/>.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// False when the app isn't installed (e.g. run from the IDE): there's nothing to update.
    /// </summary>
    bool IsSupported { get; }

    UpdateStage Stage { get; }

    /// <summary>
    /// The newer version, once a check found one.
    /// </summary>
    string? AvailableVersion { get; }

    /// <summary>
    /// 0–100 while <see cref="UpdateStage.Downloading"/>.
    /// </summary>
    int DownloadProgress { get; }

    /// <summary>
    /// Release notes of <see cref="AvailableVersion"/> (Markdown), if the release has any.
    /// </summary>
    string? ReleaseNotes { get; }

    /// <summary>
    /// Raised when anything above changes; possibly on a background thread.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Looks for a newer version. Doesn't throw: a failure ends in <see cref="UpdateStage.Failed"/>.
    /// </summary>
    Task CheckAsync(bool includePrereleases, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and prepares <see cref="AvailableVersion"/>. Doesn't throw: a failure goes back to Available.
    /// </summary>
    Task DownloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exits, applies the prepared update and starts the new version (in the tray when asked). Doesn't return.
    /// </summary>
    void RestartToApply(bool startInTray);

    /// <summary>
    /// Applies a prepared update after the app exits, without starting it again. Call on the way out.
    /// </summary>
    void ApplyOnExit();
}

/// <summary>
/// No updates: platforms and builds without an updater.
/// </summary>
public sealed class NoUpdates : IUpdateService
{
    public bool IsSupported => false;
    public UpdateStage Stage => UpdateStage.None;
    public string? AvailableVersion => null;
    public int DownloadProgress => 0;
    public string? ReleaseNotes => null;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public Task CheckAsync(bool includePrereleases, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DownloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void RestartToApply(bool startInTray)
    {
    }

    public void ApplyOnExit()
    {
    }
}
