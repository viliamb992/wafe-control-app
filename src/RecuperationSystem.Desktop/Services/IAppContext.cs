using System.ComponentModel;

namespace RecuperationSystem.Desktop.Services;

/// <summary>
/// Minimal surface exposed to card ViewModels, decoupling them from the concrete AppViewModel.
/// </summary>
public interface IAppContext : INotifyPropertyChanged
{
    bool IsAuthenticated { get; }
    bool IsManualMode { get; }
    string StatusMessage { get; set; }
}
