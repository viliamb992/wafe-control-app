using Microsoft.Maui.Controls.Shapes;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Controls;

/// <summary>
/// Fluent segmented control (DESIGN.md, section 7): a track with one raised, selected segment marked by an accent pill.
/// <see cref="SegmentTapped"/> is raised only for the user's taps, not when <see cref="SelectedIndex"/> is set in code.
/// </summary>
public sealed class SegmentedControl : ContentView
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IReadOnlyList<string>), typeof(SegmentedControl), Array.Empty<string>(),
        propertyChanged: (bindable, _, _) => ((SegmentedControl)bindable).Rebuild());

    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(SegmentedControl), -1, BindingMode.TwoWay,
        propertyChanged: (bindable, _, _) => ((SegmentedControl)bindable).UpdateSelection());

    private readonly Grid _segments = new() { ColumnSpacing = 2 };
    private readonly List<Segment> _items = [];

    public SegmentedControl()
    {
        var track = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 2,
            Content = _segments,
        };
        track.SetColor(BackgroundColorProperty, "ControlAltFill");
        Content = track;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsEnabled))
                Opacity = IsEnabled ? 1 : 0.4;
        };
    }

    public IReadOnlyList<string> Items
    {
        get => (IReadOnlyList<string>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public event EventHandler<int>? SegmentTapped;

    private void Rebuild()
    {
        _segments.Children.Clear();
        _segments.ColumnDefinitions.Clear();
        _items.Clear();

        for (var i = 0; i < Items.Count; i++)
        {
            var segment = new Segment(Items[i]);
            var index = i;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnTapped(index);
            segment.View.GestureRecognizers.Add(tap);

            _segments.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _segments.Add(segment.View, i);
            _items.Add(segment);
        }

        UpdateSelection();
    }

    private void OnTapped(int index)
    {
        if (!IsEnabled || index == SelectedIndex)
            return;

        SelectedIndex = index;
        SegmentTapped?.Invoke(this, index);
    }

    private void UpdateSelection()
    {
        for (var i = 0; i < _items.Count; i++)
            _items[i].SetSelected(i == SelectedIndex);
    }

    private sealed class Segment
    {
        private readonly Border _background;
        private readonly Label _label;
        private readonly BoxView _indicator;

        public Segment(string text)
        {
            _label = new Label
            {
                Text = text,
                Style = Theme.Style("BodyStrong"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                Margin = new Thickness(6, 0),
            };
            _indicator = new BoxView
            {
                WidthRequest = 16,
                HeightRequest = 3,
                CornerRadius = 1.5,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 3),
            }.SetColor(BoxView.ColorProperty, "Accent");

            _background = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                StrokeThickness = 1,
                HeightRequest = 36,
                Content = new Grid { Children = { _label, _indicator } },
            };
            SemanticProperties.SetDescription(_background, text);
        }

        public View View => _background;

        public void SetSelected(bool selected)
        {
            _indicator.IsVisible = selected;
            if (selected)
            {
                _background.SetColor(VisualElement.BackgroundColorProperty, "ControlFill");
                _background.SetColor(Border.StrokeProperty, "ControlStroke");
                _label.SetColor(Label.TextColorProperty, "TextPrimary");
            }
            else
            {
                _background.SetFixed(VisualElement.BackgroundColorProperty, Colors.Transparent);
                _background.SetFixed(Border.StrokeProperty, Brush.Transparent);
                _label.SetColor(Label.TextColorProperty, "TextSecondary");
            }
        }
    }
}
