using System.Globalization;
using WafeControl.Core.Localization;
using WafeControl.Shared;
using WafeControl.Shared.Models;

namespace WafeControl.Core.ViewModels.Schedule;

/// <summary>
/// Display text for schedule entries, shared by every app. Follows the current app language.
/// </summary>
public static class ScheduleFormat
{
    /// <summary>
    /// Monday first, as in the plan format (day 0 = Monday).
    /// </summary>
    public static IReadOnlyList<string> DayNames => Strings.ScheduleDayNames.Split(',');

    public static IReadOnlyList<string> ShortDayNames => Strings.ScheduleShortDayNames.Split(',');

    public static string ModeName(string mode) => mode switch
    {
        AppConstants.ScheduleModeMin => Strings.ScheduleModeMinimum,
        AppConstants.ScheduleModeAuto => Strings.ModeIntelligent,
        AppConstants.ScheduleModeNominal => Strings.ScheduleModeNominal,
        AppConstants.ScheduleModeBoost => Strings.ScheduleModeBoost,
        _ => mode,
    };

    /// <summary>
    /// "Mon 02:00" for a minute of the week.
    /// </summary>
    public static string WeekTime(int weekMinute)
    {
        var minuteOfDay = weekMinute % ScheduleEntry.MinutesPerDay;
        return $"{ShortDayNames[weekMinute / ScheduleEntry.MinutesPerDay]} {minuteOfDay / 60:D2}:{minuteOfDay % 60:D2}";
    }

    /// <summary>
    /// "Thu September 25 14:30" for a date and time (local), in the current culture's month-day format.
    /// </summary>
    public static string DateAndTime(DateTime value) =>
        $"{ShortDayNames[((int)value.DayOfWeek + 6) % 7]} {value.ToString("M", CultureInfo.CurrentCulture)} {value:HH:mm}";

    /// <summary>
    /// "Boost · Mon 02:00 – Mon 03:00".
    /// </summary>
    public static string Describe(ScheduleEntry entry) =>
        $"{ModeName(entry.Mode)} · {WeekTime(entry.Start)} – {WeekTime(entry.End)}";
}
