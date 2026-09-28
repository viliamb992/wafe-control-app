namespace WafeControl.Shared.Services;

/// <summary>
/// Why a Wafe API call failed, in terms the UI can explain to the user.
/// </summary>
public enum ApiError
{
    None,

    /// <summary>
    /// No connection to the server: no network, DNS failure, connection refused.
    /// </summary>
    Offline,

    /// <summary>
    /// The server didn't answer in time.
    /// </summary>
    Timeout,

    /// <summary>
    /// Wrong credentials, or a session that could not be renewed.
    /// </summary>
    Unauthorized,

    /// <summary>
    /// The server refused the request (4xx other than 401/403).
    /// </summary>
    Rejected,

    /// <summary>
    /// The server failed (5xx) or is temporarily blocked by the circuit breaker.
    /// </summary>
    ServerError,

    /// <summary>
    /// The response could not be read; the API may have changed.
    /// </summary>
    InvalidResponse,

    /// <summary>
    /// Anything else: a bug.
    /// </summary>
    Unexpected,
}

/// <summary>
/// Outcome of an API call without a value (PUT, sign-in).
/// </summary>
public readonly record struct ApiResult(ApiError Error, int? StatusCode = null)
{
    public static ApiResult Success => default;

    public bool Ok => Error == ApiError.None;

    public static ApiResult Fail(ApiError error, int? statusCode = null) => new(error, statusCode);
}

/// <summary>
/// Outcome of an API call that returns a value (GET).
/// </summary>
public readonly record struct ApiResult<T>(T? Value, ApiError Error, int? StatusCode = null)
    where T : class
{
    public bool Ok => Error == ApiError.None && Value is not null;

    public static ApiResult<T> Fail(ApiError error, int? statusCode = null) => new(null, error, statusCode);

    /// <summary>
    /// A value is a success; null counts as an unreadable response.
    /// </summary>
    public static implicit operator ApiResult<T>(T? value) =>
        new(value, value is null ? ApiError.InvalidResponse : ApiError.None);
}
