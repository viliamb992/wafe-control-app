using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
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
                _ = UpdateFlowSpeedAsync();
            }
        }
    }

    public string FlowSpeedText => $"{FlowSpeed} m³/h";

    private bool CanSendFlowSpeed => App.IsManualMode && App.IsAuthenticated;

    [RelayCommand(CanExecute = nameof(CanUpdateFlowSpeed))]
    private async Task UpdateFlowSpeedAsync()
    {
        if (!App.IsManualMode)
        {
            Logger.LogWarning("UpdateFlowSpeedAsync aborted - not in Manual mode");
            App.StatusMessage = Strings.FlowManualOnly;
            return;
        }

        try
        {
            App.StatusMessage = Strings.FlowUpdating;
            IsChanging = true;

            var targetSpeed = FlowSpeed;
            var confirmed = await SystemControl.SetFlowSpeedAsync(targetSpeed);

            App.StatusMessage = confirmed
                ? string.Format(Strings.FlowSet, targetSpeed)
                : string.Format(Strings.FlowNotConfirmed, targetSpeed);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating flow speed");
            App.StatusMessage = string.Format(Strings.FlowError, ex.Message);
        }
        finally
        {
            IsChanging = false;
        }
    }

    private bool CanUpdateFlowSpeed() => App.IsAuthenticated && App.IsManualMode && !IsChanging;

    private void ScheduleDebouncedUpdate()
    {
        CancelDebounce();
        _debounceCts = new CancellationTokenSource();
        _ = DebounceFlowSpeedUpdateAsync(_debounceCts.Token);
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
