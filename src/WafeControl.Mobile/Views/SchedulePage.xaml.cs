using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using WafeControl.Core.ViewModels;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Mobile.Controls;
using WafeControl.Mobile.Helpers;
using WafeControl.Shared;
using WafeControl.Shared.Models;

namespace WafeControl.Mobile.Views;

/// <summary>
/// The weekly schedule, one day at a time: tap an empty time to add an action, tap an action to edit it, swipe for
/// another day, hold a day to copy it. Same <see cref="ScheduleViewModel"/> as the Windows grid.
/// </summary>
public partial class SchedulePage : ContentPage
{
    private const int DefaultDuration = 60;
    private const int DefaultStart = 8 * 60;

    private readonly ScheduleViewModel _schedule;
    private readonly AppViewModel _app;
    private readonly IDispatcherTimer _clock;
    private Window? _window;
    private int _day = Today;
    private bool _isSwitchingDay;

    public SchedulePage(ScheduleViewModel schedule, AppViewModel app)
    {
        _schedule = schedule;
        _app = app;
        InitializeComponent();
        BindingContext = schedule;

        DaySelector.Items = ScheduleFormat.ShortDayNames;
        DaySelector.SelectedIndex = _day;

        _clock = Dispatcher.CreateTimer();
        _clock.Interval = TimeSpan.FromMinutes(1);
        _clock.Tick += (_, _) => Timeline.UpdateNow();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static int Today => ((int)DateTime.Now.DayOfWeek + 6) % 7;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // It may have been changed elsewhere (e.g. the Wafe web app) since it was last shown.
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (!_schedule.IsBusy)
            await _schedule.LoadAsync();
    }

    /// <summary>
    /// Back from the background: reload if this tab is the one shown, as on appearing.
    /// </summary>
    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        if (Shell.Current?.CurrentPage == this && Navigation.ModalStack.Count == 0)
            await ReloadAsync();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _schedule.PropertyChanged += OnSchedulePropertyChanged;
        _app.OperatingMode.PropertyChanged += OnOperatingModePropertyChanged;
        Toast.Attach(_app);
        _clock.Start();
        _window = Window;
        if (_window is not null)
            _window.Resumed += OnWindowResumed;

