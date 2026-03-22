using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Shared;
using Serilog;

namespace RecuperationSystem.Desktop.ViewModels.Cards;

public class FlowSpeedCardViewModel : ReactiveObject, IDisposable
{
    private readonly ISystemControlService _systemControl;
    private readonly IAppContext _app;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    private const int DebounceDelayMs = 800;

    private int _flowSpeed = 50;
    private bool _isChanging;
    private bool _isDragging;

    public FlowSpeedCardViewModel(ISystemControlService systemControl, IAppContext app)
    {
        _systemControl = systemControl;
        _app = app;

        _systemControl.StatusUpdated += OnStatusUpdated;

        UpdateFlowSpeedCommand = ReactiveCommand.CreateFromTask(
            UpdateFlowSpeedAsync,
            this.WhenAnyValue(
                x => x._app.IsAuthenticated,
                x => x.IsChanging,
                x => x._app.IsManualMode,
                (auth, changing, manual) => auth && !changing && manual));
    }

    public int FlowSpeed
    {
        get => _flowSpeed;
        set
        {
            var clampedValue = Math.Clamp(value, AppConstants.MinFlowSpeed, AppConstants.MaxFlowSpeed);
            this.RaiseAndSetIfChanged(ref _flowSpeed, clampedValue);
            this.RaisePropertyChanged(nameof(FlowSpeedText));
            OnFlowSpeedChanged(clampedValue);
        }
    }

    public bool IsChanging
    {
        get => _isChanging;
        set => this.RaiseAndSetIfChanged(ref _isChanging, value);
    }

    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDragging, value);
            if (!value && _app.IsManualMode && _app.IsAuthenticated)
            {
                // When drag completes, trigger update
                _ = UpdateFlowSpeedAsync();
            }
        }
    }

    public string FlowSpeedText => $"{FlowSpeed} m³/h";

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> UpdateFlowSpeedCommand { get; }

    private void OnFlowSpeedChanged(int value)
    {
        if (IsDragging && _app.IsManualMode && _app.IsAuthenticated)
        {
            // Cancel any pending debounce
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();

            // Debounce the update
            _ = DebounceFlowSpeedUpdateAsync(_debounceCts.Token);
        }
    }

    private async Task DebounceFlowSpeedUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceDelayMs, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                await UpdateFlowSpeedAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when debounce is cancelled
        }
    }

    private async Task UpdateFlowSpeedAsync()
    {
        if (!_app.IsManualMode)
        {
            Log.Warning("UpdateFlowSpeedAsync aborted - not in Manual mode");
            _app.StatusMessage = "Flow speed can only be changed in Manual mode";
            return;
        }

        try
        {
            _app.StatusMessage = "Updating flow speed...";
            IsChanging = true;

            var targetSpeed = FlowSpeed;
            Log.Information("Setting flow speed to {Speed}", targetSpeed);

            var confirmed = await _systemControl.SetFlowSpeedAsync(targetSpeed);

            if (confirmed)
            {
                _app.StatusMessage = $"Flow speed set to {targetSpeed} m³/h";
            }
            else
            {
                _app.StatusMessage = $"Flow speed command sent to {targetSpeed} m³/h (not confirmed)";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating flow speed");
            _app.StatusMessage = $"Flow speed error: {ex.Message}";
        }
        finally
        {
            IsChanging = false;
        }
    }

    private void OnStatusUpdated(object? sender, Shared.Models.SystemStatus status)
    {
        if (!IsDragging && !IsChanging)
        {
            FlowSpeed = status.FlowRequested;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _systemControl.StatusUpdated -= OnStatusUpdated;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();

        _disposed = true;
    }
}
