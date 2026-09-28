using System.ComponentModel;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly AppViewModel _app;
    private readonly SettingsViewModel _settings;
    private readonly ReleaseCheckViewModel _releases;
    private readonly ILocalizationService _localization;
    private Window? _window;
    private bool _promptsShowing;

    public App(IServiceProvider services, AppViewModel app, SettingsViewModel settings, ReleaseCheckViewModel releases,
        ILocalizationService localization)
    {
        _services = services;
        _app = app;
        _settings = settings;
        _releases = releases;
        _localization = localization;
        InitializeComponent();

        // Crash reports only with the user's consent, checked for every report.
        CrashReporting.IsAllowed = () => _settings.CrashReportsEnabled;

        // An exception in a tap handler is reported on screen instead of ending the app.
        SafeAsync.UnhandledError = _ => _app.Feedback = Feedback.Error(Strings.ErrorUnexpected, offersReport: true);

        ApplyTheme();
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        _app.PropertyChanged += OnAppPropertyChanged;
#if ANDROID
        RequestedThemeChanged += (_, _) => SystemBars.Apply();
#endif
        localization.LanguageChanged += OnLanguageChanged;

#if ANDROID
        // Sideloaded: nothing else tells about a new version.
        _releases.Enable(ReleaseCheckViewModel.AndroidTagPrefix);
        _releases.OpenRequested += (_, uri) => SafeAsync.Run(() => Launcher.Default.TryOpenAsync(uri));
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(new AppShell(_services, _app)) { Title = "WAFE Control" };
        var lastCrash = CrashHandler.TryTakeLastCrash();
        _window.Created += (_, _) => SafeAsync.Run(async () =>
        {
            await _app.StartAsync();
            if (lastCrash is not null)
                await ShowCrashNoticeAsync(lastCrash);
            await CheckForNewVersionAsync();
        });

        // No polling in the background; fresh data as soon as the app is back.
        _window.Stopped += (_, _) => _app.Pause();
        _window.Resumed += (_, _) => SafeAsync.Run(async () =>
        {
            await _app.ResumeAsync();
            await CheckForNewVersionAsync();
        });
        return _window;
    }

    /// <summary>
    /// At most every few hours. Debug builds check only from Settings (their version is older than every release).
    /// </summary>
    private Task CheckForNewVersionAsync()
    {
#if DEBUG
        return Task.CompletedTask;
#else
        return _releases.CheckIfDueAsync();
#endif
    }

    /// <summary>
    /// The page on screen, for alerts: the shell's current page or a sheet on top of it.
    /// </summary>
    private Page? CurrentPage =>
        _window?.Page is Shell shell ? shell.Navigation.ModalStack.LastOrDefault() ?? shell.CurrentPage : _window?.Page;

    /// <summary>
    /// "Closed unexpectedly last time", with the way to report it.
    /// </summary>
    private async Task ShowCrashNoticeAsync(CrashRecord crash)
    {
        if (CurrentPage is not { } page)
            return;

        var message = crash.ReportId is null ? Strings.CrashDialogMessage : $"{Strings.CrashDialogMessage}\n\n{Strings.CrashDialogReportSent}";
        if (await page.DisplayAlertAsync(Strings.CrashDialogTitle, message, Strings.ReportProblem, Strings.ButtonClose))
            await ProblemReporting.ReportAsync(page, _app, _localization);
    }

    // After a real sign-in: the fingerprint or face offer, then the crash-report question, never both on screen.
    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppViewModel.IsAuthenticated) || !_app.IsAuthenticated || _app.IsDemo || _promptsShowing
            || !(_settings.NeedsBiometricOffer || _settings.NeedsCrashReportConsent))
            return;

        _promptsShowing = true;
        Dispatcher.Dispatch(() => SafeAsync.Run(async () =>
        {
            try
            {
                await OfferBiometricAsync();
                await AskCrashReportConsentAsync();
            }
            finally
            {
                _promptsShowing = false;
            }
        }));
    }

    private async Task OfferBiometricAsync()
    {
        if (!_settings.NeedsBiometricOffer || CurrentPage is not { } page)
            return;

        if (!await page.DisplayAlertAsync(Strings.BiometricOfferTitle, Strings.BiometricOfferMessage,
                Strings.BiometricOfferAccept, Strings.BiometricOfferDecline))
        {
            _settings.DeclineBiometricOffer();
            return;
        }

        if (await _settings.AcceptBiometricOfferAsync())
            _app.Feedback = Feedback.Success(Strings.BiometricTurnedOn);
    }

    private async Task AskCrashReportConsentAsync()
    {
        if (!_settings.NeedsCrashReportConsent || CurrentPage is not { } page)
            return;

        var send = await page.DisplayAlertAsync(Strings.CrashConsentTitle,
            $"{Strings.CrashConsentMessage}\n\n{Strings.SettingsCrashReportsRestart}",
            Strings.CrashConsentSend, Strings.CrashConsentDontSend);
        _settings.AnswerCrashReportConsent(send);
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Theme))
            ApplyTheme();
        else if (e.PropertyName is nameof(SettingsViewModel.SignInMethod) or nameof(SettingsViewModel.StaySignedInFor))
            _app.SignInSettingsChanged();
        else if (e.PropertyName == nameof(SettingsViewModel.BetaUpdates))
            SafeAsync.Run(_releases.CheckAsync);
    }

    private void ApplyTheme()
    {
        UserAppTheme = _settings.Theme switch
        {
            Core.Services.AppTheme.Light => AppTheme.Light,
            Core.Services.AppTheme.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
#if ANDROID
        // Native widgets (radio buttons, drop-downs, the time dialog) take their colors from the Android theme.
        AndroidX.AppCompat.App.AppCompatDelegate.DefaultNightMode = UserAppTheme switch
        {
            AppTheme.Light => AndroidX.AppCompat.App.AppCompatDelegate.ModeNightNo,
            AppTheme.Dark => AndroidX.AppCompat.App.AppCompatDelegate.ModeNightYes,
            _ => AndroidX.AppCompat.App.AppCompatDelegate.ModeNightFollowSystem,
        };
#endif
    }

    /// <summary>
    /// Text comes from Strings when a page is built, so rebuild the pages in the new language and return to Settings.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() => SafeAsync.Run(async () =>
    {
        if (_window is null)
            return;

        // Written in the previous language.
        _app.Login.ErrorMessage = null;

        var shell = new AppShell(_services, _app);
        _window.Page = shell;
        await shell.GoToSettingsAsync();
    }));
}
