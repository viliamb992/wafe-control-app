using CommunityToolkit.Mvvm.Input;

namespace WafeControl.Core.ViewModels;

public enum FeedbackKind
{
    /// <summary>
    /// A command is on its way ("Activating boost…").
    /// </summary>
    Progress,
    Success,

    /// <summary>
    /// Not an error, but not done either (e.g. the unit hasn't confirmed yet).
    /// </summary>
    Warning,
    Error,
}

/// <summary>
/// The result of something the user did, shown briefly: the footer on Windows, a toast on mobile.
/// A class, not a record: showing the same text twice must still count as a change.
/// </summary>
public sealed class Feedback(string text, FeedbackKind kind)
{
    public string Text { get; } = text;

    public FeedbackKind Kind { get; } = kind;

    /// <summary>
    /// Sends the same command again; null when retrying can't help.
    /// </summary>
    public IAsyncRelayCommand? Retry { get; init; }

    /// <summary>
    /// Something unexpected went wrong: offer "Report a problem".
    /// </summary>
    public bool OffersReport { get; init; }

    public bool HasAction => Retry is not null || OffersReport;

    public static Feedback Progress(string text) => new(text, FeedbackKind.Progress);

    public static Feedback Success(string text) => new(text, FeedbackKind.Success);

    public static Feedback Warning(string text, Func<Task>? retry = null) =>
        new(text, FeedbackKind.Warning) { Retry = retry is null ? null : new AsyncRelayCommand(retry) };

    public static Feedback Error(string text, Func<Task>? retry = null, bool offersReport = false) =>
        new(text, FeedbackKind.Error) { Retry = retry is null ? null : new AsyncRelayCommand(retry), OffersReport = offersReport };
}
