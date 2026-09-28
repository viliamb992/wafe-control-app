using System.ComponentModel;

namespace WafeControl.Core.ViewModels;

/// <summary>
/// Minimal surface exposed to card ViewModels, decoupling them from the concrete AppViewModel.
/// </summary>
public interface IAppContext : INotifyPropertyChanged
{
    bool IsAuthenticated { get; }
    bool IsManualMode { get; }

    /// <summary>
    /// Signed in and the server reachable: a command can get through.
    /// </summary>
    bool CanSendCommands { get; }

    /// <summary>
    /// The result of the last user action; set from the UI thread.
    /// </summary>
    Feedback? Feedback { get; set; }
}
