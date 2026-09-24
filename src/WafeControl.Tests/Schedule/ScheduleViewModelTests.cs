using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Tests.Schedule;

public class ScheduleViewModelTests
{
    private const int Day = ScheduleEntry.MinutesPerDay;
    private const string Plan = "boost-6:2:0-6:2:30 boost-0:2:0-0:3:0 boost-2:2:0-2:3:0 boost-4:2:0-4:3:0";

    private readonly IWafeApiService _api = Substitute.For<IWafeApiService>();
    private readonly FixedTime _time = new(new DateTime(2026, 9, 24, 14, 0, 0)); // Thursday
    private readonly ScheduleViewModel _sut;

    public ScheduleViewModelTests()
    {
        _api.SetSchedulePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _sut = new ScheduleViewModel(_api, NullLogger<ScheduleViewModel>.Instance, _time);
    }

    private async Task LoadAsync(string plan = Plan)
    {
        _api.GetScheduleAsync(Arg.Any<CancellationToken>())
            .Returns(new ScheduleResponse { Modes = ["min", "auto", "nom", "boost"], Plan = plan });
        await _sut.LoadAsync();
    }

    [Fact]
    public async Task Open_LoadsEntriesAndModes()
    {
        _api.GetScheduleAsync(Arg.Any<CancellationToken>()).Returns(new ScheduleResponse { Modes = ["min", "boost"], Plan = Plan });

        await _sut.OpenCommand.ExecuteAsync(null);

        Assert.True(_sut.IsOpen);
        Assert.True(_sut.IsLoaded);
        Assert.Equal(4, _sut.Entries.Count);
        Assert.Equal(["min", "boost"], _sut.Modes);
        Assert.True(_sut.CanAddEntry);
        Assert.Equal("4 of 50 actions", _sut.EntryCountText);
    }

    [Fact]
    public async Task AddEntry_SendsWholePlanIncludingExistingEntries()
    {
        await LoadAsync();
        var editor = _sut.CreateEntry(day: 0, startMinute: 7 * 60, endMinute: 7 * 60 + 30);

        Assert.Equal("auto", editor.ToEntry().Mode);
        Assert.True(await _sut.SaveEntryAsync(editor));

        await _api.Received(1).SetSchedulePlanAsync(
            "boost-0:2:0-0:3:0 auto-0:7:0-0:7:30 boost-2:2:0-2:3:0 boost-4:2:0-4:3:0 boost-6:2:0-6:2:30",
            Arg.Any<CancellationToken>());
        Assert.Equal(5, _sut.Entries.Count);
    }

