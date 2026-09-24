using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Controls;

/// <summary>
/// A Windows Settings–style row (DESIGN.md, section 7): 20 px icon, title and wrapping description, the control on the right.
/// </summary>
[ContentProperty(nameof(Accessory))]
public sealed class SettingRow : ContentView
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon), typeof(string), typeof(SettingRow), null,
        propertyChanged: (bindable, _, value) => ((SettingRow)bindable)._icon.Text = (string?)value);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(SettingRow), null,
        propertyChanged: (bindable, _, value) => ((SettingRow)bindable)._title.Text = (string?)value);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(SettingRow), null,
        propertyChanged: (bindable, _, value) =>
        {
            var row = (SettingRow)bindable;
            row._description.Text = (string?)value;
            row._description.IsVisible = !string.IsNullOrEmpty((string?)value);
        });

    public static readonly BindableProperty AccessoryProperty = BindableProperty.Create(
        nameof(Accessory), typeof(View), typeof(SettingRow), null,
        propertyChanged: (bindable, old, value) => ((SettingRow)bindable).SetAccessory((View?)old, (View?)value));

    private readonly Grid _grid;
    private readonly Label _icon;
    private readonly Label _title;
    private readonly Label _description;

    public SettingRow()
    {
        _icon = new Label { Style = Theme.Style("Icon"), WidthRequest = 20 };
        _title = new Label { Style = Theme.Style("Body") };
        _description = new Label { Style = Theme.Style("SecondaryCaption"), IsVisible = false };

        _grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 16,
            MinimumHeightRequest = 44,
        };
        _grid.Add(_icon, 0);
        _grid.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _title, _description } }, 1);
        Content = _grid;
    }

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public View? Accessory
    {
        get => (View?)GetValue(AccessoryProperty);
        set => SetValue(AccessoryProperty, value);
    }

    private void SetAccessory(View? old, View? value)
    {
        if (old is not null)
            _grid.Remove(old);

        if (value is not null)
        {
            value.VerticalOptions = LayoutOptions.Center;
            _grid.Add(value, 2);
        }
    }
}
