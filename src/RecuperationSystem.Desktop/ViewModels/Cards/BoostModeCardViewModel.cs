using System;
using System.Threading.Tasks;
using ReactiveUI;
using RecuperationSystem.Desktop.Services;
using Serilog;

namespace RecuperationSystem.Desktop.ViewModels.Cards;

public class BoostModeCardViewModel : ReactiveObject, IDisposable
{
    private readonly ISystemControlService _systemControl;
    private readonly AppViewModel _app;
    private bool _disposed;

    private int _boostRemaining;
    private bool _isBoostActive;

    public BoostModeCardViewModel(ISystemControlService systemControl, AppViewModel app)
    {
        _systemControl = systemControl;
        _app = app;

        _systemControl.StatusUpdated += OnStatusUpdated;

        SetBoostCommand = ReactiveCommand.CreateFromTask<object>(
            SetBoostAsync,
            this.WhenAnyValue(x => x._app.IsAuthenticated));
    }

    public int BoostRemaining
    {
        get => _boostRemaining;
        private set
        {
            this.RaiseAndSetIfChanged(ref _boostRemaining, value);
            this.RaisePropertyChanged(nameof(IsBoostActive));
            this.RaisePropertyChanged(nameof(BoostRemainingText));
        }
    }

    public bool IsBoostActive
    {
        get => _boostRemaining > 0;
    }

    public string BoostRemainingText
    {
        get
        {
            if (BoostRemaining <= 0) return "Off";
            var minutes = BoostRemaining / 60;
            var seconds = BoostRemaining % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }
    }

    public ReactiveCommand<object, System.Reactive.Unit> SetBoostCommand { get; }

    private async Task SetBoostAsync(object secondsObject)
    {
        try
        {
            int seconds;
            if (secondsObject is string secondsString && int.TryParse(secondsString, out var parsedSeconds))
            {
                seconds = parsedSeconds;
            }
            else if (secondsObject is int secondsInt)
            {
                seconds = secondsInt;
            }
            else
            {
                _app.StatusMessage = "Invalid boost duration";
                return;
            }

            var minutes = seconds / 60;
            _app.StatusMessage = seconds > 0 ? $"Activating boost for {minutes} minutes..." : "Stopping boost...";

            var confirmed = await _systemControl.SetBoostAsync(seconds);

            if (confirmed)
            {
                _app.StatusMessage = seconds > 0 ? $"Boost activated for {minutes} minutes" : "Boost stopped";
            }
            else
            {
                _app.StatusMessage = "Boost command sent (not confirmed)";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting boost");
            _app.StatusMessage = $"Boost error: {ex.Message}";
        }
    }

    private void OnStatusUpdated(object? sender, Shared.Models.SystemStatus status)
    {
        BoostRemaining = status.BoostRemaining;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _systemControl.StatusUpdated -= OnStatusUpdated;

        _disposed = true;
    }
}
