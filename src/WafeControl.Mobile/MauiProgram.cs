using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using WafeControl.Core;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Mobile.Helpers;
using WafeControl.Mobile.Services;
using WafeControl.Mobile.Views;

namespace WafeControl.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("FluentIcons.ttf", FluentIcons.FontFamily));

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Logging.AddFilter("Polly", LogLevel.Warning);

        builder.Services.AddWafeControlCore(polling =>
        {
            polling.StatusRefreshIntervalMs = 5000;
            polling.StateChangeIntervalMs = 3000;
            polling.StateChangeTimeoutSeconds = 30;
        });
        builder.Services.AddSingleton<ICredentialStore, SecureStorageCredentialStore>();
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddSingleton<IStartupRegistration, NoStartupRegistration>();
        builder.Services.AddSingleton(Connectivity.Current);

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<SettingsPage>();

        RemoveInputUnderlines();

        var app = builder.Build();

        // The language must be in place before any page produces text.
        app.Services.GetRequiredService<LocalizationService>().Initialize();
        Routing.RegisterRoute(AppShell.LoginSettingsRoute, typeof(SettingsPage));

        return app;
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
}
