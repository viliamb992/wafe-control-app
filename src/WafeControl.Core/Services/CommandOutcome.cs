using WafeControl.Shared.Services;

namespace WafeControl.Core.Services;

public enum CommandStatus
{
    /// <summary>
    /// The unit reports the requested value.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The server accepted the command, but the unit hasn't reported the value within the timeout. It may still
    /// apply: <see cref="CommandOutcome.LateConfirmation"/> says whether it did.
    /// </summary>
    Pending,

    /// <summary>
    /// The command didn't reach the unit; <see cref="CommandOutcome.Error"/> says why.
    /// </summary>
    Failed,
}

/// <summary>
/// Result of a command sent to the unit (start/stop, mode, flow, boost, silent, holiday).
/// </summary>
public sealed record CommandOutcome(CommandStatus Status, ApiError Error = ApiError.None)
{
    public static CommandOutcome Confirmed { get; } = new(CommandStatus.Confirmed);

    public static CommandOutcome Failed(ApiError error) => new(CommandStatus.Failed, error);

    public static CommandOutcome Pending(Task<bool> lateConfirmation) =>
        new(CommandStatus.Pending) { LateConfirmation = lateConfirmation };

    /// <summary>
    /// For <see cref="CommandStatus.Pending"/>: completes with true when the unit reports the value later on,
    /// false when it still hasn't after <see cref="Configuration.PollingConfiguration.LateConfirmationSeconds"/>.
    /// </summary>
    public Task<bool> LateConfirmation { get; init; } = Task.FromResult(false);
}
