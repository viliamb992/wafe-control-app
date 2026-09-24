using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using WafeControl.Mobile.Helpers;
using AView = Android.Views.View;

namespace WafeControl.Mobile;

/// <summary>
/// Picker that opens a drop-down list under its input box instead of MAUI's dialog (DESIGN.md, section 7).
/// </summary>
public sealed class DropDownPickerHandler : PickerHandler
{
    private ListPopupWindow? _dropDown;

    protected override void ConnectHandler(MauiPicker platformView)
    {
        base.ConnectHandler(platformView);

        // Replaces the listener behind PickerHandler's Click handler, which opens the dialog. Its focus handler
        // opens the picker with a click, so it lands here too.
        platformView.SetOnClickListener(new ClickListener(ShowDropDown));
    }

    protected override void DisconnectHandler(MauiPicker platformView)
    {
        platformView.SetOnClickListener(null);
        _dropDown?.Dismiss();
        _dropDown = null;
        base.DisconnectHandler(platformView);
    }

    private void ShowDropDown()
    {
        if (_dropDown is not null || VirtualView is not { } picker || !picker.IsEnabled || picker.GetCount() == 0)
            return;

        var context = PlatformView.Context!;
        var items = Enumerable.Range(0, picker.GetCount()).Select(picker.GetItem).ToList();

        // As wide as the InputBox around the picker.
        var anchor = picker is Element { Parent: Border { Handler.PlatformView: AView box } } ? box : PlatformView;

        var background = new GradientDrawable();
        background.SetColor(Theme.Current("CardBackground").ToPlatform());
        background.SetStroke(Dp(context, 1), Theme.Current("CardStroke").ToPlatform());
        background.SetCornerRadius(Dp(context, 8));

        var dropDown = new ListPopupWindow(context)
        {
            AnchorView = anchor,
            Width = anchor.Width,
            VerticalOffset = Dp(context, 4),
            Modal = true,
        };
        dropDown.SetBackgroundDrawable(background);
        dropDown.SetAdapter(new ItemAdapter(context, items, picker.SelectedIndex));
        dropDown.ItemClick += (_, e) =>
        {
            dropDown.Dismiss();
            if (VirtualView is { } current)
                current.SelectedIndex = e.Position;
        };
        dropDown.DismissEvent += (_, _) => _dropDown = null;

        _dropDown = dropDown;
        dropDown.Show();
        if (picker.SelectedIndex >= 0)
            dropDown.SetSelection(picker.SelectedIndex);
    }

    private static int Dp(Context context, float value) =>
        (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, value, context.Resources!.DisplayMetrics);

    private sealed class ClickListener(Action onClick) : Java.Lang.Object, AView.IOnClickListener
    {
        public void OnClick(AView? v) => onClick();
    }

    /// <summary>
    /// Rows in <c>Body</c>, the selected one in <c>AccentText</c> semibold.
    /// </summary>
    private sealed class ItemAdapter(Context context, IList<string> items, int selectedIndex)
        : ArrayAdapter<string>(context, Android.Resource.Layout.SimpleListItem1, items)
    {
        public override AView GetView(int position, AView? convertView, ViewGroup parent)
        {
            var view = (TextView)base.GetView(position, convertView, parent);
            var selected = position == selectedIndex;
            view.SetTextColor(Theme.Current(selected ? "AccentText" : "TextPrimary").ToPlatform());
            view.SetTextSize(ComplexUnitType.Sp, 15);
            view.Typeface = selected ? Typeface.Create("sans-serif-medium", TypefaceStyle.Normal) : Typeface.Default;
            return view;
        }
    }
}
