namespace RecuperationSystem.Core.Configuration;

/// <summary>
/// Configuration for polling intervals throughout the application
/// </summary>
public class PollingConfiguration
{
    /// <summary>
    /// Interval in milliseconds for auto-refreshing system status in the background
    /// Default: 3000ms (3 seconds)
    /// </summary>
    public int StatusRefreshIntervalMs { get; set; } = 3000;

    /// <summary>
    /// Interval in milliseconds for polling state changes after API commands
    /// Default: 1000ms (1 second)
    /// </summary>
    public int StateChangeIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Maximum duration in seconds to wait for state change confirmation
    /// Default: 15 seconds
    /// </summary>
    public int StateChangeTimeoutSeconds { get; set; } = 15;
}
