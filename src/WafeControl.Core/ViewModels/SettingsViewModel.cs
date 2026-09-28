using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;

namespace WafeControl.Core.ViewModels;

/// <summary>
/// The settings screen. Changes apply immediately and are remembered for the next launch.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settings;
    private readonly IStartupRegistration _startup;
    private readonly ICrashReports _crashReports;
    private readonly bool _loaded;

    public SettingsViewModel(ILocalizationService localization, ISettingsStore settings, IStartupRegistration startup, ICrashReports? crashReports = null)
    {
        _localization = localization;
        _settings = settings;
        _startup = startup;
        _crashReports = crashReports ?? new NoCrashReports();

        var saved = settings.Load();
        SelectedLanguage = localization.Current;
        MinimizeToTray = saved.MinimizeToTray;
        StartInTray = saved.StartInTray;
        Theme = saved.Theme;
        RunAtStartup = startup.IsEnabled;
        CrashReportsEnabled = saved.CrashReports == true;
        AutoDownloadUpdates = saved.AutoDownloadUpdates;
        BetaUpdates = saved.BetaUpdates;
        _loaded = true;
    }

    /// <summary>
    /// Whether the settings screen is shown.
    /// </summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    public IReadOnlyList<AppLanguage> Languages => _localization.Languages;

    [ObservableProperty]
    public partial AppLanguage SelectedLanguage { get; set; }

    /// <summary>
    /// Closing the window keeps the app in the tray; otherwise it exits.
    /// </summary>
    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    /// <summary>
    /// Start the app when the user signs in to Windows.
    /// </summary>
    [ObservableProperty]
    public partial bool RunAtStartup { get; set; }

    /// <summary>
    /// Launch with only the tray icon; the window opens from the tray (or when a sign-in is needed).
    /// </summary>
    [ObservableProperty]
    public partial bool StartInTray { get; set; }

    /// <summary>
    /// Light, dark, or the system's appearance. The app applies it when it changes.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIndex))]
    public partial AppTheme Theme { get; set; }

    /// <summary>
    /// <see cref="Theme"/> as a list position (System, Light, Dark), for a list of choices.
    /// A list briefly reporting no selection (-1) is ignored.
    /// </summary>
    public int ThemeIndex
    {
        get => (int)Theme;
        set
        {
            if (Enum.IsDefined((AppTheme)value))
                Theme = (AppTheme)value;
        }
    }

    public string Version => AppVersion.Current;

    /// <summary>
    /// Send crash reports (opt-in). Only offered when <see cref="IsCrashReportsAvailable"/>.
    /// </summary>
    [ObservableProperty]
    public partial bool CrashReportsEnabled { get; set; }

    /// <summary>
    /// A release build with a reporting address; development builds hide the setting.
    /// </summary>
    public bool IsCrashReportsAvailable => _crashReports.IsAvailable;

    /// <summary>
    /// Turning reports on needs a restart on this platform; the setting says so.
    /// </summary>
    public bool ShowsCrashReportsRestartHint => !_crashReports.AppliesImmediately;

    /// <summary>
    /// The user hasn't been asked about crash reports yet (ask once, after the first sign-in).
    /// </summary>
    public bool NeedsCrashReportConsent => IsCrashReportsAvailable && _settings.Load().CrashReports is null;

    /// <summary>
    /// The answer to the one-time question; saved even when it's "no", so it isn't asked again.
    /// </summary>
    public void AnswerCrashReportConsent(bool send)
    {
        _settings.Save(_settings.Load() with { CrashReports = send });
        CrashReportsEnabled = send;
        _crashReports.Apply(send);
    }

    /// <summary>
    /// Download new versions in the background (Windows).
    /// </summary>
    [ObservableProperty]
    public partial bool AutoDownloadUpdates { get; set; }

    /// <summary>
    /// Offer pre-release versions (Windows).
    /// </summary>
    [ObservableProperty]
    public partial bool BetaUpdates { get; set; }

    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        // A list control briefly reports no selection while its items are rebuilt;
        // the constructor selects the current language.
        if (value is not null && value != _localization.Current)
            _localization.SetLanguage(value.Code);
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        // The constructor loads the saved value; only save real changes.
        var saved = _settings.Load();
        if (saved.MinimizeToTray != value)
            _settings.Save(saved with { MinimizeToTray = value });
    }

    partial void OnStartInTrayChanged(bool value)
    {
        var saved = _settings.Load();
        if (saved.StartInTray != value)
            _settings.Save(saved with { StartInTray = value });
    }

    partial void OnThemeChanged(AppTheme value)
    {
        var saved = _settings.Load();
        if (saved.Theme != value)
            _settings.Save(saved with { Theme = value });
    }

    partial void OnCrashReportsEnabledChanged(bool value)
    {
        if (!_loaded)
            return;

        var saved = _settings.Load();
        if (saved.CrashReports != value)
        {
            _settings.Save(saved with { CrashReports = value });
            _crashReports.Apply(value);
        }
    }

    partial void OnAutoDownloadUpdatesChanged(bool value)
    {
        var saved = _settings.Load();
        if (saved.AutoDownloadUpdates != value)
            _settings.Save(saved with { AutoDownloadUpdates = value });
    }

    partial void OnBetaUpdatesChanged(bool value)
    {
        var saved = _settings.Load();
        if (saved.BetaUpdates != value)
            _settings.Save(saved with { BetaUpdates = value });
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        // The system is the source of truth: nothing to do when it already matches (also on load),
        // and flip the switch back when the change is refused.
        if (value != _startup.IsEnabled && !_startup.TrySetEnabled(value))
            RunAtStartup = !value;
    }

    [RelayCommand]
    private void Open() => IsOpen = true;

    [RelayCommand]
    public void Close() => IsOpen = false;
}
