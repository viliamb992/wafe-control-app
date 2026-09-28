namespace WafeControl.Core.Configuration;

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
    /// Shorter intervals for the first polls after a command, before <see cref="StateChangeIntervalMs"/> applies:
    /// a unit that reacts quickly is confirmed sooner. Empty: always <see cref="StateChangeIntervalMs"/>.
    /// </summary>
    public int[] StateChangeInitialIntervalsMs { get; set; } = [];

    /// <summary>
    /// Maximum duration in seconds to wait for state change confirmation
    /// Default: 15 seconds
    /// </summary>
    public int StateChangeTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// After a command times out, how long the regular status updates are still watched for the requested value.
    /// </summary>
    public int LateConfirmationSeconds { get; set; } = 120;

    /// <summary>
    /// Failed refreshes in a row after which the server counts as unreachable.
    /// </summary>
    public int UnreachableAfterFailures { get; set; } = 3;

    /// <summary>
    /// Refresh intervals while the server is unreachable, one per further failure; the last one repeats.
    /// </summary>
    public int[] UnreachableBackoffMs { get; set; } = [10_000, 30_000, 60_000];

    /// <summary>
    /// The unit's data counts as out of date when its timestamp is older than this.
    /// </summary>
    public int StaleAfterSeconds { get; set; } = 180;
}
