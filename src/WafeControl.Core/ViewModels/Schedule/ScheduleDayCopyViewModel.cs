using CommunityToolkit.Mvvm.ComponentModel;

namespace WafeControl.Core.ViewModels.Schedule;

/// <summary>
/// The "copy a day" dialog: the actions starting on <see cref="FromDay"/> replace those starting on the chosen days.
/// </summary>
public sealed partial class ScheduleDayCopyViewModel : ObservableObject
{
    public ScheduleDayCopyViewModel(int fromDay, int actionCount)
    {
        FromDay = fromDay;
        ActionCount = actionCount;
        Days = Enumerable.Range(0, 7)
            .Where(day => day != fromDay)
            .Select(day => new DayOption(day, ScheduleFormat.DayNames[day]))
            .ToArray();
    }

    /// <summary>
    /// 0 = Monday.
    /// </summary>
    public int FromDay { get; }

    public string FromDayName => ScheduleFormat.DayNames[FromDay];

    /// <summary>
    /// Actions starting on <see cref="FromDay"/>; zero copies an empty day, which clears the chosen days.
    /// </summary>
    public int ActionCount { get; }

    /// <summary>
    /// Every other day of the week, Monday first.
    /// </summary>
    public IReadOnlyList<DayOption> Days { get; }

    public IReadOnlyList<int> SelectedDays => Days.Where(d => d.IsSelected).Select(d => d.Day).ToArray();

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public sealed partial class DayOption(int day, string name) : ObservableObject
    {
        public int Day { get; } = day;

        public string Name { get; } = name;

        [ObservableProperty]
        public partial bool IsSelected { get; set; }
    }
}
