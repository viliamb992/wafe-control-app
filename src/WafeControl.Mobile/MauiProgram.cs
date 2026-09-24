using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
#if ANDROID
using Microsoft.Maui.Platform;
#endif
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
            .ConfigureFonts(fonts => fonts.AddFont("FluentIcons.ttf", FluentIcons.FontFamily))
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                handlers.AddHandler<Picker, DropDownPickerHandler>();
#endif
            });

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
        TintRadioButtons();

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
