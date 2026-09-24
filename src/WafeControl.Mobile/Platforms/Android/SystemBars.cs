using Android.App;
using Android.Graphics.Drawables;
using AndroidX.Core.View;
using Microsoft.Maui.Platform;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile;

/// <summary>
/// Dark status and navigation bar icons on the light theme, light ones on the dark theme. The bars show the page
/// background (edge-to-edge), so they follow the app's theme rather than the system's.
/// The window behind the pages gets the page background too; it shows while the pages are rebuilt (language change).
/// </summary>
public static class SystemBars
{
    public static void Apply(Activity? activity = null)
    {
        activity ??= Platform.CurrentActivity;
        if (activity?.Window is not { } window)
            return;

        window.SetBackgroundDrawable(new ColorDrawable(Theme.Current("PageBackground").ToPlatform()));

        var light = Microsoft.Maui.Controls.Application.Current?.RequestedTheme != AppTheme.Dark;
        if (WindowCompat.GetInsetsController(window, window.DecorView) is { } controller)
        {
            controller.AppearanceLightStatusBars = light;
            controller.AppearanceLightNavigationBars = light;
        }
    }
}
