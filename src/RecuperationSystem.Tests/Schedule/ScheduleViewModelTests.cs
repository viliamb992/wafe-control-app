using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RecuperationSystem.Core.ViewModels.Schedule;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Services;

namespace RecuperationSystem.Tests.Schedule;

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
