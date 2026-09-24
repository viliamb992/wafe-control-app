using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Shared.Models;
using WafeControl.WinUI.Helpers;

namespace WafeControl.WinUI.Controls;

/// <summary>
/// A time range picked in the grid: <paramref name="Day"/> (0 = Monday) and minutes of that day.
/// </summary>
public sealed record ScheduleSlotRange(int Day, int StartMinute, int EndMinute);

/// <summary>
/// Weekly schedule grid: Monday–Sunday columns × 30-minute rows, with entries drawn as colored blocks.
/// Clicking or dragging over empty slots raises <see cref="RangeSelected"/>; clicking a block raises <see cref="EntryInvoked"/>.
/// </summary>
public sealed partial class WeekScheduleGrid : UserControl
{
    private const int SlotMinutes = 30;
    private const int SlotCount = ScheduleEntry.MinutesPerDay / SlotMinutes;
    private const double SlotHeight = 28;
    private const double PixelsPerMinute = SlotHeight / SlotMinutes;
    private const double TimeColumnWidth = 56;
    private const double LabelPadding = 8;

    private readonly Grid[] _days = new Grid[7];
    private readonly TextBlock[] _dayHeaders = new TextBlock[7];
    private readonly List<Button> _blocks = [];
    private readonly Border _selection;
    private readonly ScrollViewer _scroller;
    private IReadOnlyList<ScheduleEntry> _entries = [];
    private bool _hasScrolledToEntries;

    private int? _dragDay;
    private int _anchorSlot;
    private int _currentSlot;

    public WeekScheduleGrid()
    {
        _selection = new Border
        {
            Style = Style("ScheduleSelectionStyle"),
            Opacity = 0.35,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
        };

        _scroller = new ScrollViewer { Content = BuildBody(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(_scroller, 1);

        var root = new Grid { RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = new GridLength(1, GridUnitType.Star) } } };
        root.Children.Add(BuildHeader());
        root.Children.Add(_scroller);
        Content = root;
    }

    public event EventHandler<ScheduleSlotRange>? RangeSelected;

    public event EventHandler<ScheduleEntry>? EntryInvoked;

    /// <summary>
    /// When false, empty slots can't be selected (blocks can still be opened).
    /// </summary>
    public bool CanSelect { get; set; } = true;

    public void SetEntries(IReadOnlyList<ScheduleEntry> entries)
    {
        _entries = entries;
        foreach (var block in _blocks)
            ((Grid)block.Parent).Children.Remove(block);
        _blocks.Clear();

        foreach (var entry in entries)
        {
            foreach (var segment in entry.DaySegments())
            {
                var block = CreateBlock(entry, segment);
                _days[segment.Day].Children.Add(block);
                _blocks.Add(block);
            }
        }

        if (!_hasScrolledToEntries && entries.Count > 0)
        {
            _hasScrolledToEntries = true;
            var firstMinute = entries.SelectMany(e => e.DaySegments()).Min(s => s.StartMinute);
            var offset = Math.Max(0, firstMinute - 60) * PixelsPerMinute;
            DispatcherQueue.TryEnqueue(() => _scroller.ChangeView(null, offset, null, disableAnimation: true));
        }
    }

    /// <summary>
    /// Re-reads day and mode names after the app language changed.
    /// </summary>
    public void RefreshText()
    {
        UpdateDayNames();
        SetEntries(_entries);
    }

    public void ClearSelection()
    {
        (_selection.Parent as Grid)?.Children.Remove(_selection);
        _dragDay = null;
    }

    private Button CreateBlock(ScheduleEntry entry, DaySegment segment)
    {
        var height = Math.Max(6, (segment.EndMinute - segment.StartMinute) * PixelsPerMinute - 2);
        var description = ScheduleFormat.Describe(entry);
        var block = new Button
        {
            Style = (Style)Application.Current.Resources["ScheduleBlockButtonStyle"],
            Background = Xaml.ScheduleModeBrush(entry.Mode),
            Height = height,
            Margin = new Thickness(4, segment.StartMinute * PixelsPerMinute + 1, 4, 0),
            Content = height >= 20
                ? new TextBlock { Text = ScheduleFormat.ModeName(entry.Mode), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }
                : null,
        };
        ToolTipService.SetToolTip(block, description);
        AutomationProperties.SetName(block, description);
        block.Click += (_, _) => EntryInvoked?.Invoke(this, entry);
        return block;
    }

