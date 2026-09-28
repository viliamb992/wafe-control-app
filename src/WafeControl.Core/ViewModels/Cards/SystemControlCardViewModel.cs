using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Shared.Models;

namespace WafeControl.Core.ViewModels.Cards;

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

    /// <summary>
    /// Stops a running unit, starts a stopped one. The views ask before stopping.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleSystem))]
    private Task ToggleSystemAsync() => SetRunningAsync(!IsSystemRunning);

    private async Task SetRunningAsync(bool start)
    {
        if (!CanToggleSystem())
            return;

        var texts = start
            ? new CommandTexts(Strings.SystemStarting, Strings.SystemStartedStatus, Strings.SystemToggleError)
            : new CommandTexts(Strings.SystemStopping, Strings.SystemStoppedStatus, Strings.SystemToggleError);

        await RunCommandAsync(
            () => start ? SystemControl.StartSystemAsync() : SystemControl.StopSystemAsync(),
            texts,
            () => SetRunningAsync(start));
    }

    private bool CanToggleSystem() => App.CanSendCommands && !IsOperationInProgress;

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
