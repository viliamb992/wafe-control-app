using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Shared.Models;

namespace WafeControl.Core.ViewModels.Cards;

public sealed partial class BoostModeCardViewModel : CardViewModelBase
{
    public BoostModeCardViewModel(ISystemControlService systemControl, IAppContext app, ILogger<BoostModeCardViewModel> logger)
        : base(systemControl, app, logger)
    {
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBoostActive))]
    [NotifyPropertyChangedFor(nameof(BoostRemainingText))]
    public partial int BoostRemaining { get; private set; }

    public bool IsBoostActive => BoostRemaining > 0;

    public string BoostRemainingText => DisplayFormat.Countdown(BoostRemaining);

    /// <summary>
    /// Parameter: boost duration in seconds as a string (XAML CommandParameter); "0" stops the boost.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSetBoost))]
    private async Task SetBoostAsync(string? secondsString)
    {
        try
        {
            if (!int.TryParse(secondsString, out var seconds))
            {
                App.StatusMessage = Strings.BoostInvalidDuration;
                return;
            }

            var minutes = seconds / 60;
            App.StatusMessage = seconds > 0 ? string.Format(Strings.BoostActivating, minutes) : Strings.BoostStopping;

            var confirmed = await SystemControl.SetBoostAsync(seconds);

            if (confirmed)
                App.StatusMessage = seconds > 0 ? string.Format(Strings.BoostActivated, minutes) : Strings.BoostStopped;
            else
                App.StatusMessage = Strings.BoostNotConfirmed;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error setting boost");
            App.StatusMessage = string.Format(Strings.BoostError, ex.Message);
        }
    }

    private bool CanSetBoost() => App.IsAuthenticated;

    protected override void OnStatusUpdated(SystemStatus status) => BoostRemaining = status.BoostRemaining;

    protected override void NotifyCommandsCanExecuteChanged() => SetBoostCommand.NotifyCanExecuteChanged();
}