    private Grid BuildHeader()
    {
        var header = CreateColumns();
        header.Padding = new Thickness(0, 0, 0, 8);
        for (var day = 0; day < 7; day++)
        {
            var name = new TextBlock
            {
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(name, day + 1);
            header.Children.Add(name);
            _dayHeaders[day] = name;
        }

        UpdateDayNames();
        return header;
    }

    private void UpdateDayNames()
    {
        var shortNames = ScheduleFormat.ShortDayNames;
        var names = ScheduleFormat.DayNames;
        for (var day = 0; day < 7; day++)
        {
            _dayHeaders[day].Text = shortNames[day];
            AutomationProperties.SetName(_dayHeaders[day], names[day]);
        }
    }

    private Grid BuildBody()
    {
        var body = CreateColumns();
        body.Height = SlotCount * SlotHeight + 1;
        body.Margin = new Thickness(0, LabelPadding, 0, LabelPadding);

        // Time labels and horizontal lines; full hours stronger than half hours.
        for (var slot = 0; slot <= SlotCount; slot++)
        {
            var isHour = slot % 2 == 0;
            var top = slot * SlotHeight;

            var line = new Border
            {
                Height = 1,
                Style = Style(isHour ? "ScheduleHourLineStyle" : "ScheduleDividerStyle"),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, top, 0, 0),
                IsHitTestVisible = false,
            };
            Grid.SetColumn(line, 1);
            Grid.SetColumnSpan(line, 7);
            body.Children.Add(line);

            if (slot == SlotCount)
                break;

            var label = new TextBlock
            {
                Text = $"{slot / 2:D2}:{slot % 2 * SlotMinutes:D2}",
                Style = Style(isHour ? "ScheduleHourLabelStyle" : "ScheduleHalfHourLabelStyle"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, top - LabelPadding, 10, 0),
                IsHitTestVisible = false,
            };
            body.Children.Add(label);
        }

        // Day columns: vertical separators and the interactive surface that hosts the blocks.
        for (var day = 0; day < 7; day++)
        {
            var separator = new Border
            {
                Width = 1,
                Style = Style("ScheduleDividerStyle"),
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = false,
            };
            Grid.SetColumn(separator, day + 1);
            body.Children.Add(separator);

            var column = new Grid { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
            Grid.SetColumn(column, day + 1);
            AttachSelection(column, day);
            body.Children.Add(column);
            _days[day] = column;
        }

        return body;
    }

    private void AttachSelection(Grid column, int day)
    {
        column.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(column);
            if (!CanSelect || !point.Properties.IsLeftButtonPressed)
                return;

            _dragDay = day;
            _anchorSlot = _currentSlot = SlotAt(point.Position.Y);
            column.CapturePointer(e.Pointer);
            ShowSelection(column);
            e.Handled = true;
        };

        column.PointerMoved += (_, e) =>
        {
            if (_dragDay != day)
                return;

            var slot = SlotAt(e.GetCurrentPoint(column).Position.Y);
            if (slot != _currentSlot)
            {
                _currentSlot = slot;
                ShowSelection(column);
            }
        };

        column.PointerReleased += (_, e) =>
        {
            if (_dragDay != day)
                return;

            column.ReleasePointerCapture(e.Pointer);
            _dragDay = null;
            var (from, to) = (Math.Min(_anchorSlot, _currentSlot), Math.Max(_anchorSlot, _currentSlot) + 1);
            RangeSelected?.Invoke(this, new ScheduleSlotRange(day, from * SlotMinutes, to * SlotMinutes));
            e.Handled = true;
        };

        column.PointerCaptureLost += (_, _) =>
        {
            // Capture lost without a release (e.g. window deactivated): drop the half-made selection.
            if (_dragDay == day)
                ClearSelection();
        };
    }

    private void ShowSelection(Grid column)
    {
        if (_selection.Parent != column)
        {
            (_selection.Parent as Grid)?.Children.Remove(_selection);
            column.Children.Add(_selection);
        }

        var from = Math.Min(_anchorSlot, _currentSlot);
        var to = Math.Max(_anchorSlot, _currentSlot) + 1;
        _selection.Margin = new Thickness(2, from * SlotHeight, 2, 0);
        _selection.Height = (to - from) * SlotHeight;
    }

    private static int SlotAt(double y) => Math.Clamp((int)(y / SlotHeight), 0, SlotCount - 1);

    private static Grid CreateColumns()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeColumnWidth) });
        for (var day = 0; day < 7; day++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 36 });
        return grid;
    }

    // Styles from App.xaml rather than brushes: a theme brush looked up in code follows Windows' theme,
    // not the one chosen in Settings → Appearance.
    private static Style Style(string key) => (Style)Application.Current.Resources[key];
}
