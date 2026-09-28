using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.Threading;

namespace WafeControl.Core.ViewModels;

/// <summary>
/// The update button and the Updates section of Settings. Checks shortly after start and then every few hours;
/// with automatic downloads on, a new version is downloaded in the background and the button turns into
/// "Restart to update". Create it on the UI thread.
/// </summary>
public sealed partial class UpdateViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);

    private readonly IUpdateService _updates;
    private readonly ISettingsStore _settings;
    private readonly ILogger<UpdateViewModel> _logger;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext;
    private ITimer? _timer;
    private bool _isChecking;

    public UpdateViewModel(IUpdateService updates, ISettingsStore settings, ILogger<UpdateViewModel> logger, TimeProvider? timeProvider = null)
    {
        _updates = updates;
        _settings = settings;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _uiContext = SynchronizationContext.Current;
        _updates.Changed += OnUpdatesChanged;
    }

    /// <summary>
    /// The window prepares for exit (saves its place, removes the tray icon), then calls <see cref="Restart"/>.
    /// </summary>
    public event EventHandler? RestartRequested;

    public bool IsSupported => _updates.IsSupported;

    public UpdateStage Stage => _updates.Stage;

    /// <summary>
    /// The title bar button: shown while there's something to download, downloading or to restart for.
    /// </summary>
    public bool IsButtonVisible => Stage is UpdateStage.Available or UpdateStage.Downloading or UpdateStage.ReadyToRestart;

    public bool IsReady => Stage == UpdateStage.ReadyToRestart;

    public bool IsDownloading => Stage == UpdateStage.Downloading;

    public string ButtonText => Stage switch
    {
        UpdateStage.Downloading => string.Format(Strings.UpdateDownloading, _updates.DownloadProgress),
        UpdateStage.ReadyToRestart => Strings.UpdateRestart,
        _ => Strings.UpdateAvailable,
    };

    /// <summary>
    /// "Version 1.3.0 is ready" / "Version 1.3.0 is available".
    /// </summary>
    public string Title => string.Format(IsReady ? Strings.UpdateReadyTitle : Strings.UpdateAvailableTitle, _updates.AvailableVersion);

    public string? ReleaseNotes => _updates.ReleaseNotes;

    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotes);

    /// <summary>
    /// "Download" or "Restart now" in the flyout.
    /// </summary>
    public string PrimaryText => IsReady ? Strings.UpdateRestartNow : Strings.UpdateDownloadNow;

    /// <summary>
    /// The Updates section of Settings.
    /// </summary>
    public string StatusText => !IsSupported ? Strings.UpdateNotInstalled
        : Stage switch
        {
            UpdateStage.Checking => Strings.UpdateChecking,
            UpdateStage.Failed => Strings.UpdateCheckFailed,
            UpdateStage.Available or UpdateStage.Downloading or UpdateStage.ReadyToRestart => Title,
            _ => LastChecked is { } time
                ? string.Format(Strings.UpdateUpToDate, time.LocalDateTime.ToString("t", CultureInfo.CurrentCulture))
                : string.Empty,
        };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial DateTimeOffset? LastChecked { get; private set; }

    /// <summary>
    /// Starts the checks: the first soon after start, then every <see cref="CheckInterval"/>.
    /// </summary>
    public void Start()
    {
        if (!IsSupported || _timer is not null)
            return;

        _timer = _time.CreateTimer(_ => RunOnUiThread(() => CheckAsync().Forget(_logger, "Update check")), null, FirstCheckDelay, CheckInterval);
    }

    /// <summary>
    /// Checks now; downloads right away when automatic downloads are on.
    /// </summary>
    [RelayCommand]
    public async Task CheckAsync()
    {
        if (!IsSupported || _isChecking || IsReady || IsDownloading)
            return;

        _isChecking = true;
        try
        {
            var settings = _settings.Load();
            await _updates.CheckAsync(settings.BetaUpdates);
            LastChecked = _time.GetUtcNow();

            if (Stage == UpdateStage.Available && settings.AutoDownloadUpdates)
                await _updates.DownloadAsync();
        }
        finally
        {
            _isChecking = false;
        }
    }

    /// <summary>
    /// The button: downloads when a version is available, asks for the restart once it's ready.
    /// </summary>
    [RelayCommand]
    private async Task PrimaryAsync()
    {
        if (IsReady)
            RestartRequested?.Invoke(this, EventArgs.Empty);
        else if (Stage == UpdateStage.Available)
            await _updates.DownloadAsync();
    }

    /// <summary>
    /// Applies the update and starts the new version. Call after the window prepared for exit.
    /// </summary>
    public void Restart(bool startInTray) => _updates.RestartToApply(startInTray);

    /// <summary>
    /// On exit: a prepared update is applied so the next start is the new version.
    /// </summary>
    public void ApplyOnExit()
    {
        if (IsReady)
            _updates.ApplyOnExit();
    }

    /// <summary>
    /// The version this run updated to, once: null when the app didn't change since the last run.
    /// </summary>
    public string? TakeUpdatedVersion()
    {
        var settings = _settings.Load();
        var current = AppVersion.Current;
        if (settings.LastRunVersion == current)
            return null;

        _settings.Save(settings with { LastRunVersion = current });
        return settings.LastRunVersion is null ? null : current;
    }

    private void OnUpdatesChanged(object? sender, EventArgs e) => RunOnUiThread(() =>
    {
        OnPropertyChanged(nameof(Stage));
        OnPropertyChanged(nameof(IsButtonVisible));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ReleaseNotes));
        OnPropertyChanged(nameof(HasReleaseNotes));
        OnPropertyChanged(nameof(PrimaryText));
        OnPropertyChanged(nameof(StatusText));
    });

    private void RunOnUiThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _updates.Changed -= OnUpdatesChanged;
    }
}
