using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using RecuperationSystem.Core;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Core.ViewModels;
using RecuperationSystem.Core.ViewModels.Schedule;
using RecuperationSystem.WinUI.Services;
using Serilog;
using Serilog.Events;

namespace RecuperationSystem.WinUI;

public partial class App : Application
{
    private ServiceProvider? _services;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Folder for log files: %LocalAppData%\RecuperationSystem\logs.
    /// </summary>
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecuperationSystem", "logs");

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Logger = CreateLogger();
        Log.Information("Starting Wafe Recuperation (WinUI) {Version}", AppVersion.Current);

        _services = ConfigureServices();

        // The language must be in place before any view or view model produces text.
        var localization = _services.GetRequiredService<LocalizationService>();
        localization.Initialize();
        ApplyControlLanguage(localization.Current);
        localization.LanguageChanged += (_, _) => ApplyControlLanguage(localization.Current);

        var viewModel = _services.GetRequiredService<AppViewModel>();
        var settings = _services.GetRequiredService<SettingsViewModel>();

        _window = new MainWindow(
            viewModel,
            _services.GetRequiredService<ScheduleViewModel>(),
            settings,
            localization,
            _services.GetRequiredService<ISettingsStore>());
        _window.Closed += OnMainWindowClosed;

        var startInTray = settings.StartInTray;
        if (startInTray)
            _window.StartInTray();
        else
            _window.ShowAtLaunch();

        await viewModel.StartAsync();

        // Nobody would notice a sign-in form hidden in the tray.
        if (startInTray && viewModel.IsLoginRequired)
            _window.ShowFromTray();
    }

    /// <summary>
    /// Brings the main window back, e.g. when the user launches the app again while it sits in the tray.
    /// Safe to call from any thread.
    /// </summary>
    public void ActivateMainWindow() => _window?.DispatcherQueue.TryEnqueue(() => _window.ShowFromTray());

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog(dispose: false));
        services.AddRecuperationCore(polling =>
        {
            polling.StatusRefreshIntervalMs = 5000;
            polling.StateChangeIntervalMs = 3000;
            polling.StateChangeTimeoutSeconds = 30;
        });
        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IStartupRegistration, RegistryStartupRegistration>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Text built into WinUI controls (time picker, title bar back button, ...) follows this language.
    /// Controls that already exist keep theirs until the next launch.
    /// </summary>
    private static void ApplyControlLanguage(AppLanguage language)
    {
        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = language.CultureName;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set the WinUI control language to {Language}", language.CultureName);
        }
    }

    private static Serilog.ILogger CreateLogger() =>
        new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(LogDirectory, "wafe-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        Log.Information("Application exiting");
        _services?.Dispose();
        Log.CloseAndFlush();
        Exit();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => Log.Fatal(e.Exception, "Unhandled exception");
}
