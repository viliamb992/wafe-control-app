using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace WafeControl.Shared.Services;

/// <summary>
/// Maps HTTP status codes and exceptions to <see cref="ApiError"/>.
/// </summary>
public static class ApiErrors
{
    public static ApiError FromStatus(HttpStatusCode status) => (int)status switch
    {
        401 or 403 => ApiError.Unauthorized,
        >= 500 => ApiError.ServerError,
        _ => ApiError.Rejected,
    };

    public static ApiError FromException(Exception exception) => exception switch
    {
        JsonException => ApiError.InvalidResponse,
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError } => ApiError.Offline,
        HttpRequestException { InnerException: SocketException or IOException } => ApiError.Offline,
        HttpRequestException => ApiError.ServerError,
        // HttpClient's own timeout (the caller's token was not cancelled).
        TaskCanceledException or TimeoutException => ApiError.Timeout,
        // Polly (Microsoft.Extensions.Http.Resilience), matched by name to keep this project free of Polly.
        _ when exception.GetType().Name == "TimeoutRejectedException" => ApiError.Timeout,
        _ when exception.GetType().Name == "BrokenCircuitException" => ApiError.ServerError,
        _ => ApiError.Unexpected,
    };

    /// <summary>
    /// True for failures that are part of normal operation (network, server): logged as warnings, not errors.
    /// </summary>
    public static bool IsExpected(ApiError error) =>
        error is ApiError.Offline or ApiError.Timeout or ApiError.Unauthorized or ApiError.Rejected or ApiError.ServerError;
}
