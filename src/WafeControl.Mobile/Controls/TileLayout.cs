using Microsoft.Maui.Layouts;

namespace WafeControl.Mobile.Controls;

/// <summary>
/// Equal-width columns, as many as fit <see cref="MinItemWidth"/> (at most <see cref="MaxColumns"/>); each row is as
/// tall as its tallest item. Hidden children take no cell. Like the Windows app's TilePanel.
/// </summary>
public sealed class TileLayout : Layout
{
    public static readonly BindableProperty MinItemWidthProperty = BindableProperty.Create(
        nameof(MinItemWidth), typeof(double), typeof(TileLayout), 150.0,
        propertyChanged: (bindable, _, _) => ((TileLayout)bindable).InvalidateMeasure());

    public static readonly BindableProperty MaxColumnsProperty = BindableProperty.Create(
        nameof(MaxColumns), typeof(int), typeof(TileLayout), 3,
        propertyChanged: (bindable, _, _) => ((TileLayout)bindable).InvalidateMeasure());

    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(
        nameof(Spacing), typeof(double), typeof(TileLayout), 12.0,
        propertyChanged: (bindable, _, _) => ((TileLayout)bindable).InvalidateMeasure());

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override ILayoutManager CreateLayoutManager() => new Manager(this);

    private sealed class Manager(TileLayout layout) : LayoutManager(layout)
    {
        private readonly List<double> _rowHeights = [];

        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = layout.Padding;
            var width = widthConstraint - padding.HorizontalThickness;
            var (columns, itemWidth) = Columns(width);
            var visible = Visible();

            _rowHeights.Clear();
            for (var i = 0; i < visible.Count; i++)
            {
                var size = visible[i].Measure(itemWidth, double.PositiveInfinity);
                if (i % columns == 0)
                    _rowHeights.Add(0);
                _rowHeights[^1] = Math.Max(_rowHeights[^1], size.Height);
            }

            var height = _rowHeights.Sum() + Math.Max(0, _rowHeights.Count - 1) * layout.Spacing + padding.VerticalThickness;
            return new Size(widthConstraint, height);
        }

        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = layout.Padding;
            var (columns, itemWidth) = Columns(bounds.Width - padding.HorizontalThickness);
            var visible = Visible();

            var y = bounds.Top + padding.Top;
            for (var i = 0; i < visible.Count; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var rowHeight = row < _rowHeights.Count ? _rowHeights[row] : visible[i].DesiredSize.Height;
                if (column == 0 && row > 0)
                    y += _rowHeights[row - 1] + layout.Spacing;

                var x = bounds.Left + padding.Left + column * (itemWidth + layout.Spacing);
                visible[i].Arrange(new Rect(x, y, itemWidth, rowHeight));
            }

            return bounds.Size;
        }

        private (int Columns, double ItemWidth) Columns(double width)
        {
            var spacing = layout.Spacing;
            var columns = double.IsFinite(width)
                ? Math.Clamp((int)((width + spacing) / (layout.MinItemWidth + spacing)), 1, Math.Max(1, layout.MaxColumns))
                : 1;
            var itemWidth = double.IsFinite(width) ? (width - (columns - 1) * spacing) / columns : layout.MinItemWidth;
            return (columns, Math.Max(0, itemWidth));
        }

        private List<IView> Visible() => layout.Where(child => child.Visibility != Visibility.Collapsed).ToList();
    }
}
