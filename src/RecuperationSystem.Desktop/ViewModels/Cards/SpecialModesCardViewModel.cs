using System;
using System.Threading.Tasks;
using ReactiveUI;
using RecuperationSystem.Desktop.Services;
using Serilog;

namespace RecuperationSystem.Desktop.ViewModels.Cards;

public class SpecialModesCardViewModel : ReactiveObject, IDisposable
{
    private readonly ISystemControlService _systemControl;
    private readonly IAppContext _app;
    private bool _disposed;

    private bool _isSilentMode;
    private bool _isHolidayMode;
    private bool _isSilentModeChanging;
    private bool _isHolidayModeChanging;

    public SpecialModesCardViewModel(ISystemControlService systemControl, IAppContext app)
    {
        _systemControl = systemControl;
        _app = app;

        _systemControl.StatusUpdated += OnStatusUpdated;

        SetSilentModeCommand = ReactiveCommand.CreateFromTask<bool>(
            SetSilentModeAsync,
            this.WhenAnyValue(
                x => x._app.IsAuthenticated,
                x => x.IsSilentModeChanging,
                (auth, changing) => auth && !changing));

        SetHolidayModeCommand = ReactiveCommand.CreateFromTask<bool>(
            SetHolidayModeAsync,
            this.WhenAnyValue(
                x => x._app.IsAuthenticated,
                x => x.IsHolidayModeChanging,
                (auth, changing) => auth && !changing));
    }

    public bool IsSilentMode
    {
        get => _isSilentMode;
        set => this.RaiseAndSetIfChanged(ref _isSilentMode, value);
    }

    public bool IsHolidayMode
    {
        get => _isHolidayMode;
        set => this.RaiseAndSetIfChanged(ref _isHolidayMode, value);
    }

    public bool IsSilentModeChanging
    {
        get => _isSilentModeChanging;
        set => this.RaiseAndSetIfChanged(ref _isSilentModeChanging, value);
    }

    public bool IsHolidayModeChanging
    {
        get => _isHolidayModeChanging;
        set => this.RaiseAndSetIfChanged(ref _isHolidayModeChanging, value);
    }

    public ReactiveCommand<bool, System.Reactive.Unit> SetSilentModeCommand { get; }
    public ReactiveCommand<bool, System.Reactive.Unit> SetHolidayModeCommand { get; }

    private async Task SetSilentModeAsync(bool enabled)
    {
        try
        {
            _app.StatusMessage = enabled ? "Enabling silent mode..." : "Disabling silent mode...";
            IsSilentModeChanging = true;

            var confirmed = await _systemControl.SetSilentModeAsync(enabled);

            IsSilentMode = enabled;

            if (confirmed)
            {
                _app.StatusMessage = enabled ? "Silent mode ON" : "Silent mode OFF";
            }
            else
            {
                _app.StatusMessage = enabled ? "Silent mode command sent (not confirmed)" : "Silent mode off command sent (not confirmed)";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting silent mode");
            _app.StatusMessage = $"Silent mode error: {ex.Message}";
        }
        finally
        {
            IsSilentModeChanging = false;
        }
    }

    private async Task SetHolidayModeAsync(bool enabled)
    {
        try
        {
            _app.StatusMessage = enabled ? "Enabling holiday mode..." : "Disabling holiday mode...";
            IsHolidayModeChanging = true;

            var confirmed = await _systemControl.SetHolidayModeAsync(enabled);

            IsHolidayMode = enabled;

            if (confirmed)
            {
                _app.StatusMessage = enabled ? "Holiday mode ON" : "Holiday mode OFF";
            }
            else
            {
                _app.StatusMessage = enabled ? "Holiday mode command sent (not confirmed)" : "Holiday mode off command sent (not confirmed)";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting holiday mode");
            _app.StatusMessage = $"Holiday mode error: {ex.Message}";
        }
        finally
        {
            IsHolidayModeChanging = false;
        }
    }

    private void OnStatusUpdated(object? sender, Shared.Models.SystemStatus status)
    {
        if (!IsSilentModeChanging)
        {
            IsSilentMode = status.SilentActive;
        }

        if (!IsHolidayModeChanging)
        {
            IsHolidayMode = status.HolidayActive;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _systemControl.StatusUpdated -= OnStatusUpdated;

        _disposed = true;
    }
}
