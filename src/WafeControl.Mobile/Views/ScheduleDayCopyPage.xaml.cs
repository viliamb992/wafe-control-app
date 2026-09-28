using WafeControl.Core.Localization;
using WafeControl.Core.Threading;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Views;

/// <summary>
/// The "copy a day" sheet: pick the days that get the shown day's actions. Stays open until the unit accepted the
/// plan; errors show inside the sheet.
/// </summary>
public partial class ScheduleDayCopyPage : ContentPage
{
    private readonly ScheduleViewModel _schedule;
    private readonly ScheduleDayCopyViewModel _copy;

    public ScheduleDayCopyPage(ScheduleViewModel schedule, ScheduleDayCopyViewModel copy)
    {
        _schedule = schedule;
        _copy = copy;
        InitializeComponent();
        BindingContext = copy;

        ActionCount.Text = string.Format(Strings.ScheduleCopyActionCount, copy.ActionCount);
        CopyButton.IsEnabled = schedule.CanEdit;
        BuildDayList();
    }

    private void BuildDayList()
    {
        for (var i = 0; i < _copy.Days.Count; i++)
        {
            var day = _copy.Days[i];
            var check = new Label { Text = FluentIcons.Checkmark, Style = Theme.Style("Icon"), IsVisible = day.IsSelected }
                .SetColor(Label.TextColorProperty, "Accent");
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 12,
                MinimumHeightRequest = 44,
            };
            grid.Add(new Label { Text = day.Name, VerticalOptions = LayoutOptions.Center }, 0);
            grid.Add(check, 1);

            var row = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, Content = grid };
            SemanticProperties.SetDescription(row, day.Name);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                day.IsSelected = !day.IsSelected;
                check.IsVisible = day.IsSelected;
                _copy.ErrorMessage = null;
            };
            row.GestureRecognizers.Add(tap);

            if (i > 0)
                DayList.Add(new BoxView { Style = Theme.Style("Divider") });
            DayList.Add(row);
        }
    }

    private void OnCopyClicked(object? sender, EventArgs e) => SafeAsync.Run(CopyAsync);

    private async Task CopyAsync()
    {
        SetBusy(true);
        try
        {
            if (await _schedule.CopyDayAsync(_copy))
                await Navigation.PopModalAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnCancelClicked(object? sender, EventArgs e) => SafeAsync.Run(() => Navigation.PopModalAsync());

    private void SetBusy(bool busy)
    {
        CopyButton.IsVisible = !busy;
        Busy.IsVisible = Busy.IsRunning = busy;
    }
}
