using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Shared.Models;

namespace WafeControl.Core.ViewModels.Cards;

public sealed partial class SpecialModesCardViewModel : CardViewModelBase
{
    public SpecialModesCardViewModel(ISystemControlService systemControl, IAppContext app, ILogger<SpecialModesCardViewModel> logger)
        : base(systemControl, app, logger)
    {
    }

    [ObservableProperty]
    public partial bool IsSilentMode { get; set; }

    [ObservableProperty]
    public partial bool IsHolidayMode { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetSilentModeCommand))]
    public partial bool IsSilentModeChanging { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetHolidayModeCommand))]
    public partial bool IsHolidayModeChanging { get; set; }

    [RelayCommand(CanExecute = nameof(CanSetSilentMode))]
    private async Task SetSilentModeAsync(bool enabled)
    {
        IsSilentModeChanging = true;
        try
        {
            var outcome = await RunCommandAsync(
                () => SystemControl.SetSilentModeAsync(enabled),
                enabled
                    ? new CommandTexts(Strings.SilentEnabling, Strings.SilentOn, Strings.SilentError)
                    : new CommandTexts(Strings.SilentDisabling, Strings.SilentOff, Strings.SilentError),
                () => SetSilentModeCommand.ExecuteAsync(enabled));

            IsSilentMode = outcome.Status == CommandStatus.Failed ? SystemControl.CurrentStatus?.SilentActive ?? !enabled : enabled;

            // The switch was flipped by the user; after a failure it must follow the unit even if the value didn't change.
            OnPropertyChanged(nameof(IsSilentMode));
        }
        finally
        {
            IsSilentModeChanging = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSetHolidayMode))]
    private async Task SetHolidayModeAsync(bool enabled)
    {
        IsHolidayModeChanging = true;
        try
        {
            var outcome = await RunCommandAsync(
                () => SystemControl.SetHolidayModeAsync(enabled),
                enabled
                    ? new CommandTexts(Strings.HolidayEnabling, Strings.HolidayOn, Strings.HolidayError)
                    : new CommandTexts(Strings.HolidayDisabling, Strings.HolidayOff, Strings.HolidayError),
                () => SetHolidayModeCommand.ExecuteAsync(enabled));

            IsHolidayMode = outcome.Status == CommandStatus.Failed ? SystemControl.CurrentStatus?.HolidayActive ?? !enabled : enabled;
            OnPropertyChanged(nameof(IsHolidayMode));
        }
        finally
        {
            IsHolidayModeChanging = false;
        }
    }

    private bool CanSetSilentMode() => App.CanSendCommands && !IsSilentModeChanging;

    private bool CanSetHolidayMode() => App.CanSendCommands && !IsHolidayModeChanging;

    protected override void OnStatusUpdated(SystemStatus status)
    {
        if (!IsSilentModeChanging)
            IsSilentMode = status.SilentActive;

        if (!IsHolidayModeChanging)
            IsHolidayMode = status.HolidayActive;
    }

    protected override void NotifyCommandsCanExecuteChanged()
    {
        SetSilentModeCommand.NotifyCanExecuteChanged();
        SetHolidayModeCommand.NotifyCanExecuteChanged();
    }
}
