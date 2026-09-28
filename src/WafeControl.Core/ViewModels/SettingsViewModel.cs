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
    private readonly IBiometricAuth _biometric;
    private readonly IAuthenticationService? _authentication;
    private readonly bool _loaded;

    public SettingsViewModel(
        ILocalizationService localization,
        ISettingsStore settings,
        IStartupRegistration startup,
        ICrashReports? crashReports = null,
        IBiometricAuth? biometric = null,
        IAuthenticationService? authentication = null)
    {
        _localization = localization;
        _settings = settings;
        _startup = startup;
        _crashReports = crashReports ?? new NoCrashReports();
        _biometric = biometric ?? new NoBiometricAuth();
        _authentication = authentication;

        var saved = settings.Load();
        SelectedLanguage = localization.Current;
        MinimizeToTray = saved.MinimizeToTray;
        StartInTray = saved.StartInTray;
        Theme = saved.Theme;
        RunAtStartup = startup.IsEnabled;
        CrashReportsEnabled = saved.CrashReports == true;
        AutoDownloadUpdates = saved.AutoDownloadUpdates;
        BetaUpdates = saved.BetaUpdates;
        SignInMethod = saved.SignInMethod;
        StaySignedInFor = saved.StaySignedInFor ?? SignInDuration.ThirtyDays;
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

    #region Sign-in (Android)

    /// <summary>
    /// The platform offers sign-in methods (Android); otherwise the card is hidden.
    /// </summary>
    public bool IsSignInCardVisible => _biometric.IsSupported;

    /// <summary>
    /// How a remembered login is used. Changed only through <see cref="SetSignInMethodAsync"/>, which checks the
    /// fingerprint or face first.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStaySignedIn), nameof(CanChooseBiometric))]
    public partial SignInMethod SignInMethod { get; private set; }

    public bool IsStaySignedIn => SignInMethod == SignInMethod.StaySignedIn;

    /// <summary>
    /// How long Stay signed in lasts after the password was typed. A change applies to the saved login at once.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StaySignedInForIndex))]
    public partial SignInDuration StaySignedInFor { get; set; }

    /// <summary>
    /// The choices for <see cref="StaySignedInFor"/>, shortest first.
    /// </summary>
    public IReadOnlyList<SignInDuration> Durations { get; } = Enum.GetValues<SignInDuration>();

    /// <summary>
    /// <see cref="StaySignedInFor"/> as a position in <see cref="Durations"/>; -1 (no selection) is ignored.
    /// </summary>
    public int StaySignedInForIndex
    {
        get => Durations.ToList().IndexOf(StaySignedInFor);
        set
        {
            if (value >= 0 && value < Durations.Count)
                StaySignedInFor = Durations[value];
        }
    }

    /// <summary>
    /// A fingerprint or face is set up, so the check can be shown.
    /// </summary>
    public bool IsBiometricAvailable => _biometric.Availability == BiometricAvailability.Available;

    /// <summary>
    /// The fingerprint or face option can be picked: it's available, or already on (to switch away from).
    /// </summary>
    public bool CanChooseBiometric => IsBiometricAvailable || SignInMethod == SignInMethod.Biometric;

    /// <summary>
    /// Why fingerprint or face can't be picked; null when it can.
    /// </summary>
    public string? BiometricUnavailableReason => _biometric.Availability switch
    {
        BiometricAvailability.Available => null,
        BiometricAvailability.NoHardware => Strings.SettingsSignInBiometricNoHardware,
        _ => Strings.SettingsSignInBiometricUnavailable,
    };

    /// <summary>
    /// Reads the phone's fingerprint and face setup again (it may have changed in Android settings meanwhile).
    /// </summary>
    public void RefreshBiometricAvailability()
    {
        OnPropertyChanged(nameof(IsBiometricAvailable));
        OnPropertyChanged(nameof(CanChooseBiometric));
        OnPropertyChanged(nameof(BiometricUnavailableReason));
    }

    /// <summary>
    /// Switches the sign-in method once the fingerprint or face check passes, both ways: turning it off must not
    /// be a way around it. False when the method stays as it was.
    /// </summary>
    public async Task<bool> SetSignInMethodAsync(SignInMethod method)
    {
        if (method == SignInMethod)
            return true;
        if (!IsSignInCardVisible || (method == SignInMethod.Biometric && !IsBiometricAvailable))
            return false;

        // Switching away with no fingerprint or face left: there's nothing to check (removing them needs the
        // phone's own PIN), and the next start would fall back anyway.
        var needsCheck = method == SignInMethod.Biometric || IsBiometricAvailable;
        if (needsCheck && await _biometric.AuthenticateAsync() != BiometricResult.Succeeded)
            return false;

        _settings.Save(_settings.Load() with { SignInMethod = method });
        SignInMethod = method;

        // The time limit starts now, not from a password typed long ago.
        if (method == SignInMethod.StaySignedIn && _authentication is not null)
            await _authentication.RestartSignInPeriodAsync();

        return true;
    }

    /// <summary>
    /// Ask once, after a sign-in, whether to use fingerprint or face: not answered yet, one is set up, and a login
    /// is saved to unlock. Users of earlier versions have no answer saved, so they are asked after the update too.
    /// </summary>
    public bool NeedsBiometricOffer =>
        IsSignInCardVisible
        && !_settings.Load().BiometricOfferAnswered
        && IsBiometricAvailable
        && _authentication is { HasRememberedLogin: true, IsDemo: false };

    /// <summary>
    /// "Use fingerprint or face": checks it once; on success the method becomes <see cref="SignInMethod.Biometric"/>.
    /// The answer is saved either way. True when it was turned on.
    /// </summary>
    public async Task<bool> AcceptBiometricOfferAsync()
    {
        AnswerBiometricOffer();
        return await SetSignInMethodAsync(SignInMethod.Biometric);
    }

    /// <summary>
    /// "Not now": Stay signed in remains, and the question isn't asked again.
    /// </summary>
    public void DeclineBiometricOffer() => AnswerBiometricOffer();

    private void AnswerBiometricOffer() => _settings.Save(_settings.Load() with { BiometricOfferAnswered = true });

    partial void OnStaySignedInForChanged(SignInDuration value)
    {
        if (!_loaded || !IsSignInCardVisible)
            return;

        var saved = _settings.Load();
        if (saved.StaySignedInFor != value)
            _settings.Save(saved with { StaySignedInFor = value });
    }

    #endregion

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
