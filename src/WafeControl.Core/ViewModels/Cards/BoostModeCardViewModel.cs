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
        if (!int.TryParse(secondsString, out var seconds))
        {
            App.Feedback = Feedback.Error(Strings.BoostInvalidDuration);
            return;
        }

        var minutes = seconds / 60;
        var texts = seconds > 0
            ? new CommandTexts(string.Format(Strings.BoostActivating, minutes), string.Format(Strings.BoostActivated, minutes),
                Strings.BoostNotConfirmed, Strings.BoostError)
            : new CommandTexts(Strings.BoostStopping, Strings.BoostStopped, Strings.BoostNotConfirmed, Strings.BoostError);

        await RunCommandAsync(
            onSent => SystemControl.SetBoostAsync(seconds, onSent),
            texts,
            () => SetBoostCommand.ExecuteAsync(secondsString));
    }

    private bool CanSetBoost() => App.CanSendCommands;

    protected override void OnStatusUpdated(SystemStatus status) => BoostRemaining = status.BoostRemaining;

    protected override void NotifyCommandsCanExecuteChanged() => SetBoostCommand.NotifyCanExecuteChanged();
}
