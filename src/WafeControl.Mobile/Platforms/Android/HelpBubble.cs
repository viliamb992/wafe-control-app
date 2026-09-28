using Android.Graphics.Drawables;
using Android.Util;
using Android.Widget;
using Microsoft.Maui.Platform;
using WafeControl.Mobile.Helpers;
using AView = Android.Views.View;
using ViewGroup = Android.Views.ViewGroup;

namespace WafeControl.Mobile;

/// <summary>
/// A small explanation bubble under a (?) icon, or above it when there's no room below. Android shows its native
/// tooltip only on a long press, so a tap opens this instead. Any tap closes it.
/// </summary>
internal static class HelpBubble
{
    private static PopupWindow? _current;

    public static void Show(View anchor, string text)
    {
        Dismiss();
        if (anchor.Handler?.PlatformView is not AView view || view.Context is not { } context)
            return;

        var density = context.Resources?.DisplayMetrics?.Density ?? 1;
        int Dp(double value) => (int)Math.Round(value * density);

        // Card colors (DESIGN.md, section 2): surface, primary text, stroke; 12 dp corners.
        var background = new GradientDrawable();
        background.SetColor(Theme.Current("CardBackground").ToPlatform());
        background.SetStroke(Dp(1), Theme.Current("ControlStroke").ToPlatform());
        background.SetCornerRadius(Dp(12));

        var label = new TextView(context) { Text = text, Background = background };
        label.SetTextColor(Theme.Current("TextPrimary").ToPlatform());
        label.SetTextSize(ComplexUnitType.Sp, 14);
        label.SetLineSpacing(Dp(2), 1);
        label.SetMaxWidth(Dp(280));
        label.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));

        // Focusable: a tap outside closes it (and doesn't reach the page, so it can't also toggle an option).
        var popup = new PopupWindow(label, ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, true)
        {
            OutsideTouchable = true,
            Elevation = Dp(4),
        };
        popup.SetBackgroundDrawable(new ColorDrawable(Android.Graphics.Color.Transparent));
        label.Click += (_, _) => popup.Dismiss();
        popup.DismissEvent += (_, _) =>
        {
            if (ReferenceEquals(_current, popup))
                _current = null;
        };

        _current = popup;
        popup.ShowAsDropDown(view, 0, Dp(-4));
    }

    public static void Dismiss() => _current?.Dismiss();
}
