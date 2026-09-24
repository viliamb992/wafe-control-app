using Microsoft.Maui.Controls.Shapes;
using WafeControl.Core.Localization;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Mobile.Helpers;

namespace WafeControl.Mobile.Views;

/// <summary>
/// The add/edit sheet for one schedule action, like the Windows dialog. Stays open until the unit accepted the plan;
/// errors show inside the sheet.
/// </summary>
public partial class ScheduleEntryPage : ContentPage
{
    private readonly ScheduleViewModel _schedule;
    private readonly ScheduleEntryEditorViewModel _editor;
    private readonly List<(Border Row, Label Check)> _modeRows = [];

    public ScheduleEntryPage(ScheduleViewModel schedule, ScheduleEntryEditorViewModel editor)
    {
        _schedule = schedule;
        _editor = editor;
        InitializeComponent();
        BindingContext = editor;

        SheetTitle.Text = editor.IsNew ? Strings.ScheduleAddAction : Strings.ScheduleEditAction;
        SaveButton.Text = editor.IsNew ? Strings.ButtonAdd : Strings.ButtonSave;
        SaveButton.IsEnabled = schedule.CanEdit;
        DeleteButton.IsVisible = !editor.IsNew;
        DeleteButton.IsEnabled = schedule.CanEdit;
        BuildModeList();
    }

    private void BuildModeList()
    {
        for (var i = 0; i < _editor.Modes.Count; i++)
        {
            var index = i;
            var check = new Label { Text = FluentIcons.Checkmark, Style = Theme.Style("Icon") }.SetColor(Label.TextColorProperty, "Accent");
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 12,
                MinimumHeightRequest = 44,
            };
            grid.Add(new Border
            {
                WidthRequest = 16,
                HeightRequest = 16,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                BackgroundColor = Theme.ScheduleMode(_editor.Modes[i]),
                VerticalOptions = LayoutOptions.Center,
            }, 0);
            grid.Add(new Label { Text = _editor.ModeNames[i], VerticalOptions = LayoutOptions.Center }, 1);
            grid.Add(check, 2);

            var row = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, Content = grid };
            SemanticProperties.SetDescription(row, _editor.ModeNames[i]);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SelectMode(index);
            row.GestureRecognizers.Add(tap);

            if (i > 0)
                ModeList.Add(new BoxView { Style = Theme.Style("Divider") });
            ModeList.Add(row);
            _modeRows.Add((row, check));
        }

        SelectMode(_editor.ModeIndex);
    }

    private void SelectMode(int index)
    {
        _editor.ModeIndex = index;
        for (var i = 0; i < _modeRows.Count; i++)
            _modeRows[i].Check.IsVisible = i == index;
    }

    private async void OnSaveClicked(object? sender, EventArgs e) => await RunAsync(() => _schedule.SaveEntryAsync(_editor));

    private async void OnDeleteClicked(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        var deleted = await _schedule.DeleteEntryAsync(_editor.Original!);
        if (!deleted)
            _editor.ErrorMessage = _schedule.ErrorMessage;
        return deleted;
    });

    private async void OnCancelClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private async Task RunAsync(Func<Task<bool>> action)
    {
        SetBusy(true);
        try
        {
            if (await action())
                await Navigation.PopModalAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        SaveButton.IsVisible = !busy;
        Busy.IsVisible = Busy.IsRunning = busy;
        DeleteButton.IsEnabled = !busy && _schedule.CanEdit;
    }
}
