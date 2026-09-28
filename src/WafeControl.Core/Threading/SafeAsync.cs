using System.Runtime.CompilerServices;
using Serilog;

namespace WafeControl.Core.Threading;

/// <summary>
/// Runs async work from a UI event handler (which has to return void) without crashing the app: an exception is
/// logged and reported through <see cref="UnhandledError"/> instead of ending the process.
/// <code>private void OnSaveClicked(object? sender, EventArgs e) => SafeAsync.Run(SaveAsync);</code>
/// </summary>
public static class SafeAsync
{
    /// <summary>
    /// Shows the user that something went wrong (e.g. an error message with "Report a problem"). Called on the
    /// thread the handler ran on; the exception is already logged.
    /// </summary>
    public static Action<Exception>? UnhandledError { get; set; }

#pragma warning disable VSTHRD100 // The one async void: every UI handler goes through here, and it can't throw.
    public static async void Run(Func<Task> action, [CallerMemberName] string? caller = null)
#pragma warning restore VSTHRD100
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Closed or superseded; nothing to report.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in {Handler}", caller);
            try
            {
                UnhandledError?.Invoke(ex);
            }
            catch (Exception reportError)
            {
                Log.Error(reportError, "Could not show the error from {Handler}", caller);
            }
        }
    }
}
