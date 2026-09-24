using Android.App;
using AndroidX.Core.View;

namespace WafeControl.Mobile;

/// <summary>
/// Dark status and navigation bar icons on the light theme, light ones on the dark theme. The bars show the page
/// background (edge-to-edge), so they follow the app's theme rather than the system's.
/// </summary>
public static class SystemBars
{
    public static void Apply(Activity? activity = null)
    {
        activity ??= Platform.CurrentActivity;
        if (activity?.Window is not { } window)
            return;

        var light = Microsoft.Maui.Controls.Application.Current?.RequestedTheme != AppTheme.Dark;
        if (WindowCompat.GetInsetsController(window, window.DecorView) is { } controller)
        {
            controller.AppearanceLightStatusBars = light;
            controller.AppearanceLightNavigationBars = light;
        }
    }
}
