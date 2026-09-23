using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Core.ViewModels.Cards;

/// <summary>
/// Common plumbing for dashboard cards: status updates in, command availability refreshed
/// when app-level state (authentication, manual mode) changes.
/// </summary>
public abstract class CardViewModelBase : ObservableObject, IDisposable
{
    private bool _disposed;

    protected CardViewModelBase(ISystemControlService systemControl, IAppContext app, ILogger logger)
    {
        SystemControl = systemControl;
        App = app;
        Logger = logger;

        SystemControl.StatusUpdated += OnSystemStatusUpdated;
        App.PropertyChanged += OnAppPropertyChanged;
    }

    protected ISystemControlService SystemControl { get; }
    protected IAppContext App { get; }
    protected ILogger Logger { get; }

    protected abstract void OnStatusUpdated(SystemStatus status);

    /// <summary>
    /// Raise CanExecuteChanged on the card's commands.
    /// </summary>
    protected abstract void NotifyCommandsCanExecuteChanged();

    protected virtual void OnDisposing()
    {
    }

    private void OnSystemStatusUpdated(object? sender, SystemStatus status) => OnStatusUpdated(status);

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IAppContext.IsAuthenticated) or nameof(IAppContext.IsManualMode))
            NotifyCommandsCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;

        SystemControl.StatusUpdated -= OnSystemStatusUpdated;
        App.PropertyChanged -= OnAppPropertyChanged;
        OnDisposing();

        _disposed = true;
    }
}
