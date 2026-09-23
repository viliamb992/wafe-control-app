namespace RecuperationSystem.Core.Services;

/// <summary>
/// Starts the app when the user signs in to the operating system. Each app registers its own implementation.
/// </summary>
public interface IStartupRegistration
{
    /// <summary>
    /// Whether this app is registered to start, read from the system each time.
    /// </summary>
    bool IsEnabled { get; }

    /// <returns>False if the system refused the change; the reason is logged.</returns>
    bool TrySetEnabled(bool enabled);
}
