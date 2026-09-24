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

    public SettingsViewModel(ILocalizationService localization, ISettingsStore settings, IStartupRegistration startup)
    {
        _localization = localization;
        _settings = settings;
        _startup = startup;

        var saved = settings.Load();
        SelectedLanguage = localization.Current;
        MinimizeToTray = saved.MinimizeToTray;
        StartInTray = saved.StartInTray;
        Theme = saved.Theme;
        RunAtStartup = startup.IsEnabled;
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
