using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Core.ViewModels.Cards;

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
        try
        {
            App.StatusMessage = enabled ? Strings.SilentEnabling : Strings.SilentDisabling;
            IsSilentModeChanging = true;

            var confirmed = await SystemControl.SetSilentModeAsync(enabled);

            IsSilentMode = enabled;
            App.StatusMessage = (enabled, confirmed) switch
            {
                (true, true) => Strings.SilentOn,
                (false, true) => Strings.SilentOff,
                (true, false) => Strings.SilentOnNotConfirmed,
                (false, false) => Strings.SilentOffNotConfirmed,
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error setting silent mode");
            App.StatusMessage = string.Format(Strings.SilentError, ex.Message);
        }
        finally
        {
            IsSilentModeChanging = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSetHolidayMode))]
    private async Task SetHolidayModeAsync(bool enabled)
    {
        try
        {
            App.StatusMessage = enabled ? Strings.HolidayEnabling : Strings.HolidayDisabling;
            IsHolidayModeChanging = true;

            var confirmed = await SystemControl.SetHolidayModeAsync(enabled);

            IsHolidayMode = enabled;
            App.StatusMessage = (enabled, confirmed) switch
            {
                (true, true) => Strings.HolidayOn,
                (false, true) => Strings.HolidayOff,
                (true, false) => Strings.HolidayOnNotConfirmed,
                (false, false) => Strings.HolidayOffNotConfirmed,
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error setting holiday mode");
            App.StatusMessage = string.Format(Strings.HolidayError, ex.Message);
        }
        finally
        {
            IsHolidayModeChanging = false;
        }
    }

    private bool CanSetSilentMode() => App.IsAuthenticated && !IsSilentModeChanging;

    private bool CanSetHolidayMode() => App.IsAuthenticated && !IsHolidayModeChanging;

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
