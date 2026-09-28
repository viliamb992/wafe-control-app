using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using WafeControl.Shared.Diagnostics;
using WafeControl.Shared.Models;
using WafeControl.Shared.Serialization;
using WafeControl.Shared.Services.Http;

namespace WafeControl.Shared.Services;

/// <summary>
/// Typed client for the Wafe REST API. Stateless: the Sandcastle-Key lives in <see cref="WafeSession"/>
/// and is attached to requests by <see cref="SandcastleAuthHandler"/>.
/// Network and server failures are expected (logged as warnings); an unreadable response or an unknown
/// exception is an error, because it points at an API change or a bug.
/// </summary>
public class WafeApiService : IWafeApiService
{
    /// <summary>
    /// Message template of the error logged when a response can't be read. Crash reports group these by endpoint
    /// ("the API changed"), so keep it stable.
    /// </summary>
    public const string InvalidResponseMessage = "API response from {Endpoint} could not be read";

    private readonly HttpClient _httpClient;
    private readonly WafeSession _session;
    private readonly ILogger<WafeApiService> _logger;

    public WafeApiService(HttpClient httpClient, WafeSession session, ILogger<WafeApiService> logger)
    {
        _httpClient = httpClient;
        _session = session;
        _logger = logger;
    }

    // ==================== AUTHENTICATION ====================

    public async Task<ApiResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("POST {Endpoint} for {Username}", AppConstants.AuthContextEndpoint, LogRedaction.Email(username));

            using var request = SandcastleAuth.CreateLoginRequest(username, password);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (SandcastleAuth.TryReadKey(response, out var key))
            {
                _session.Start(username, password, key);
                _logger.LogInformation("Sandcastle-Key received and stored");
                return ApiResult.Success;
            }

            _logger.LogWarning("Authentication failed with status {StatusCode}", (int)response.StatusCode);

            // A success without a key means the credentials weren't accepted either.
            var error = response.IsSuccessStatusCode ? ApiError.Unauthorized : ApiErrors.FromStatus(response.StatusCode);
            return ApiResult.Fail(error == ApiError.Rejected ? ApiError.Unauthorized : error, (int)response.StatusCode);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult.Fail(LogFailure(ex, "POST", AppConstants.AuthContextEndpoint));
        }
    }

    // ==================== GET ENDPOINTS ====================

    public Task<ApiResult<SystemStatus>> GetMainStatusAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.MainEndpoint, WafeJsonContext.Default.SystemStatus, cancellationToken);

    public Task<ApiResult<HeaderInfo>> GetHeaderInfoAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.HeaderEndpoint, WafeJsonContext.Default.HeaderInfo, cancellationToken);

    public Task<ApiResult<SystemInfo>> GetSystemInfoAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.InfoEndpoint, WafeJsonContext.Default.SystemInfo, cancellationToken);

    public Task<ApiResult<ScheduleResponse>> GetScheduleAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.ScheduleEndpoint, WafeJsonContext.Default.ScheduleResponse, cancellationToken);

    // ==================== CONTROL ENDPOINTS (PUT) ====================

    public Task<ApiResult> SetStopActiveAsync(bool stopActive, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.StopActiveEndpoint, stopActive, WafeJsonContext.Default.ValueRequestBoolean, cancellationToken);

    public Task<ApiResult> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.SilentActiveEndpoint, enabled, WafeJsonContext.Default.ValueRequestBoolean, cancellationToken);

    public Task<ApiResult> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.HolidayActiveEndpoint, enabled, WafeJsonContext.Default.ValueRequestBoolean, cancellationToken);

    public Task<ApiResult> SetBoostAsync(int seconds, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.BoostRemainingEndpoint, seconds, WafeJsonContext.Default.ValueRequestInt32, cancellationToken);

    public Task<ApiResult> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.AuthorityEndpoint, mode, WafeJsonContext.Default.ValueRequestString, cancellationToken);

    public Task<ApiResult> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default)
    {
        if (speed is < AppConstants.MinFlowSpeed or > AppConstants.MaxFlowSpeed)
        {
            _logger.LogWarning("Flow speed {Speed} out of range ({Min}-{Max})", speed, AppConstants.MinFlowSpeed, AppConstants.MaxFlowSpeed);
            return Task.FromResult(ApiResult.Fail(ApiError.Rejected));
        }

        return PutValueAsync(AppConstants.FlowRequestedEndpoint, speed, WafeJsonContext.Default.ValueRequestInt32, cancellationToken);
    }

    public Task<ApiResult> SetSchedulePlanAsync(string plan, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.SchedulePlanEndpoint, plan, WafeJsonContext.Default.ValueRequestString, cancellationToken);

    public Task<ApiResult> SetUnitNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > AppConstants.MaxUnitNameLength)
        {
            _logger.LogWarning("Unit name must be 1-{Max} characters, got {Length}", AppConstants.MaxUnitNameLength, name?.Length ?? 0);
            return Task.FromResult(ApiResult.Fail(ApiError.Rejected));
        }

        return PutValueAsync(AppConstants.HeaderNameEndpoint, name, WafeJsonContext.Default.ValueRequestString, cancellationToken);
    }

    // ==================== HELPER METHODS ====================

    private async Task<ApiResult<T>> GetAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Endpoint} failed with status {StatusCode}", endpoint, (int)response.StatusCode);
                return ApiResult<T>.Fail(ApiErrors.FromStatus(response.StatusCode), (int)response.StatusCode);
            }

            _logger.LogDebug("GET {Endpoint} → {Body}", endpoint, body);
            if (JsonSerializer.Deserialize(body, typeInfo) is { } value)
                return value;

            _logger.LogError(InvalidResponseMessage, endpoint);
            return ApiResult<T>.Fail(ApiError.InvalidResponse);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult<T>.Fail(LogFailure(ex, "GET", endpoint));
        }
    }

    private async Task<ApiResult> PutValueAsync<T>(string endpoint, T value, JsonTypeInfo<ValueRequest<T>> typeInfo, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PUT {Endpoint} = {Value}", endpoint, value);

            using var content = JsonBody.Create(new ValueRequest<T> { Value = value }, typeInfo);
            using var response = await _httpClient.PutAsync(endpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
                return ApiResult.Success;

            _logger.LogWarning("PUT {Endpoint} failed with status {StatusCode}", endpoint, (int)response.StatusCode);
            return ApiResult.Fail(ApiErrors.FromStatus(response.StatusCode), (int)response.StatusCode);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult.Fail(LogFailure(ex, "PUT", endpoint));
        }
    }

    private ApiError LogFailure(Exception ex, string method, string endpoint)
    {
        var error = ApiErrors.FromException(ex);
        if (ApiErrors.IsExpected(error))
            _logger.LogWarning("{Method} {Endpoint} failed: {Error} ({Reason})", method, endpoint, error, ex.Message);
        else if (error == ApiError.InvalidResponse)
            _logger.LogError(ex, InvalidResponseMessage, endpoint);
        else
            _logger.LogError(ex, "{Method} {Endpoint} failed", method, endpoint);

        return error;
    }
}
