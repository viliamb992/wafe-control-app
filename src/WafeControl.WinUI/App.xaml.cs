using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WafeControl.Core;
using WafeControl.Core.Demo;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.WinUI.Services;
using Serilog;
using Serilog.Events;

namespace WafeControl.WinUI;

public partial class App : Application
{
    /// <summary>
    /// Starts the app in the tray for this launch only (e.g. after an update applied while it was in the tray).
    /// </summary>
    public const string TrayArgument = "--tray";

    private ServiceProvider? _services;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Folder for log files: %LocalAppData%\WafeControl\logs.
    /// </summary>
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WafeControl", "logs");

    /// <summary>
    /// Crash reports waiting to be sent (e.g. a crash while offline): %LocalAppData%\WafeControl\reports.
    /// </summary>
    private static string ReportCacheDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WafeControl", "reports");

    // A failure while starting is fatal: log it, leave the crash marker and quit instead of hanging in the tray.
    protected override void OnLaunched(LaunchActivatedEventArgs args) => _ = LaunchGuardedAsync();

    private async Task LaunchGuardedAsync()
    {
        try
        {
            await LaunchAsync();
        }
        catch (Exception ex)
        {
            CrashHandler.OnFatal(ex, "Launch");
            Exit();
        }
    }

    private async Task LaunchAsync()
    {
        DeviceDescription.Current = new DeviceDescription(
            "windows",
            $"Windows {Environment.OSVersion.Version}",
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            null,
            "dev");
        Log.Logger = CreateLogger();
        LegacyInstallMigration.MoveUserData();

        _services = ConfigureServices();

        // The language must be in place before any view or view model produces text.
        var localization = _services.GetRequiredService<LocalizationService>();
        localization.Initialize();
        ApplyControlLanguage(localization.Current);
        localization.LanguageChanged += (_, _) => ApplyControlLanguage(localization.Current);

        var updates = _services.GetRequiredService<VelopackUpdateService>();
        DeviceDescription.Current = DeviceDescription.Current with { InstallType = updates.InstallType };
        AppLogging.LogEnvironment(localization.Current.Code);

        var viewModel = _services.GetRequiredService<AppViewModel>();
        var settings = _services.GetRequiredService<SettingsViewModel>();

        // Crash reports only with the user's consent, checked for every report.
        CrashReporting.IsAllowed = () => settings.CrashReportsEnabled;
        if (settings.CrashReportsEnabled)
            _services.GetRequiredService<ICrashReports>().Apply(true);

        // An exception in a click handler is reported on screen instead of ending the app.
        SafeAsync.UnhandledError = _ => viewModel.Feedback = Feedback.Error(Strings.ErrorUnexpected, offersReport: true);

        var lastCrash = CrashHandler.TryTakeLastCrash();

        _window = new MainWindow(
            viewModel,
            _services.GetRequiredService<ScheduleViewModel>(),
            settings,
            _services.GetRequiredService<UpdateViewModel>(),
            _services.GetRequiredService<DemoWafeApi>(),
            localization,
            _services.GetRequiredService<ISettingsStore>());
        _window.Closed += OnMainWindowClosed;

        var startInTray = settings.StartInTray || Environment.GetCommandLineArgs().Contains(TrayArgument);
        if (startInTray)
            _window.StartInTray();
        else
            _window.ShowAtLaunch();

        _window.ShowStartupNotices(lastCrash);
        _window.ShowUpdatedNotice();

        await viewModel.StartAsync();

        // Nobody would notice a sign-in form hidden in the tray.
        if (startInTray && viewModel.IsLoginRequired)
            _window.ShowFromTray();

        _services.GetRequiredService<UpdateViewModel>().Start();
        FindNativeCrashesAsync(lastCrash).Forget(_services.GetRequiredService<ILogger<App>>(), "Reading the event log");
    }

    /// <summary>
    /// Crashes .NET never saw, from the Windows event log; read in the background, reported once.
    /// </summary>
    private async Task FindNativeCrashesAsync(CrashRecord? handledCrash)
    {
        var settings = _services!.GetRequiredService<ISettingsStore>();
        var crashes = await Task.Run(() => NativeCrashDetector.FindNew(settings, handledCrash));
        if (crashes.Count == 0)
            return;

        foreach (var crash in crashes)
        {
            Log.Error("Previous session crashed: {Type} ({Source}, version {Version}) at {Time}: {Message}",
                crash.ExceptionType, crash.Source, crash.Version, crash.Time, crash.Message);
            var reportId = CrashReporting.CaptureEarlierCrash(crash, new Dictionary<string, string>
            {
                ["message"] = crash.Message,
                ["stack"] = crash.StackTrace,
            });
            CrashHandler.Remember(crash with { ReportId = reportId });
        }

        // Only when the .NET marker didn't already bring the dialog up.
        if (handledCrash is null)
            _window?.ShowStartupNotices(CrashHandler.LastCrash);
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
        services.AddWafeControlCore(polling =>
        {
            polling.StatusRefreshIntervalMs = 5000;
            polling.StateChangeIntervalMs = 3000;
            polling.StateChangeInitialIntervalsMs = [1000, 1000, 2000, 2000];
            polling.StateChangeTimeoutSeconds = 30;
        });
        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IStartupRegistration, RegistryStartupRegistration>();
        services.AddSingleton<ICrashReports>(new WindowsCrashReports(
            CrashReporting.ReadDsn(typeof(App).Assembly), ReportCacheDirectory));
        services.AddSingleton<VelopackUpdateService>();
        services.AddSingleton<IUpdateService>(sp => sp.GetRequiredService<VelopackUpdateService>());

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

    // Errors become crash reports while the SDK is on (see WindowsCrashReports); the sink does nothing otherwise.
    private static Serilog.ILogger CreateLogger() =>
        AppLogging.Configure(LogDirectory)
            .WriteTo.Sentry(options =>
            {
                options.InitializeSdk = false;
                options.MinimumEventLevel = LogEventLevel.Error;
                options.MinimumBreadcrumbLevel = LogEventLevel.Information;
            })
            .CreateLogger();

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        Log.Information("Application exiting");
        _services?.Dispose();
        Log.CloseAndFlush();
        Exit();
    }

    // Anything reaching here left the UI in an unknown state: record it and let the app end.
    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => CrashHandler.OnFatal(e.Exception, "XAML");
}
