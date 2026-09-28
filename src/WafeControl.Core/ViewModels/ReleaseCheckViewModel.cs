using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;

namespace WafeControl.Core.ViewModels;

/// <summary>
/// "Version 1.3.0 is available" for an app that nothing updates on its own (the sideloaded Android app): checks
/// the published releases at start and on return to the app, at most every <see cref="CheckInterval"/>, and
/// offers the file to install. Windows updates itself (<see cref="UpdateViewModel"/>).
/// </summary>
public sealed partial class ReleaseCheckViewModel : ObservableObject
{
    public const string AndroidTagPrefix = "android-v";

    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    private readonly IReleaseFeed _feed;
    private readonly ISettingsStore _settings;
    private readonly ILogger<ReleaseCheckViewModel> _logger;
    private readonly TimeProvider _time;
    private readonly ReleaseVersion? _current;
    private string? _tagPrefix;

    public ReleaseCheckViewModel(
        IReleaseFeed feed,
        ISettingsStore settings,
        ILogger<ReleaseCheckViewModel> logger,
        TimeProvider? timeProvider = null,
        string? currentVersion = null)
    {
        _feed = feed;
        _settings = settings;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _current = ReleaseVersion.TryParse(currentVersion ?? AppVersion.Current);
    }

    /// <summary>
    /// Asks the platform to open a link: the file to install or the release page.
    /// </summary>
    public event EventHandler<Uri>? OpenRequested;

    /// <summary>
    /// The platform has releases to check (<see cref="Enable"/>); otherwise the Updates section is hidden.
    /// </summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial UpdateStage Stage { get; private set; }

    /// <summary>
    /// A release newer than this app; null when up to date or not checked yet.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable), nameof(AvailableTitle), nameof(BannerTitle), nameof(BannerMessage), nameof(StatusText))]
    public partial AppRelease? Available { get; private set; }

    /// <summary>
    /// The user closed the banner for this version; Settings still offers it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BannerTitle), nameof(BannerMessage))]
    public partial bool IsDismissed { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial DateTimeOffset? LastChecked { get; private set; }

    public bool IsAvailable => Available is not null;

    /// <summary>
    /// "Version 1.3.0 is available".
    /// </summary>
    public string? AvailableTitle => Available is { } release ? string.Format(Strings.UpdateAvailableTitle, release.Version) : null;

    /// <summary>
    /// The banner's title; null hides it.
    /// </summary>
    public string? BannerTitle => IsDismissed ? null : AvailableTitle;

    public string? BannerMessage => BannerTitle is null ? null : Strings.UpdateApkMessage;

    /// <summary>
    /// The Updates section of Settings.
    /// </summary>
    public string StatusText => Stage switch
    {
        UpdateStage.Checking => Strings.UpdateChecking,
        UpdateStage.Failed => Strings.UpdateCheckFailed,
        _ => AvailableTitle
            ?? (LastChecked is { } time
                ? string.Format(Strings.UpdateUpToDate, time.ToLocalTime().ToString("t", CultureInfo.CurrentCulture))
                : string.Empty),
    };

    /// <summary>
    /// Turns the checks on for releases tagged <paramref name="tagPrefix"/> (e.g. <see cref="AndroidTagPrefix"/>).
    /// </summary>
    public void Enable(string tagPrefix)
    {
        _tagPrefix = tagPrefix;
        IsEnabled = true;
    }

    /// <summary>
    /// Checks unless the last check was recent; call at start and when the app returns to the foreground.
    /// </summary>
    public Task CheckIfDueAsync() =>
        LastChecked is { } last && _time.GetUtcNow() - last < CheckInterval ? Task.CompletedTask : CheckAsync();

    /// <summary>
    /// Checks now. Doesn't throw: a failure shows in <see cref="StatusText"/>, and the banner keeps what it had.
    /// </summary>
    [RelayCommand]
    public async Task CheckAsync()
    {
        if (!IsEnabled || _tagPrefix is null || Stage == UpdateStage.Checking)
            return;

        Stage = UpdateStage.Checking;
        try
        {
            var settings = _settings.Load();
            var latest = await _feed.GetLatestAsync(_tagPrefix, settings.BetaUpdates || _current?.IsPrerelease == true);

            // A build without a release version (run from the IDE) is older than every release.
            Available = latest is not null && latest.Version.CompareTo(_current) > 0 ? latest : null;
            IsDismissed = Available is not null && settings.DismissedUpdateVersion == Available.Version.ToString();
            LastChecked = _time.GetUtcNow();
            Stage = UpdateStage.None;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Checking for a new release failed");
            Stage = UpdateStage.Failed;
        }
    }

    /// <summary>
    /// Opens the file to install (Android downloads it; opening it installs over this version), or the release page.
    /// </summary>
    [RelayCommand]
    private void Download()
    {
        if (Available is { } release)
            OpenRequested?.Invoke(this, release.Download ?? release.Page);
    }

    /// <summary>
    /// The release page, with what changed.
    /// </summary>
    [RelayCommand]
    private void OpenReleaseNotes()
    {
        if (Available is { } release)
            OpenRequested?.Invoke(this, release.Page);
    }

    /// <summary>
    /// Hides the banner until a newer version comes out.
    /// </summary>
    [RelayCommand]
    private void Dismiss()
    {
        if (Available is not { } release)
            return;

        _settings.Save(_settings.Load() with { DismissedUpdateVersion = release.Version.ToString() });
        IsDismissed = true;
    }
}
