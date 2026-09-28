using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
#if ANDROID
using Microsoft.Maui.Platform;
#endif
using Serilog;
using WafeControl.Core;
using WafeControl.Core.Diagnostics;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Mobile.Helpers;
using WafeControl.Mobile.Services;
using WafeControl.Mobile.Views;

namespace WafeControl.Mobile;

public static class MauiProgram
{
    /// <summary>
    /// The app's log files (kept for a week), shared from Settings → About.
    /// </summary>
    public static string LogDirectory { get; } = Path.Combine(FileSystem.AppDataDirectory, "logs");

    public static MauiApp CreateMauiApp()
    {
        // Crashes from here on leave a log entry and a marker for the next start.
        CrashHandler.Initialize(LogDirectory);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                CrashHandler.OnFatal(exception, "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += CrashHandler.OnUnobservedTask;

        DeviceDescription.Current = new DeviceDescription(
            DeviceInfo.Platform.ToString().ToLowerInvariant(),
            $"{DeviceInfo.Platform} {DeviceInfo.VersionString}",
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            $"{DeviceInfo.Manufacturer} {DeviceInfo.Model}",
            "sideload");
        Log.Logger = AppLogging.Configure(LogDirectory).CreateLogger();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("FluentIcons.ttf", FluentIcons.FontFamily))
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Picker, DropDownPickerHandler>();
#endif
            });

        builder.Logging.AddSerilog(dispose: true);
#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Logging.AddFilter("Polly", LogLevel.Warning);

        UseCrashReports(builder);

        builder.Services.AddWafeControlCore(polling =>
        {
            polling.StatusRefreshIntervalMs = 5000;
            polling.StateChangeIntervalMs = 3000;
            polling.StateChangeInitialIntervalsMs = [1000, 1000, 2000, 2000];
            polling.StateChangeTimeoutSeconds = 30;
        });
        builder.Services.AddSingleton<ICredentialStore, SecureStorageCredentialStore>();
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddSingleton<IStartupRegistration, NoStartupRegistration>();
        builder.Services.AddSingleton(Connectivity.Current);
        builder.Services.AddSingleton<INetworkStatus, MauiNetworkStatus>();
        builder.Services.AddSingleton<ICrashReports>(new MobileCrashReports(CrashReporting.ReadDsn(typeof(MauiProgram).Assembly)));

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<SettingsPage>();

        RemoveInputUnderlines();
        TintRadioButtons();

        var app = builder.Build();

        // The language must be in place before any page produces text.
        var localization = app.Services.GetRequiredService<LocalizationService>();
        localization.Initialize();
        AppLogging.LogEnvironment(localization.Current.Code);
        Routing.RegisterRoute(AppShell.LoginSettingsRoute, typeof(SettingsPage));

        return app;
    }

    /// <summary>
    /// Crash reports (Sentry SDK to GlitchTip) only in release builds with a reporting address, and only when the
    /// user said yes; the SDK then also catches Java and native crashes and ANRs.
    /// </summary>
    private static void UseCrashReports(MauiAppBuilder builder)
    {
        var dsn = CrashReporting.ReadDsn(typeof(MauiProgram).Assembly);
        var consent = new PreferencesSettingsStore().Load().CrashReports == true;

        // Until the settings screen's view model takes over (App).
        CrashReporting.IsAllowed = () => consent;
        if (dsn is null || !consent)
            return;

        builder.UseSentry(options =>
        {
            CrashReporting.Configure(options, dsn, DeviceInfo.Platform.ToString().ToLowerInvariant(),
                Path.Combine(FileSystem.CacheDirectory, "reports"));
            options.MinimumEventLevel = LogLevel.Error;
            options.MinimumBreadcrumbLevel = LogLevel.Information;
        });
    }

    /// <summary>
    /// Inputs sit in an InputBox border (DESIGN.md, section 7), so drop the platform underline and padding.
    /// </summary>
    private static void RemoveInputUnderlines()
    {
#if ANDROID
        static void Clear(Android.Widget.EditText view)
        {
            view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            view.SetPadding(0, view.PaddingTop, 0, view.PaddingBottom);
        }

        EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => Clear(handler.PlatformView));
        PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => Clear(handler.PlatformView));
        TimePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) => Clear(handler.PlatformView));
#elif IOS
        EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, _) => handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None);
        PickerHandler.Mapper.AppendToMapping("NoBorder", (handler, _) => handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None);
        TimePickerHandler.Mapper.AppendToMapping("NoBorder", (handler, _) => handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None);
#endif
    }

    /// <summary>
    /// Android radio circle: <c>Accent</c> when checked, <c>ControlStrong</c> otherwise. Runs with the text color,
    /// which is an AppThemeBinding, so it follows theme changes.
    /// </summary>
    private static void TintRadioButtons()
    {
#if ANDROID
        RadioButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.TextColor), (handler, _) =>
        {
            if (handler.PlatformView is not Android.Widget.CompoundButton button)
                return;

            button.ButtonTintList = new Android.Content.Res.ColorStateList(
                [[Android.Resource.Attribute.StateChecked], []],
                [Theme.Current("Accent").ToPlatform().ToArgb(), Theme.Current("ControlStrong").ToPlatform().ToArgb()]);
        });
#endif
    }
}
