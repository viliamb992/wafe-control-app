using System.ComponentModel;
using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels;

namespace WafeControl.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly AppViewModel _app;
    private readonly SettingsViewModel _settings;
    private Window? _window;

    public App(IServiceProvider services, AppViewModel app, SettingsViewModel settings, ILocalizationService localization)
    {
        _services = services;
        _app = app;
        _settings = settings;
        InitializeComponent();

        ApplyTheme();
        _settings.PropertyChanged += OnSettingsPropertyChanged;
#if ANDROID
        RequestedThemeChanged += (_, _) => SystemBars.Apply();
#endif
        localization.LanguageChanged += OnLanguageChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(new AppShell(_services, _app)) { Title = "WAFE Control" };
        _window.Created += async (_, _) => await _app.StartAsync();

        // No polling in the background; fresh data as soon as the app is back.
        _window.Stopped += (_, _) => _app.Pause();
        _window.Resumed += async (_, _) => await _app.ResumeAsync();
        return _window;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.Theme))
            ApplyTheme();
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
    private void OnLanguageChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(async () =>
    {
        if (_window is null)
            return;

        // Written in the previous language.
        _app.Login.ErrorMessage = null;

        var shell = new AppShell(_services, _app);
        _window.Page = shell;
        await shell.GoToSettingsAsync();
    });
}
