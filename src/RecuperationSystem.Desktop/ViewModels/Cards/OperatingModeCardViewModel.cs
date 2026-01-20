using System;
using System.Threading.Tasks;
using ReactiveUI;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Shared;
using Serilog;

namespace RecuperationSystem.Desktop.ViewModels.Cards;

public class OperatingModeCardViewModel : ReactiveObject, IDisposable
{
    private readonly ISystemControlService _systemControl;
    private readonly AppViewModel _app;
    private bool _disposed;

    private string _selectedMode = AppConstants.ModeIntelligent;
    private bool _isModeChanging;

    public OperatingModeCardViewModel(ISystemControlService systemControl, AppViewModel app)
    {
        _systemControl = systemControl;
        _app = app;

        _systemControl.StatusUpdated += OnStatusUpdated;

        UpdateModeCommand = ReactiveCommand.CreateFromTask(
            UpdateModeAsync,
            this.WhenAnyValue(
                x => x._app.IsAuthenticated,
                x => x.IsModeChanging,
                (auth, changing) => auth && !changing));
    }

    public string SelectedMode
    {
        get => _selectedMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedMode, value);
            this.RaisePropertyChanged(nameof(IsManualMode));
            this.RaisePropertyChanged(nameof(IsIntelligentMode));
            this.RaisePropertyChanged(nameof(IsScheduleMode));
        }
    }

    public bool IsModeChanging
    {
        get => _isModeChanging;
        set => this.RaiseAndSetIfChanged(ref _isModeChanging, value);
    }

    public bool IsManualMode => _selectedMode == AppConstants.ModeManual;
    public bool IsIntelligentMode => _selectedMode == AppConstants.ModeIntelligent;
    public bool IsScheduleMode => _selectedMode == AppConstants.ModeSchedule;

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> UpdateModeCommand { get; }

    private async Task UpdateModeAsync()
    {
        try
        {
            _app.StatusMessage = "Changing mode...";
            IsModeChanging = true;

            var targetMode = SelectedMode;
            var confirmed = await _systemControl.SetAuthorityModeAsync(targetMode);

            if (confirmed)
            {
                _app.StatusMessage = $"Mode changed to {SelectedMode}";
            }
            else
            {
                _app.StatusMessage = "Mode change timed out. Please verify status.";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating mode");
            _app.StatusMessage = $"Mode error: {ex.Message}";
        }
        finally
        {
            IsModeChanging = false;
        }
    }

    private void OnStatusUpdated(object? sender, Shared.Models.SystemStatus status)
    {
        SelectedMode = status.Authority;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _systemControl.StatusUpdated -= OnStatusUpdated;

        _disposed = true;
    }
}
