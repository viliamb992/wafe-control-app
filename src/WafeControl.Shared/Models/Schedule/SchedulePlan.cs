using System.Text.RegularExpressions;

namespace WafeControl.Shared.Models;

/// <summary>
/// The Wafe schedule plan string: space-separated <c>mode-D:H:M-D:H:M</c> entries,
/// where D is the day (0 = Monday … 6 = Sunday), e.g. <c>boost-0:2:0-0:3:0 auto-6:22:30-0:1:0</c>.
/// The API replaces the whole plan on every PUT, so the full list is always sent.
/// </summary>
public static partial class SchedulePlan
{
    /// <summary>
    /// Parses a plan. Returns false (with the entries that could be read) if any part is not understood,
    /// so callers can avoid writing back a plan that would lose data.
    /// </summary>
    public static bool TryParse(string? plan, out IReadOnlyList<ScheduleEntry> entries)
    {
        var parsed = new List<ScheduleEntry>();
        var ok = true;

        foreach (var token in (plan ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = EntryPattern().Match(token);
            if (match.Success
                && TryParseTime(match.Groups["start"].Value, out var start)
                && TryParseTime(match.Groups["end"].Value, out var end)
                && start != end)
            {
                parsed.Add(new ScheduleEntry(match.Groups["mode"].Value, start, end));
            }
            else
            {
                ok = false;
            }
        }

        entries = parsed;
        return ok;
    }

    /// <summary>
    /// Formats entries in the API's order (by start time).
    /// </summary>
    public static string Format(IEnumerable<ScheduleEntry> entries) =>
        string.Join(' ', entries.OrderBy(e => e.Start).Select(e => $"{e.Mode}-{FormatTime(e.Start)}-{FormatTime(e.End)}"));

    /// <summary>
    /// When the schedule next switches the unit on after <paramref name="now"/> (local time), or null for an empty plan.
    /// An entry starting right where another ends continues the run, so it isn't a start.
    /// </summary>
    public static DateTime? NextStart(IEnumerable<ScheduleEntry> entries, DateTime now)
    {
        var list = entries.ToList();
        var ends = list.Select(e => e.End).ToHashSet();
        var starts = list.Where(e => !ends.Contains(e.Start)).Select(e => e.Start).ToList();
        if (starts.Count == 0)
            return null;

        var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind);
        var nowWeekMinute = ScheduleEntry.WeekMinute(((int)now.DayOfWeek + 6) % 7, now.Hour * 60 + now.Minute);

        return starts
            .Select(start => minute.AddMinutes(ScheduleEntry.WeekMinute(0, start - nowWeekMinute)))
            .Select(next => next <= now ? next.AddMinutes(ScheduleEntry.MinutesPerWeek) : next)
            .Min();
    }

    private static string FormatTime(int weekMinute) =>
        $"{weekMinute / ScheduleEntry.MinutesPerDay}:{weekMinute % ScheduleEntry.MinutesPerDay / 60}:{weekMinute % 60}";

    // "D:H:M"; tolerates 24:00 and day 7 as "end of the day/week" and normalizes them.
    private static bool TryParseTime(string value, out int weekMinute)
    {
        weekMinute = 0;
        var parts = value.Split(':');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var hour) || !int.TryParse(parts[2], out var minute)
            || day is < 0 or > 7 || hour is < 0 or > 24 || minute is < 0 or > 59 || (hour == 24 && minute != 0))
        {
            return false;
        }

        weekMinute = ScheduleEntry.WeekMinute(day, hour * 60 + minute);
        return true;
    }

    [GeneratedRegex(@"^(?<mode>[A-Za-z0-9_]+)-(?<start>\d+:\d+:\d+)-(?<end>\d+:\d+:\d+)$")]
    private static partial Regex EntryPattern();
}