    [Fact]
    public async Task EditEntry_ReplacesOriginal()
    {
        await LoadAsync();
        var sunday = _sut.Entries.Single(e => e.Start == 6 * Day + 120);
        var editor = _sut.EditEntry(sunday);
        editor.ModeIndex = 0; // min
        editor.EndTime = TimeSpan.FromHours(4);

        Assert.True(await _sut.SaveEntryAsync(editor));

        await _api.Received(1).SetSchedulePlanAsync(
            "boost-0:2:0-0:3:0 boost-2:2:0-2:3:0 boost-4:2:0-4:3:0 min-6:2:0-6:4:0", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditEntry_KeepingItsOwnTimeRange_IsNotAnOverlap()
    {
        await LoadAsync();
        var editor = _sut.EditEntry(_sut.Entries[0]);

        Assert.True(await _sut.SaveEntryAsync(editor));
    }

    [Fact]
    public async Task DeleteEntry_SendsRemainingEntries()
    {
        await LoadAsync();

        Assert.True(await _sut.DeleteEntryAsync(_sut.Entries.Single(e => e.Start == 2 * Day + 120)));

        await _api.Received(1).SetSchedulePlanAsync(
            "boost-0:2:0-0:3:0 boost-4:2:0-4:3:0 boost-6:2:0-6:2:30", Arg.Any<CancellationToken>());
        Assert.Equal(3, _sut.Entries.Count);
    }

    [Fact]
    public async Task SaveEntry_Overlapping_IsRejectedWithoutRequest()
    {
        await LoadAsync();
        var editor = _sut.CreateEntry(day: 0, startMinute: 150, endMinute: 240);

        Assert.False(await _sut.SaveEntryAsync(editor));

        Assert.Equal("Overlaps with Boost · Mon 02:00 – Mon 03:00.", editor.ErrorMessage);
        await _api.DidNotReceive().SetSchedulePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveEntry_EndBeforeStartSameDay_IsRejected()
    {
        await LoadAsync();
        var editor = _sut.CreateEntry(day: 1, startMinute: 600, endMinute: 630);
        editor.EndTime = TimeSpan.FromHours(9);

        Assert.False(await _sut.SaveEntryAsync(editor));
        Assert.Equal("The end must be after the start.", editor.ErrorMessage);
    }

    [Fact]
    public async Task SaveEntry_ScheduleFull_IsRejected()
    {
        var full = string.Join(' ', Enumerable.Range(0, ScheduleViewModel.MaxEntries)
            .Select(i => $"min-{i / 20}:{i % 20}:0-{i / 20}:{i % 20}:30"));
        await LoadAsync(full);

        Assert.False(_sut.CanAddEntry);
        var editor = _sut.CreateEntry(day: 5, startMinute: 600, endMinute: 630);
        Assert.False(await _sut.SaveEntryAsync(editor));
        Assert.Equal("The schedule is full (50 actions).", editor.ErrorMessage);
    }

    [Fact]
    public async Task SaveEntry_ServerRejects_KeepsEntriesAndReportsError()
    {
        await LoadAsync();
        _api.SetSchedulePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var editor = _sut.CreateEntry(day: 1, startMinute: 600, endMinute: 630);

        Assert.False(await _sut.SaveEntryAsync(editor));

        Assert.Equal(4, _sut.Entries.Count);
        Assert.Equal("Couldn't save the schedule. Please try again.", editor.ErrorMessage);
    }

    [Fact]
    public async Task UnparseablePlan_IsReadOnlyAndNeverWritten()
    {
        await LoadAsync("boost-0:2:0-0:3:0 fireplace@0:5:0");

        Assert.True(_sut.IsReadOnly);
        Assert.False(_sut.CanEdit);
        Assert.NotNull(_sut.ErrorMessage);
        Assert.False(await _sut.DeleteEntryAsync(_sut.Entries[0]));
        Assert.False(await _sut.SaveEntryAsync(_sut.CreateEntry(1, 0, 30)));
        await _api.DidNotReceive().SetSchedulePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadFails_CannotEdit()
    {
        _api.GetScheduleAsync(Arg.Any<CancellationToken>()).Returns((ScheduleResponse?)null);

        await _sut.LoadAsync();

        Assert.False(_sut.IsLoaded);
        Assert.False(_sut.CanAddEntry);
        Assert.Equal("Couldn't load the schedule.", _sut.ErrorMessage);
    }

    [Fact]
    public async Task SaveEntry_ClearedTime_IsRejected()
    {
        await LoadAsync();
        var editor = _sut.CreateEntry(day: 1, startMinute: 600, endMinute: 630);
        editor.StartTime = null;

        Assert.False(await _sut.SaveEntryAsync(editor));
        Assert.Equal("Choose a start and end time.", editor.ErrorMessage);
    }

    [Fact]
    public void CreateEntry_AtMidnight_HasZeroStartTime()
    {
        var editor = _sut.CreateEntry(day: 0, startMinute: 0, endMinute: 30);

        Assert.Equal(TimeSpan.Zero, editor.StartTime);
        Assert.Equal(TimeSpan.FromMinutes(30), editor.EndTime);
    }

    [Fact]
    public async Task EntryCrossingIntoNextWeek_UsesToDay()
    {
        await LoadAsync("");
        var editor = _sut.CreateEntry(day: 6, startMinute: 23 * 60, endMinute: 23 * 60 + 30);
        editor.EndDay = 0;
        editor.EndTime = TimeSpan.FromHours(1);

        Assert.True(await _sut.SaveEntryAsync(editor));

        await _api.Received(1).SetSchedulePlanAsync("auto-6:23:0-0:1:0", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyDay_ReplacesChosenDaysWithSourceDaysActions()
    {
        // Mon 02:00-03:00 and Mon 22:00 - Tue 01:00, Wed 02:00-03:00, Thu 05:00-06:00.
        await LoadAsync("boost-0:2:0-0:3:0 min-0:22:0-1:1:0 boost-2:2:0-2:3:0 nom-3:5:0-3:6:0");
        var copy = _sut.CopyDay(0);
        Assert.Equal(2, copy.ActionCount);
        Assert.Equal([1, 2, 3, 4, 5, 6], copy.Days.Select(d => d.Day));

        copy.Days.Single(d => d.Day == 2).IsSelected = true;
        copy.Days.Single(d => d.Day == 6).IsSelected = true;

        Assert.True(await _sut.CopyDayAsync(copy));

        // Wednesday's own action is replaced; Sunday's late action wraps into Monday.
        await _api.Received(1).SetSchedulePlanAsync(
            "boost-0:2:0-0:3:0 min-0:22:0-1:1:0 boost-2:2:0-2:3:0 min-2:22:0-3:1:0 nom-3:5:0-3:6:0 boost-6:2:0-6:3:0 min-6:22:0-0:1:0",
            Arg.Any<CancellationToken>());
        Assert.Null(copy.ErrorMessage);
        Assert.Equal(7, _sut.Entries.Count);
    }

    [Fact]
    public async Task CopyDay_EmptyDay_ClearsChosenDays()
    {
        await LoadAsync();
        var copy = _sut.CopyDay(1);
        Assert.Equal(0, copy.ActionCount);
        copy.Days.Single(d => d.Day == 2).IsSelected = true;

        Assert.True(await _sut.CopyDayAsync(copy));

        await _api.Received(1).SetSchedulePlanAsync(
            "boost-0:2:0-0:3:0 boost-4:2:0-4:3:0 boost-6:2:0-6:2:30", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyDay_NoDayChosen_IsRejected()
    {
        await LoadAsync();
        var copy = _sut.CopyDay(0);

        Assert.False(await _sut.CopyDayAsync(copy));

        Assert.Equal("Choose at least one day.", copy.ErrorMessage);
        await _api.DidNotReceive().SetSchedulePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyDay_OverlappingTheNextDay_IsRejectedWithoutRequest()
    {
        // Mon 22:00 - Tue 01:00 copied to Tuesday would run into Wednesday's 00:30 action.
        await LoadAsync("min-0:22:0-1:1:0 boost-2:0:30-2:2:0");
        var copy = _sut.CopyDay(0);
        copy.Days.Single(d => d.Day == 1).IsSelected = true;

        Assert.False(await _sut.CopyDayAsync(copy));

        Assert.Equal("Minimum · Tue 22:00 – Wed 01:00 would overlap with Boost · Wed 00:30 – Wed 02:00.", copy.ErrorMessage);
        await _api.DidNotReceive().SetSchedulePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyDay_TooManyActions_IsRejected()
    {
        var monday = string.Join(' ', Enumerable.Range(0, 10).Select(i => $"min-0:{i}:0-0:{i}:30"));
        await LoadAsync(monday);
        var copy = _sut.CopyDay(0);
        foreach (var day in copy.Days)
            day.IsSelected = true;

        Assert.False(await _sut.CopyDayAsync(copy));
        Assert.Equal("The schedule is full (50 actions).", copy.ErrorMessage);
    }

    [Fact]
    public async Task CopyDay_ServerRejects_KeepsEntriesAndReportsError()
    {
        await LoadAsync();
        _api.SetSchedulePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var copy = _sut.CopyDay(0);
        copy.Days.Single(d => d.Day == 1).IsSelected = true;

        Assert.False(await _sut.CopyDayAsync(copy));

        Assert.Equal(4, _sut.Entries.Count);
        Assert.Equal("Couldn't save the schedule. Please try again.", copy.ErrorMessage);
    }

    [Fact]
    public async Task TrackUnitMode_OnSwitchToSchedule_LoadsPlanAndSetsNextStart()
    {
        _api.GetScheduleAsync(Arg.Any<CancellationToken>()).Returns(new ScheduleResponse { Plan = Plan });

        await _sut.TrackUnitModeAsync("manual");
        await _api.DidNotReceive().GetScheduleAsync(Arg.Any<CancellationToken>());

        await _sut.TrackUnitModeAsync("schedule");
        Assert.Equal(new DateTime(2026, 9, 25, 2, 0, 0), _sut.NextStart); // Fri 02:00

        _time.Now = new DateTime(2026, 9, 25, 3, 0, 0);
        await _sut.TrackUnitModeAsync("schedule");
        Assert.Equal(new DateTime(2026, 9, 27, 2, 0, 0), _sut.NextStart); // Sun 02:00, without reloading
        await _api.Received(1).GetScheduleAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrackUnitMode_ReloadsWhenSwitchingBackToSchedule()
    {
        _api.GetScheduleAsync(Arg.Any<CancellationToken>()).Returns(new ScheduleResponse { Plan = Plan });

        await _sut.TrackUnitModeAsync("schedule");
        await _sut.TrackUnitModeAsync("intelligent");
        await _sut.TrackUnitModeAsync("schedule");

        await _api.Received(2).GetScheduleAsync(Arg.Any<CancellationToken>());
    }

    private sealed class FixedTime(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }
}
