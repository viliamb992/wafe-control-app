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

    [RelayCommand(CanExecute = nameof(CanUpdateMode))]
    private async Task UpdateModeAsync()
    {
        try
        {
            App.StatusMessage = Strings.ModeChanging;
            IsModeChanging = true;

            var targetMode = SelectedMode;
            var confirmed = await SystemControl.SetAuthorityModeAsync(targetMode);

            App.StatusMessage = confirmed
                ? string.Format(Strings.ModeChanged, ModeNames.Operating(targetMode))
                : Strings.ModeChangeTimedOut;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating mode");
            App.StatusMessage = string.Format(Strings.ModeError, ex.Message);
        }
        finally
        {
            IsModeChanging = false;
        }
    }

    private bool CanUpdateMode() => App.IsAuthenticated && !IsModeChanging;

    protected override void OnStatusUpdated(SystemStatus status)
    {
        if (!IsModeChanging)
            SelectedMode = status.Authority;

        if (status.AuthorityAvailable is { Count: > 0 } modes && !modes.SequenceEqual(AvailableModes))
            AvailableModes = modes.ToArray();
    }

    protected override void NotifyCommandsCanExecuteChanged() => UpdateModeCommand.NotifyCanExecuteChanged();
}
