using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Mobile.Helpers;
using WafeControl.Shared.Models;

namespace WafeControl.Mobile.Controls;

/// <summary>
/// One day of the weekly schedule as a 24-hour timeline (DESIGN.md, section 7): 30-minute rows, entries as blocks in
/// their mode color and a "now" line on today. Tapping an empty row raises <see cref="SlotTapped"/> (minute of the day);
/// tapping a block raises <see cref="EntryTapped"/>; a horizontal swipe raises <see cref="Swiped"/>.
/// </summary>
public sealed class DayTimeline : ContentView
{
    public const int SlotMinutes = 30;

    private const double SlotHeight = 24;
    private const double PixelsPerMinute = SlotHeight / SlotMinutes;
    private const double DayHeight = ScheduleEntry.MinutesPerDay * PixelsPerMinute;
    private const double TimeColumnWidth = 48;

    private readonly AbsoluteLayout _canvas;
    private readonly List<View> _blocks = [];
    private readonly BoxView _nowLine;
    private IReadOnlyList<ScheduleEntry> _entries = [];
    private int _day;

    public DayTimeline()
    {
        var times = new AbsoluteLayout { HeightRequest = DayHeight, WidthRequest = TimeColumnWidth };
        _canvas = new AbsoluteLayout { HeightRequest = DayHeight, BackgroundColor = Colors.Transparent };

        for (var hour = 0; hour <= 24; hour++)
        {
            var y = hour * 60 * PixelsPerMinute;
            var line = new BoxView { Style = Theme.Style("Divider") };
            _canvas.Add(line);
            AbsoluteLayout.SetLayoutBounds(line, new Rect(0, Math.Min(y, DayHeight - 1), 1, 1));
            AbsoluteLayout.SetLayoutFlags(line, AbsoluteLayoutFlags.WidthProportional);

            if (hour is > 0 and < 24)
            {
                var label = new Label { Text = $"{hour:D2}:00", Style = Theme.Style("Caption") }.SetColor(Label.TextColorProperty, "TextTertiary");
                times.Add(label);
                AbsoluteLayout.SetLayoutBounds(label, new Rect(0, y - 8, TimeColumnWidth, 16));
            }
        }

        _nowLine = new BoxView { HeightRequest = 2, IsVisible = false, InputTransparent = true, ZIndex = 1 }.SetColor(BoxView.ColorProperty, "Critical");
        _canvas.Add(_nowLine);
        AbsoluteLayout.SetLayoutFlags(_nowLine, AbsoluteLayoutFlags.WidthProportional);

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnCanvasTapped;
        _canvas.GestureRecognizers.Add(tap);

        // On the canvas, not a parent: the canvas takes the touches for its taps.
        foreach (var direction in new[] { SwipeDirection.Left, SwipeDirection.Right })
        {
            var swipe = new SwipeGestureRecognizer { Direction = direction };
            swipe.Swiped += (_, e) => Swiped?.Invoke(this, e.Direction);
            _canvas.GestureRecognizers.Add(swipe);
        }

        var root = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(TimeColumnWidth), new ColumnDefinition(GridLength.Star) },
            Padding = new Thickness(0, 8),
        };
        root.Add(times, 0);
        root.Add(_canvas, 1);
        Content = root;
    }

    public event EventHandler<int>? SlotTapped;

    public event EventHandler<ScheduleEntry>? EntryTapped;

    public event EventHandler<SwipeDirection>? Swiped;

    /// <summary>
    /// Shows <paramref name="day"/> (0 = Monday) of <paramref name="entries"/>.
    /// </summary>
    public void Show(int day, IReadOnlyList<ScheduleEntry> entries)
    {
        _day = day;
        _entries = entries;

        foreach (var block in _blocks)
            _canvas.Remove(block);
        _blocks.Clear();

        foreach (var entry in entries)
        {
            foreach (var segment in entry.DaySegments().Where(s => s.Day == day))
                AddBlock(entry, segment);
        }

        UpdateNow();
    }

    /// <summary>
    /// Moves the "now" line; shown only on today.
    /// </summary>
    public void UpdateNow()
    {
        var now = DateTime.Now;
        var today = ((int)now.DayOfWeek + 6) % 7;
        _nowLine.IsVisible = today == _day;
        AbsoluteLayout.SetLayoutBounds(_nowLine, new Rect(0, now.TimeOfDay.TotalMinutes * PixelsPerMinute - 1, 1, 2));
    }

    /// <summary>
    /// The y position of a minute of the day, for scrolling to it.
    /// </summary>
    public static double OffsetOf(int minuteOfDay) => 8 + minuteOfDay * PixelsPerMinute;

    private void AddBlock(ScheduleEntry entry, DaySegment segment)
    {
        var height = (segment.EndMinute - segment.StartMinute) * PixelsPerMinute;
        var mode = ScheduleFormat.ModeName(entry.Mode);
        var times = $"{Time(segment.StartMinute)} – {Time(segment.EndMinute)}";

        var title = new Label
        {
            Text = height < 40 ? $"{mode} · {times}" : mode,
            Style = Theme.Style("BodyStrong"),
            FontSize = 12,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.TailTruncation,
        };
        var content = new VerticalStackLayout { Spacing = 0, Children = { title } };
        if (height >= 40)
        {
            content.Add(new Label
            {
                Text = times,
                Style = Theme.Style("Caption"),
                TextColor = Colors.White,
                LineBreakMode = LineBreakMode.TailTruncation,
            });
        }

        var block = new Border
        {
            BackgroundColor = Theme.ScheduleMode(entry.Mode),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = new Thickness(10, height < 40 ? 0 : 4),
            Margin = new Thickness(4, 1, 0, 1),
            Content = content,
        };
        if (height < 40)
            title.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(block, ScheduleFormat.Describe(entry));

        // Taps go to the canvas, which finds the entry under the finger.
        block.InputTransparent = true;

        _canvas.Add(block);
        AbsoluteLayout.SetLayoutBounds(block, new Rect(0, segment.StartMinute * PixelsPerMinute, 1, Math.Max(height, SlotHeight / 2)));
        AbsoluteLayout.SetLayoutFlags(block, AbsoluteLayoutFlags.WidthProportional);
        _blocks.Add(block);
    }

    private void OnCanvasTapped(object? sender, TappedEventArgs e)
    {
        if (e.GetPosition(_canvas) is not { } position)
            return;

        var minute = Math.Clamp((int)(position.Y / PixelsPerMinute), 0, ScheduleEntry.MinutesPerDay - 1);

        var hit = _entries.FirstOrDefault(entry =>
            entry.DaySegments().Any(s => s.Day == _day && s.StartMinute <= minute && minute < s.EndMinute));
        if (hit is not null)
            EntryTapped?.Invoke(this, hit);
        else
            SlotTapped?.Invoke(this, minute / SlotMinutes * SlotMinutes);
    }

    private static string Time(int minuteOfDay) => $"{minuteOfDay / 60:D2}:{minuteOfDay % 60:D2}";
}
