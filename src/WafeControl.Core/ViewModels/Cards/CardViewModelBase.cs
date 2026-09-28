using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.Threading;
using WafeControl.Shared.Models;
using WafeControl.Shared.Services;

namespace WafeControl.Core.ViewModels.Cards;

/// <summary>
/// What a command says at each step: while sending, when confirmed, when the unit hasn't confirmed yet, and when it
/// failed (a format with the reason as {0}).
/// </summary>
public sealed record CommandTexts(string Sending, string Confirmed, string Pending, string FailedFormat);

/// <summary>
/// Common plumbing for dashboard cards: status updates in, command availability refreshed
/// when app-level state (authentication, connection, manual mode) changes, and the feedback for commands.
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

    /// <summary>
    /// Sends a command and reports each step in <see cref="IAppContext.Feedback"/>: sending, waiting for the unit,
    /// then the result, with Retry when trying again can help. A command the unit confirms late is announced then.
    /// </summary>
    protected async Task<CommandOutcome> RunCommandAsync(Func<Action, Task<CommandOutcome>> send, CommandTexts texts, Func<Task> retry)
    {
        App.Feedback = Feedback.Progress(texts.Sending);

        CommandOutcome outcome;
        try
        {
            outcome = await send(() => App.Feedback = Feedback.Progress(Strings.CommandWaiting));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Command failed: {Command}", texts.Sending);
            App.Feedback = Feedback.Error(string.Format(texts.FailedFormat, Strings.ErrorUnexpected), offersReport: true);
            return CommandOutcome.Failed(ApiError.Unexpected);
        }

        switch (outcome.Status)
        {
            case CommandStatus.Confirmed:
                App.Feedback = Feedback.Success(texts.Confirmed);
                break;

            case CommandStatus.Pending:
                var pending = Feedback.Warning(texts.Pending, retry);
                App.Feedback = pending;
                ShowLateConfirmationAsync(outcome.LateConfirmation, pending, texts.Confirmed).Forget(Logger, "Late confirmation");
                break;

            default:
                App.Feedback = Feedback.Error(
                    string.Format(texts.FailedFormat, ErrorText.For(outcome.Error)),
                    ErrorText.IsRetryable(outcome.Error) ? retry : null,
                    offersReport: outcome.Error is ApiError.Unexpected or ApiError.InvalidResponse);
                break;
        }

        return outcome;
    }

    // Announced unless something newer is shown by then.
    private async Task ShowLateConfirmationAsync(Task<bool> lateConfirmation, Feedback pending, string confirmedText)
    {
        if (await lateConfirmation && (App.Feedback is null || ReferenceEquals(App.Feedback, pending)))
            App.Feedback = Feedback.Success(string.Format(Strings.CommandConfirmedLate, confirmedText));
    }

    private void OnSystemStatusUpdated(object? sender, SystemStatus status) => OnStatusUpdated(status);

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IAppContext.IsAuthenticated) or nameof(IAppContext.IsManualMode) or nameof(IAppContext.CanSendCommands))
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