        ShowDay();
        BuildLegend();
        UpdateModeOffBanner();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _schedule.PropertyChanged -= OnSchedulePropertyChanged;
        _app.OperatingMode.PropertyChanged -= OnOperatingModePropertyChanged;
        Toast.Detach();
        _clock.Stop();
        if (_window is not null)
            _window.Resumed -= OnWindowResumed;
        _window = null;
    }

    private void OnSchedulePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScheduleViewModel.Entries):
                ShowDay();
                break;
            case nameof(ScheduleViewModel.Modes):
                BuildLegend();
                break;
        }
    }

    private void OnOperatingModePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Core.ViewModels.Cards.OperatingModeCardViewModel.SelectedMode))
            UpdateModeOffBanner();
    }

    private void UpdateModeOffBanner() =>
        ModeOffBanner.IsShown = _app.OperatingMode.SelectedMode != AppConstants.ModeSchedule;

    private void OnUseScheduleModeClicked(object? sender, EventArgs e)
    {
        var operatingMode = _app.OperatingMode;
        if (!operatingMode.UpdateModeCommand.CanExecute(null))
            return;

        operatingMode.SelectedMode = AppConstants.ModeSchedule;
        operatingMode.UpdateModeCommand.Execute(null);
    }

    // ── Day ──────────────────────────────────────────────────────────────

    private void OnDayTapped(object? sender, int day)
    {
        _day = day;
        ShowDay();
    }

    private async void OnTimelineSwiped(object? sender, SwipeDirection direction)
    {
        if (_isSwitchingDay)
            return;

        // Left brings in the next day, as if the week were a row of pages; wraps around.
        var step = direction == SwipeDirection.Left ? 1 : -1;
        _isSwitchingDay = true;
        try
        {
            var width = TimelineCard.Width;
            await Task.WhenAll(
                Timeline.TranslateToAsync(-step * width / 3, 0, 150, Easing.CubicIn),
                Timeline.FadeToAsync(0, 150, Easing.CubicIn));

            _day = (_day + step + 7) % 7;
            DaySelector.SelectedIndex = _day;
            ShowDay();

            Timeline.TranslationX = step * width / 3;
            await Task.WhenAll(
                Timeline.TranslateToAsync(0, 0, 250, Easing.CubicOut),
                Timeline.FadeToAsync(1, 250, Easing.CubicOut));
        }
        finally
        {
            Timeline.TranslationX = 0;
            Timeline.Opacity = 1;
            _isSwitchingDay = false;
        }
    }

    private async void OnDayLongPressed(object? sender, int day)
    {
        _day = day;
        DaySelector.SelectedIndex = day;
        ShowDay();
        await ShowCopyDayAsync();
    }

    private async void OnCopyDayClicked(object? sender, EventArgs e) => await ShowCopyDayAsync();

    private async Task ShowCopyDayAsync()
    {
        if (_schedule.CanEdit && Navigation.ModalStack.Count == 0)
            await Navigation.PushModalAsync(new ScheduleDayCopyPage(_schedule, _schedule.CopyDay(_day)));
    }

    private void ShowDay()
    {
        DayName.Text = ScheduleFormat.DayNames[_day];
        Timeline.Show(_day, _schedule.Entries);
    }

    private void BuildLegend()
    {
        Legend.Children.Clear();
        foreach (var mode in _schedule.Modes)
        {
            Legend.Children.Add(new HorizontalStackLayout
            {
                Spacing = 6,
                Margin = new Thickness(0, 0, 16, 8),
                Children =
                {
                    new Border
                    {
                        WidthRequest = 12,
                        HeightRequest = 12,
                        StrokeThickness = 0,
                        StrokeShape = new RoundRectangle { CornerRadius = 3 },
                        BackgroundColor = Theme.ScheduleMode(mode),
                        VerticalOptions = LayoutOptions.Center,
                    },
                    new Label { Text = ScheduleFormat.ModeName(mode), Style = Theme.Style("Caption"), VerticalOptions = LayoutOptions.Center },
                },
            });
        }
    }

    // ── Add / edit ───────────────────────────────────────────────────────

    private async void OnSlotTapped(object? sender, int minute)
    {
        if (_schedule.CanAddEntry)
            await ShowEditorAsync(_schedule.CreateEntry(_day, minute, EndOfFreeTime(minute)));
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var start = FirstFreeMinute();
        await ShowEditorAsync(_schedule.CreateEntry(_day, start, EndOfFreeTime(start)));
    }

    private async void OnEntryTapped(object? sender, ScheduleEntry entry) => await ShowEditorAsync(_schedule.EditEntry(entry));

    private async Task ShowEditorAsync(ScheduleEntryEditorViewModel editor)
    {
        if (Navigation.ModalStack.Count == 0)
            await Navigation.PushModalAsync(new ScheduleEntryPage(_schedule, editor));
    }

    /// <summary>
    /// A new action starts at the next free half hour: from now on today, from 08:00 on other days.
    /// </summary>
    private int FirstFreeMinute()
    {
        var start = _day == Today
            ? ((int)DateTime.Now.TimeOfDay.TotalMinutes / DayTimeline.SlotMinutes + 1) * DayTimeline.SlotMinutes
            : DefaultStart;

        for (var minute = Math.Min(start, ScheduleEntry.MinutesPerDay - DayTimeline.SlotMinutes);
             minute < ScheduleEntry.MinutesPerDay;
             minute += DayTimeline.SlotMinutes)
        {
            if (!IsTaken(minute))
                return minute;
        }

        return DefaultStart;
    }

    /// <summary>
    /// An hour from <paramref name="start"/>, or less when the next action (or midnight) comes first.
    /// </summary>
    private int EndOfFreeTime(int start)
    {
        var end = Math.Min(start + DefaultDuration, ScheduleEntry.MinutesPerDay);
        foreach (var segment in Segments())
        {
            if (segment.StartMinute > start)
                end = Math.Min(end, segment.StartMinute);
        }

        return end;
    }

    private bool IsTaken(int minute) => Segments().Any(s => s.StartMinute <= minute && minute < s.EndMinute);

    private IEnumerable<DaySegment> Segments() =>
        _schedule.Entries.SelectMany(entry => entry.DaySegments()).Where(s => s.Day == _day);
}
