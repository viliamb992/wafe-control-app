using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Shared;
using WafeControl.Shared.Models;

namespace WafeControl.Core.ViewModels.Cards;

public sealed partial class OperatingModeCardViewModel : CardViewModelBase
{
    private static readonly IReadOnlyList<string> DefaultModes =
        [AppConstants.ModeIntelligent, AppConstants.ModeManual, AppConstants.ModeSchedule];

    public OperatingModeCardViewModel(ISystemControlService systemControl, IAppContext app, ILogger<OperatingModeCardViewModel> logger)
        : base(systemControl, app, logger)
    {
        SelectedMode = AppConstants.ModeIntelligent;
        AvailableModes = DefaultModes;
    }

    /// <summary>
    /// The mode shown as selected: the unit's, or the requested one while a change is on its way.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualMode))]
    [NotifyPropertyChangedFor(nameof(IsIntelligentMode))]
    [NotifyPropertyChangedFor(nameof(IsScheduleMode))]
    public partial string SelectedMode { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateModeCommand))]
    public partial bool IsModeChanging { get; set; }

    /// <summary>
    /// Modes the unit accepts (API "authority-available"); defaults until the first status arrives.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> AvailableModes { get; private set; }

    public bool IsManualMode => SelectedMode == AppConstants.ModeManual;
    public bool IsIntelligentMode => SelectedMode == AppConstants.ModeIntelligent;
    public bool IsScheduleMode => SelectedMode == AppConstants.ModeSchedule;

    /// <summary>
    /// Sends <see cref="SelectedMode"/>. It stays selected while the unit confirms, and goes back to the unit's mode
    /// if the change fails.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateMode))]
    private async Task UpdateModeAsync()
    {
        var targetMode = SelectedMode;
        IsModeChanging = true;
        try
        {
            var outcome = await RunCommandAsync(
                () => SystemControl.SetAuthorityModeAsync(targetMode),
                new CommandTexts(Strings.ModeChanging, string.Format(Strings.ModeChanged, ModeNames.Operating(targetMode)), Strings.ModeError),
                () => ChangeModeAsync(targetMode));

            if (outcome.Status == CommandStatus.Failed && SystemControl.CurrentStatus is { } status)
                SelectedMode = status.Authority;
        }
        finally
        {
            IsModeChanging = false;
        }
    }

    /// <summary>
    /// Selects <paramref name="mode"/> and sends it.
    /// </summary>
    public async Task ChangeModeAsync(string mode)
    {
        if (!UpdateModeCommand.CanExecute(null))
            return;

        SelectedMode = mode;
        await UpdateModeCommand.ExecuteAsync(null);
    }

    private bool CanUpdateMode() => App.CanSendCommands && !IsModeChanging;

    protected override void OnStatusUpdated(SystemStatus status)
    {
        if (!IsModeChanging)
            SelectedMode = status.Authority;

        if (status.AuthorityAvailable is { Count: > 0 } modes && !modes.SequenceEqual(AvailableModes))
            AvailableModes = modes.ToArray();
    }

    protected override void NotifyCommandsCanExecuteChanged() => UpdateModeCommand.NotifyCanExecuteChanged();
}
