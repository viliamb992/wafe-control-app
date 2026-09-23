using CommunityToolkit.Mvvm.ComponentModel;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Core.ViewModels.Schedule;

/// <summary>
/// The add/edit dialog for one schedule entry: from day + time, to day + time, mode.
/// </summary>
public sealed partial class ScheduleEntryEditorViewModel : ObservableObject
{
    public ScheduleEntryEditorViewModel(IReadOnlyList<string> modes, ScheduleEntry entry, ScheduleEntry? original)
    {
        Modes = modes;
        ModeNames = modes.Select(ScheduleFormat.ModeName).ToArray();
        Original = original;

        StartDay = entry.Start / ScheduleEntry.MinutesPerDay;
        StartTime = TimeSpan.FromMinutes(entry.Start % ScheduleEntry.MinutesPerDay);
        EndDay = entry.End / ScheduleEntry.MinutesPerDay;
        EndTime = TimeSpan.FromMinutes(entry.End % ScheduleEntry.MinutesPerDay);
        ModeIndex = Math.Max(0, IndexOf(modes, entry.Mode));
    }

    /// <summary>
    /// The entry being edited; null when adding.
    /// </summary>
    public ScheduleEntry? Original { get; }

    public bool IsNew => Original is null;

    public IReadOnlyList<string> Modes { get; }

    public IReadOnlyList<string> ModeNames { get; }

    public IReadOnlyList<string> DayNames => ScheduleFormat.DayNames;

    [ObservableProperty]
    public partial int StartDay { get; set; }

    /// <summary>
    /// Time of day; nullable because time pickers can be cleared.
    /// </summary>
    [ObservableProperty]
    public partial TimeSpan? StartTime { get; set; }

    [ObservableProperty]
    public partial int EndDay { get; set; }

    [ObservableProperty]
    public partial TimeSpan? EndTime { get; set; }

    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public ScheduleEntry ToEntry() => new(
        Modes[Math.Clamp(ModeIndex, 0, Modes.Count - 1)],
        ScheduleEntry.WeekMinute(StartDay, (int)(StartTime ?? TimeSpan.Zero).TotalMinutes),
        ScheduleEntry.WeekMinute(EndDay, (int)(EndTime ?? TimeSpan.Zero).TotalMinutes));

    /// <summary>
    /// Returns why the entry can't be saved next to <paramref name="otherEntries"/>, or null if it can.
    /// </summary>
    public string? Validate(IEnumerable<ScheduleEntry> otherEntries)
    {
        if (ModeIndex < 0 || ModeIndex >= Modes.Count)
            return Strings.ScheduleChooseMode;

        if (StartTime is null || EndTime is null)
            return Strings.ScheduleChooseTimes;

        if (StartDay == EndDay && EndTime <= StartTime)
            return Strings.ScheduleEndBeforeStart;

        var entry = ToEntry();
        var clash = otherEntries.FirstOrDefault(entry.Overlaps);
        return clash is null ? null : string.Format(Strings.ScheduleOverlaps, ScheduleFormat.Describe(clash));
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return i;
        }

        return -1;
    }
}
