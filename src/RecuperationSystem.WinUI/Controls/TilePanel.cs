using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace RecuperationSystem.WinUI.Controls;

/// <summary>
/// Lays out tiles in equal-width columns that fill the available width, wrapping into balanced rows
/// (6 tiles that don't fit on one row become 3 + 3). Collapsed tiles take no space.
/// All tiles share the height of the tallest one.
/// </summary>
public sealed partial class TilePanel : Panel
{
    private double _minItemWidth = 160;
    private double _spacing = 12;

    public double MinItemWidth
    {
        get => _minItemWidth;
        set { _minItemWidth = value; InvalidateMeasure(); }
    }

    public double Spacing
    {
        get => _spacing;
        set { _spacing = value; InvalidateMeasure(); }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var tiles = VisibleTiles();
        if (tiles.Count == 0)
            return new Size(0, 0);

        var width = double.IsInfinity(availableSize.Width)
            ? tiles.Count * (MinItemWidth + Spacing) - Spacing
            : availableSize.Width;
        var (columns, rows, itemWidth) = Layout(width, tiles.Count);

        var itemHeight = 0.0;
        foreach (var tile in tiles)
        {
            tile.Measure(new Size(itemWidth, double.PositiveInfinity));
            itemHeight = Math.Max(itemHeight, tile.DesiredSize.Height);
        }

        return new Size(width, rows * itemHeight + (rows - 1) * Spacing);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var tiles = VisibleTiles();
        if (tiles.Count == 0)
            return finalSize;

        var (columns, _, itemWidth) = Layout(finalSize.Width, tiles.Count);
        var itemHeight = tiles.Max(t => t.DesiredSize.Height);

        for (var i = 0; i < tiles.Count; i++)
        {
            var (row, column) = Math.DivRem(i, columns);
            tiles[i].Arrange(new Rect(column * (itemWidth + Spacing), row * (itemHeight + Spacing), itemWidth, itemHeight));
        }

        return finalSize;
    }

    private (int Columns, int Rows, double ItemWidth) Layout(double width, int count)
    {
        var maxColumns = Math.Max(1, (int)((width + Spacing) / (MinItemWidth + Spacing)));
        var rows = (count + maxColumns - 1) / maxColumns;
        var columns = (count + rows - 1) / rows;
        var itemWidth = Math.Max(0, (width - (columns - 1) * Spacing) / columns);
        return (columns, rows, itemWidth);
    }

    private List<UIElement> VisibleTiles() => Children.Where(c => c.Visibility == Visibility.Visible).ToList();
}
