using System;
using System.Threading.Tasks;
using ReactiveUI;
using RecuperationSystem.Desktop.Services;
using Serilog;

namespace RecuperationSystem.Desktop.ViewModels.Cards;

public class SystemControlCardViewModel : ReactiveObject, IDisposable
{
    private readonly ISystemControlService _systemControl;
    private readonly AppViewModel _app;
    private bool _disposed;

    private bool _isOperationInProgress;

    public SystemControlCardViewModel(ISystemControlService systemControl, AppViewModel app)
    {
        _systemControl = systemControl;
        _app = app;

        _systemControl.OperationInProgress += OnOperationInProgressChanged;
        _systemControl.StatusUpdated += OnStatusUpdated;

        ToggleSystemCommand = ReactiveCommand.CreateFromTask(
            ToggleSystemAsync,
            this.WhenAnyValue(
                x => x._app.IsAuthenticated,
                x => x.IsOperationInProgress,
                (auth, inProgress) => auth && !inProgress));
    }

    public bool IsOperationInProgress
    {
        get => _isOperationInProgress;
        set => this.RaiseAndSetIfChanged(ref _isOperationInProgress, value);
    }

    public bool IsSystemRunning => _systemControl.IsSystemRunning;
    public bool IsSystemStopped => _systemControl.IsSystemStopped;
    public bool IsSystemOnline => _systemControl.IsSystemOnline;
    public string SystemToggleButtonText => IsSystemRunning ? "Stop System" : "Start System";
    public string CurrentAuthority => _systemControl.CurrentStatus?.Authority ?? "unknown";

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ToggleSystemCommand { get; }

    private async Task ToggleSystemAsync()
    {
        try
        {
            if (IsSystemRunning)
            {
                _app.StatusMessage = "Stopping system...";
                await _systemControl.StopSystemAsync();
                _app.StatusMessage = "System stopped";
            }
            else
            {
                _app.StatusMessage = "Starting system...";
                await _systemControl.StartSystemAsync();
                _app.StatusMessage = "System started";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling system");
            _app.StatusMessage = $"System toggle error: {ex.Message}";
        }
    }

    private void OnOperationInProgressChanged(object? sender, bool inProgress)
    {
        IsOperationInProgress = inProgress;
    }

    private void OnStatusUpdated(object? sender, Shared.Models.SystemStatus status)
    {
        this.RaisePropertyChanged(nameof(IsSystemRunning));
        this.RaisePropertyChanged(nameof(IsSystemStopped));
        this.RaisePropertyChanged(nameof(IsSystemOnline));
        this.RaisePropertyChanged(nameof(SystemToggleButtonText));
        this.RaisePropertyChanged(nameof(CurrentAuthority));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _systemControl.OperationInProgress -= OnOperationInProgressChanged;
        _systemControl.StatusUpdated -= OnStatusUpdated;

        _disposed = true;
    }
}
