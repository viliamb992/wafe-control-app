using System.Text.Json;
using WafeControl.Shared.Models;
using WafeControl.Shared.Serialization;

namespace WafeControl.Tests.Schedule;

public class SchedulePlanTests
{
    private const int Day = ScheduleEntry.MinutesPerDay;

    // The PUT body the Wafe web app sends (sorted by start).
    private const string WebAppPlan =
        "boost-0:2:0-0:3:0 auto-0:7:0-0:7:30 auto-1:7:0-1:7:30 boost-2:2:0-2:3:0 min-2:7:0-2:7:30 min-3:7:0-3:7:30 boost-4:2:0-4:3:0 boost-6:2:0-6:2:30";

    [Fact]
    public void Fixture_GetSchedule_DeserializesAndParses()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "schedule.json"));

        var response = JsonSerializer.Deserialize(json, WafeJsonContext.Default.ScheduleResponse);

        Assert.NotNull(response);
        Assert.Equal(["min", "auto", "nom", "boost"], response.Modes);
        Assert.True(SchedulePlan.TryParse(response.Plan, out var entries));
        // Day 0 is Monday: Mon/Wed/Fri 02:00–03:00 and Sun 02:00–02:30, as in the web app's grid.
        Assert.Equal(
            [
                new ScheduleEntry("boost", 6 * Day + 120, 6 * Day + 150),
                new ScheduleEntry("boost", 0 * Day + 120, 0 * Day + 180),
                new ScheduleEntry("boost", 2 * Day + 120, 2 * Day + 180),
                new ScheduleEntry("boost", 4 * Day + 120, 4 * Day + 180),
            ],
            entries);
    }

    [Fact]
    public void Format_RoundTripsWebAppPlanExactly()
    {
        Assert.True(SchedulePlan.TryParse(WebAppPlan, out var entries));

        Assert.Equal(WebAppPlan, SchedulePlan.Format(entries));
    }

    [Fact]
    public void Format_SortsByStart()
    {
        var entries = new[] { new ScheduleEntry("boost", 6 * Day + 120, 6 * Day + 150), new ScheduleEntry("min", 90, 120) };

        Assert.Equal("min-0:1:30-0:2:0 boost-6:2:0-6:2:30", SchedulePlan.Format(entries));
    }

    [Fact]
    public void Format_Empty_IsEmptyString() => Assert.Equal("", SchedulePlan.Format([]));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_EmptyPlan_HasNoEntries(string? plan)
    {
        Assert.True(SchedulePlan.TryParse(plan, out var entries));
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData("boost-0:2:0")]
    [InlineData("boost-8:2:0-0:3:0")]
    [InlineData("boost-0:25:0-0:26:0")]
    [InlineData("boost-0:2:0-0:2:0")]
    [InlineData("boost-0:2:61-0:3:0")]
    [InlineData("0:2:0-0:3:0")]
    public void TryParse_InvalidToken_ReturnsFalseButKeepsValidEntries(string badToken)
    {
        Assert.False(SchedulePlan.TryParse($"min-0:1:0-0:1:30 {badToken}", out var entries));
        Assert.Equal([new ScheduleEntry("min", 60, 90)], entries);
    }

    [Theory]
    [InlineData("auto-0:22:0-0:24:0", 22 * 60, Day)]
    [InlineData("auto-6:23:0-7:0:0", 6 * Day + 23 * 60, 0)]
    public void TryParse_EndOfDayOrWeek_IsNormalized(string plan, int start, int end)
    {
        Assert.True(SchedulePlan.TryParse(plan, out var entries));
        Assert.Equal(new ScheduleEntry("auto", start, end), Assert.Single(entries));
    }

    [Fact]
    public void DaySegments_CrossingMidnight_SplitsPerDay()
    {
        var entry = new ScheduleEntry("nom", 23 * 60, Day + 60); // Mon 23:00 – Tue 01:00

        Assert.Equal([new DaySegment(0, 23 * 60, Day), new DaySegment(1, 0, 60)], entry.DaySegments());
    }

    [Fact]
    public void DaySegments_WrappingSundayIntoMonday_Splits()
    {
        var entry = new ScheduleEntry("nom", 6 * Day + 23 * 60, 30); // Sun 23:00 – Mon 00:30

        Assert.Equal(90, entry.Duration);
        Assert.Equal([new DaySegment(6, 23 * 60, Day), new DaySegment(0, 0, 30)], entry.DaySegments());
    }

    [Theory]
    [InlineData(120, 180, 150, 210, true)]   // partial overlap
    [InlineData(120, 180, 180, 240, false)]  // touching after
    [InlineData(120, 180, 60, 120, false)]   // touching before
    [InlineData(120, 240, 150, 180, true)]   // contained
    public void Overlaps_SameDay(int s1, int e1, int s2, int e2, bool expected)
    {
        Assert.Equal(expected, new ScheduleEntry("a", s1, e1).Overlaps(new ScheduleEntry("b", s2, e2)));
    }

    [Fact]
    public void Overlaps_WrappingEntry_DetectsMondayMorningClash()
    {
        var wrapping = new ScheduleEntry("nom", 6 * Day + 23 * 60, 60); // Sun 23:00 – Mon 01:00

        Assert.True(wrapping.Overlaps(new ScheduleEntry("boost", 30, 90)));
        Assert.False(wrapping.Overlaps(new ScheduleEntry("boost", 60, 90)));
    }

    // 2026-09-24 is a Thursday (day 3).
    private static readonly DateTime Thursday = new(2026, 9, 24);

    [Fact]
    public void NextStart_PicksTheEarliestUpcomingStart()
    {
        var entries = new[] { new ScheduleEntry("boost", 0 * Day + 120, 0 * Day + 180), new ScheduleEntry("auto", 4 * Day + 420, 4 * Day + 450) };

        Assert.Equal(new DateTime(2026, 9, 25, 7, 0, 0), SchedulePlan.NextStart(entries, Thursday.AddHours(14).AddMinutes(10)));
    }

    [Fact]
    public void NextStart_StartAlreadyPassedThisWeek_IsNextWeek()
    {
        var entries = new[] { new ScheduleEntry("boost", 3 * Day + 120, 3 * Day + 180) }; // Thu 02:00

        Assert.Equal(new DateTime(2026, 10, 1, 2, 0, 0), SchedulePlan.NextStart(entries, Thursday.AddHours(2).AddSeconds(1)));
        Assert.Equal(new DateTime(2026, 9, 24, 2, 0, 0), SchedulePlan.NextStart(entries, Thursday.AddHours(1).AddMinutes(59).AddSeconds(59)));
    }

    [Fact]
    public void NextStart_SkipsEntriesContinuingAnotherOne()
    {
        // Fri 07:00–08:00 then 08:00–09:00: the unit switches on once, at 07:00.
        var entries = new[] { new ScheduleEntry("auto", 4 * Day + 420, 4 * Day + 480), new ScheduleEntry("boost", 4 * Day + 480, 4 * Day + 540) };

        Assert.Equal(new DateTime(2026, 10, 2, 7, 0, 0), SchedulePlan.NextStart(entries, Thursday.AddDays(1).AddHours(7).AddMinutes(30)));
    }

    [Fact]
    public void NextStart_EmptyPlan_IsNull()
    {
        Assert.Null(SchedulePlan.NextStart([], Thursday));
    }
}
