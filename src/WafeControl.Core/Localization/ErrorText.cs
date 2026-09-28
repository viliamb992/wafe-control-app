using WafeControl.Shared.Services;

namespace WafeControl.Core.Localization;

/// <summary>
/// What to tell the user about a failed call, in the current app language. Never the exception text.
/// </summary>
public static class ErrorText
{
    public static string For(ApiError error) => error switch
    {
        ApiError.Offline => Strings.ErrorOffline,
        ApiError.Timeout => Strings.ErrorTimeout,
        ApiError.Unauthorized => Strings.ErrorUnauthorized,
        ApiError.Rejected => Strings.ErrorRejected,
        ApiError.ServerError => Strings.ErrorServer,
        ApiError.InvalidResponse => Strings.ErrorInvalidResponse,
        _ => Strings.ErrorUnexpected,
    };

    /// <summary>
    /// Whether trying the same thing again can help (a network or server hiccup, not a refusal).
    /// </summary>
    public static bool IsRetryable(ApiError error) =>
        error is ApiError.Offline or ApiError.Timeout or ApiError.ServerError;
}
