using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Shared;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Services;

namespace RecuperationSystem.Core.ViewModels.Schedule;

/// <summary>
/// The weekly schedule: loads the plan, and adds, edits or deletes entries.
/// Every change sends the complete plan, because the API replaces it as a whole.
/// </summary>
public sealed partial class ScheduleViewModel : ObservableObject
{
    public const int MaxEntries = 50;

    private static readonly IReadOnlyList<string> DefaultModes =
    [
        AppConstants.ScheduleModeMin,
        AppConstants.ScheduleModeAuto,
        AppConstants.ScheduleModeNominal,
        AppConstants.ScheduleModeBoost,
    ];

    private readonly IWafeApiService _apiService;
    private readonly ILogger<ScheduleViewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private string? _unitMode;

    public ScheduleViewModel(IWafeApiService apiService, ILogger<ScheduleViewModel> logger, TimeProvider? timeProvider = null)
    {
        _apiService = apiService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Entries = [];
        Modes = DefaultModes;
    }

    /// <summary>
    /// Whether the schedule screen is shown.
    /// </summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(CanEdit), nameof(CanAddEntry))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(CanEdit), nameof(CanAddEntry))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// True once a plan was loaded; until then there's nothing safe to write back.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanAddEntry))]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    /// The plan contains entries this app can't parse. Saving would drop them, so editing is disabled.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(CanAddEntry))]
    public partial bool IsReadOnly { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddEntry), nameof(EntryCountText))]
    public partial IReadOnlyList<ScheduleEntry> Entries { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Modes { get; private set; }

    /// <summary>
    /// When the schedule next switches the unit on (local time); null until the plan is loaded, or if it's empty.
    /// Kept current by <see cref="TrackUnitModeAsync"/>.
    /// </summary>
    [ObservableProperty]
    public partial DateTime? NextStart { get; private set; }

    public bool IsBusy => IsLoading || IsSaving;

    public bool CanEdit => IsLoaded && !IsReadOnly && !IsBusy;

    public bool CanAddEntry => CanEdit && Entries.Count < MaxEntries;

    public string EntryCountText => string.Format(Strings.ScheduleEntryCount, Entries.Count, MaxEntries);

    /// <summary>
    /// Call on every status update with the unit's operating mode. While it runs on the schedule this keeps
    /// <see cref="NextStart"/> current, and reloads the plan on switching to the schedule, as it may have
    /// been changed elsewhere (e.g. in the Wafe web app).
    /// </summary>
    public async Task TrackUnitModeAsync(string? unitMode)
    {
        var entered = unitMode == AppConstants.ModeSchedule && _unitMode != AppConstants.ModeSchedule;
        _unitMode = unitMode;
        if (unitMode != AppConstants.ModeSchedule)
            return;

        if ((entered || !IsLoaded) && !IsBusy)
            await LoadAsync();
        else
            UpdateNextStart();
    }

    partial void OnEntriesChanged(IReadOnlyList<ScheduleEntry> value) => UpdateNextStart();

    private void UpdateNextStart() => NextStart = SchedulePlan.NextStart(Entries, _timeProvider.GetLocalNow().DateTime);

    [RelayCommand]
    private async Task OpenAsync()
    {
        IsOpen = true;
        await LoadAsync();
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var response = await _apiService.GetScheduleAsync();
            if (response is null)
            {
                ErrorMessage = Strings.ScheduleLoadFailed;
                return;
            }

            Modes = response.Modes.Count > 0 ? response.Modes : DefaultModes;
            IsReadOnly = !SchedulePlan.TryParse(response.Plan, out var entries);
            Entries = entries;
            IsLoaded = true;

            if (IsReadOnly)
            {
                _logger.LogWarning("Schedule plan not fully understood, shown read-only: {Plan}", response.Plan);
                ErrorMessage = Strings.ScheduleReadOnly;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loading the schedule failed");
            ErrorMessage = Strings.ScheduleLoadFailed;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// An editor for a new entry on <paramref name="day"/>, from and to the given minutes of that day.
    /// </summary>
    public ScheduleEntryEditorViewModel CreateEntry(int day, int startMinute, int endMinute)
    {
        var mode = Modes.Contains(AppConstants.ScheduleModeAuto) ? AppConstants.ScheduleModeAuto : Modes[0];
        var entry = new ScheduleEntry(mode, ScheduleEntry.WeekMinute(day, startMinute), ScheduleEntry.WeekMinute(day, endMinute));
        return new ScheduleEntryEditorViewModel(Modes, entry, original: null);
    }

    public ScheduleEntryEditorViewModel EditEntry(ScheduleEntry entry) => new(Modes, entry, entry);

    /// <summary>
    /// Validates the editor and saves the plan with the new or changed entry.
    /// On failure the reason is in <see cref="ScheduleEntryEditorViewModel.ErrorMessage"/>.
    /// </summary>
    public async Task<bool> SaveEntryAsync(ScheduleEntryEditorViewModel editor)
    {
        var others = Entries.Where(e => !ReferenceEquals(e, editor.Original)).ToList();

        editor.ErrorMessage = !CanEdit ? Strings.ScheduleCannotChange
            : editor.IsNew && Entries.Count >= MaxEntries ? string.Format(Strings.ScheduleFull, MaxEntries)
            : editor.Validate(others);
        if (editor.ErrorMessage is not null)
            return false;

        others.Add(editor.ToEntry());
        var saved = await SavePlanAsync(others);
        if (!saved)
            editor.ErrorMessage = ErrorMessage;

        return saved;
    }

    public async Task<bool> DeleteEntryAsync(ScheduleEntry entry)
    {
        if (!CanEdit)
            return false;

        return await SavePlanAsync(Entries.Where(e => !ReferenceEquals(e, entry)).ToList());
    }

    private async Task<bool> SavePlanAsync(List<ScheduleEntry> entries)
    {
        entries.Sort((a, b) => a.Start.CompareTo(b.Start));
        var plan = SchedulePlan.Format(entries);

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            _logger.LogInformation("Saving schedule plan with {Count} entries", entries.Count);
            if (await _apiService.SetSchedulePlanAsync(plan))
            {
                Entries = entries;
                return true;
            }

            ErrorMessage = Strings.ScheduleSaveFailed;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving the schedule failed");
            ErrorMessage = Strings.ScheduleSaveFailed;
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
