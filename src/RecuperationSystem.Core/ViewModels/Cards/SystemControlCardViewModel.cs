using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Core.ViewModels.Cards;

public sealed partial class SystemControlCardViewModel : CardViewModelBase
{
    public SystemControlCardViewModel(ISystemControlService systemControl, IAppContext app, ILogger<SystemControlCardViewModel> logger)
        : base(systemControl, app, logger)
    {
        SystemControl.OperationInProgress += OnOperationInProgressChanged;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleSystemCommand))]
    public partial bool IsOperationInProgress { get; set; }

    public bool IsSystemRunning => SystemControl.IsSystemRunning;
    public bool IsSystemStopped => SystemControl.IsSystemStopped;
    public bool IsSystemOnline => SystemControl.IsSystemOnline;
    public string SystemToggleButtonText => IsSystemRunning ? Strings.SystemStop : Strings.SystemStart;
    public string CurrentAuthority => SystemControl.CurrentStatus?.Authority ?? string.Empty;

    [RelayCommand(CanExecute = nameof(CanToggleSystem))]
    private async Task ToggleSystemAsync()
    {
        try
        {
            if (IsSystemRunning)
            {
                App.StatusMessage = Strings.SystemStopping;
                var confirmed = await SystemControl.StopSystemAsync();
                App.StatusMessage = confirmed ? Strings.SystemStoppedStatus : Strings.SystemStopNotConfirmed;
            }
            else
            {
                App.StatusMessage = Strings.SystemStarting;
                var confirmed = await SystemControl.StartSystemAsync();
                App.StatusMessage = confirmed ? Strings.SystemStartedStatus : Strings.SystemStartNotConfirmed;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling system");
            App.StatusMessage = string.Format(Strings.SystemToggleError, ex.Message);
        }
    }

    private bool CanToggleSystem() => App.IsAuthenticated && !IsOperationInProgress;

    private void OnOperationInProgressChanged(object? sender, bool inProgress) => IsOperationInProgress = inProgress;

    protected override void OnStatusUpdated(SystemStatus status)
    {
        OnPropertyChanged(nameof(IsSystemRunning));
        OnPropertyChanged(nameof(IsSystemStopped));
        OnPropertyChanged(nameof(IsSystemOnline));
        OnPropertyChanged(nameof(SystemToggleButtonText));
        OnPropertyChanged(nameof(CurrentAuthority));
    }

    protected override void NotifyCommandsCanExecuteChanged() => ToggleSystemCommand.NotifyCanExecuteChanged();

    protected override void OnDisposing() => SystemControl.OperationInProgress -= OnOperationInProgressChanged;
}
