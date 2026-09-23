using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Response from GET /api/v1/schedule, e.g.
/// <c>{"modes": ["min","auto","nom","boost"], "plan": "boost-0:2:0-0:3:0 boost-6:2:0-6:2:30"}</c>.
/// See <see cref="SchedulePlan"/> for the plan format.
/// </summary>
public class ScheduleResponse
{
    /// <summary>
    /// Modes a schedule entry can use.
    /// </summary>
    [JsonPropertyName("modes")]
    public List<string> Modes { get; set; } = [];

    [JsonPropertyName("plan")]
    public string Plan { get; set; } = string.Empty;
}

/// <summary>
/// One weekly recurring schedule action: the unit runs <paramref name="Mode"/> from <paramref name="Start"/> until <paramref name="End"/>.
/// Times are minutes from Monday 00:00. An entry may cross midnight, or wrap from Sunday into Monday (End &lt; Start).
/// </summary>
public sealed record ScheduleEntry(string Mode, int Start, int End)
{
    public const int MinutesPerDay = 24 * 60;
    public const int MinutesPerWeek = 7 * MinutesPerDay;

    public int Duration => Modulo(End - Start, MinutesPerWeek);

    public static int WeekMinute(int day, int minuteOfDay) => Modulo(day * MinutesPerDay + minuteOfDay, MinutesPerWeek);

    /// <summary>
    /// Splits the entry at midnight into per-day pieces, for drawing it in a day grid.
    /// </summary>
    public IEnumerable<DaySegment> DaySegments()
    {
        var remaining = Duration;
        var time = Start;
        while (remaining > 0)
        {
            var minuteOfDay = time % MinutesPerDay;
            var length = Math.Min(remaining, MinutesPerDay - minuteOfDay);
            yield return new DaySegment(time / MinutesPerDay, minuteOfDay, minuteOfDay + length);

            remaining -= length;
            time = (time + length) % MinutesPerWeek;
        }
    }

    public bool Overlaps(ScheduleEntry other) =>
        Intervals().Any(a => other.Intervals().Any(b => a.From < b.To && b.From < a.To));

    // The covered minutes as [From, To) ranges within one week; an entry wrapping past Sunday midnight yields two.
    private IEnumerable<(int From, int To)> Intervals()
    {
        var end = Start + Duration;
        if (end <= MinutesPerWeek)
        {
            yield return (Start, end);
        }
        else
        {
            yield return (Start, MinutesPerWeek);
            yield return (0, end - MinutesPerWeek);
        }
    }

    private static int Modulo(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}

/// <summary>
/// The part of a <see cref="ScheduleEntry"/> that falls on one day (0 = Monday); minutes of that day, End up to 1440.
/// </summary>
public readonly record struct DaySegment(int Day, int StartMinute, int EndMinute);
