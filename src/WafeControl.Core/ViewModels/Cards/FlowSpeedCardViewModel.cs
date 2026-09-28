using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.Threading;
using WafeControl.Shared;
using WafeControl.Shared.Models;

namespace WafeControl.Core.ViewModels.Cards;

public sealed partial class FlowSpeedCardViewModel : CardViewModelBase
{
    private const int DebounceDelayMs = 800;

    private CancellationTokenSource? _debounceCts;
    private int _flowSpeed = AppConstants.MinFlowSpeed;
    private bool _isDragging;

    public FlowSpeedCardViewModel(ISystemControlService systemControl, IAppContext app, ILogger<FlowSpeedCardViewModel> logger)
        : base(systemControl, app, logger)
    {
    }

    public int FlowSpeed
    {
        get => _flowSpeed;
        set
        {
            if (SetProperty(ref _flowSpeed, Math.Clamp(value, AppConstants.MinFlowSpeed, AppConstants.MaxFlowSpeed)))
            {
                OnPropertyChanged(nameof(FlowSpeedText));
                if (IsDragging && CanSendFlowSpeed)
                    ScheduleDebouncedUpdate();
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateFlowSpeedCommand))]
    public partial bool IsChanging { get; set; }

    /// <summary>
    /// Set by the view while the user drags the slider. Values are sent debounced while dragging
    /// and once more when the drag ends.
    /// </summary>
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (SetProperty(ref _isDragging, value) && !value && CanSendFlowSpeed)
            {
                CancelDebounce();
                UpdateFlowSpeedAsync().Forget(Logger, "Flow speed update");
            }
        }
    }

    public string FlowSpeedText => $"{FlowSpeed} m³/h";

    private bool CanSendFlowSpeed => App.IsManualMode && App.CanSendCommands;

    [RelayCommand(CanExecute = nameof(CanUpdateFlowSpeed))]
    private async Task UpdateFlowSpeedAsync()
    {
        if (!App.IsManualMode)
        {
            Logger.LogWarning("UpdateFlowSpeedAsync aborted - not in Manual mode");
            App.Feedback = Feedback.Warning(Strings.FlowManualOnly);
            return;
        }

        var targetSpeed = FlowSpeed;
        IsChanging = true;
        try
        {
            var outcome = await RunCommandAsync(
                () => SystemControl.SetFlowSpeedAsync(targetSpeed),
                new CommandTexts(Strings.FlowUpdating, string.Format(Strings.FlowSet, targetSpeed), Strings.FlowError),
                () => SendFlowSpeedAsync(targetSpeed));

            // Back to what the unit has.
            if (outcome.Status == CommandStatus.Failed && SystemControl.CurrentStatus is { } status)
                FlowSpeed = status.FlowRequested;
        }
        finally
        {
            IsChanging = false;
        }
    }

    private async Task SendFlowSpeedAsync(int speed)
    {
        if (!UpdateFlowSpeedCommand.CanExecute(null))
            return;

        FlowSpeed = speed;
        await UpdateFlowSpeedCommand.ExecuteAsync(null);
    }

    private bool CanUpdateFlowSpeed() => App.CanSendCommands && App.IsManualMode && !IsChanging;

    private void ScheduleDebouncedUpdate()
    {
        CancelDebounce();
        _debounceCts = new CancellationTokenSource();
        DebounceFlowSpeedUpdateAsync(_debounceCts.Token).Forget(Logger, "Flow speed update");
    }

    private async Task DebounceFlowSpeedUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceDelayMs, cancellationToken);
            await UpdateFlowSpeedAsync();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer value or by the end of the drag.
        }
    }

    private void CancelDebounce()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    protected override void OnStatusUpdated(SystemStatus status)
    {
        if (!IsDragging && !IsChanging)
            FlowSpeed = status.FlowRequested;
    }

    protected override void NotifyCommandsCanExecuteChanged() => UpdateFlowSpeedCommand.NotifyCanExecuteChanged();

    protected override void OnDisposing() => CancelDebounce();
}
