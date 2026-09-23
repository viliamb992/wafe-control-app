using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Serialization;
using RecuperationSystem.Shared.Services.Http;

namespace RecuperationSystem.Shared.Services;

/// <summary>
/// Typed client for the Wafe REST API. Stateless: the Sandcastle-Key lives in <see cref="WafeSession"/>
/// and is attached to requests by <see cref="SandcastleAuthHandler"/>.
/// </summary>
public class WafeApiService : IWafeApiService
{
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

    public async Task<bool> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("POST {Endpoint} for {Username}", AppConstants.AuthContextEndpoint, username);

            using var request = SandcastleAuth.CreateLoginRequest(username, password);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (SandcastleAuth.TryReadKey(response, out var key))
            {
                _session.Start(username, password, key);
                _logger.LogInformation("Sandcastle-Key received and stored");
                return true;
            }

            _logger.LogWarning("Authentication failed with status {StatusCode}", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Authentication request failed");
            return false;
        }
    }

    // ==================== GET ENDPOINTS ====================

    public Task<SystemStatus?> GetMainStatusAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.MainEndpoint, WafeJsonContext.Default.SystemStatus, cancellationToken);

    public Task<HeaderInfo?> GetHeaderInfoAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.HeaderEndpoint, WafeJsonContext.Default.HeaderInfo, cancellationToken);

    public Task<SystemInfo?> GetSystemInfoAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.InfoEndpoint, WafeJsonContext.Default.SystemInfo, cancellationToken);

    public Task<MessagesResponse?> GetMessagesAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.MessagesEndpoint, WafeJsonContext.Default.MessagesResponse, cancellationToken);

    public Task<ScheduleResponse?> GetScheduleAsync(CancellationToken cancellationToken = default)
        => GetAsync(AppConstants.ScheduleEndpoint, WafeJsonContext.Default.ScheduleResponse, cancellationToken);

    // ==================== CONTROL ENDPOINTS (PUT) ====================

    public Task<bool> SetStopActiveAsync(bool stopActive, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.StopActiveEndpoint, stopActive, WafeJsonContext.Default.ValueRequestBoolean, cancellationToken);

    public Task<bool> SetSilentModeAsync(bool enabled, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.SilentActiveEndpoint, enabled, WafeJsonContext.Default.ValueRequestBoolean, cancellationToken);

    public Task<bool> SetHolidayModeAsync(bool enabled, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.HolidayActiveEndpoint, enabled, WafeJsonContext.Default.ValueRequestBoolean, cancellationToken);

    public Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.BoostRemainingEndpoint, seconds, WafeJsonContext.Default.ValueRequestInt32, cancellationToken);

    public Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.AuthorityEndpoint, mode, WafeJsonContext.Default.ValueRequestString, cancellationToken);

    public Task<bool> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default)
    {
        if (speed is < AppConstants.MinFlowSpeed or > AppConstants.MaxFlowSpeed)
        {
            _logger.LogWarning("Flow speed {Speed} out of range ({Min}-{Max})", speed, AppConstants.MinFlowSpeed, AppConstants.MaxFlowSpeed);
            return Task.FromResult(false);
        }

        return PutValueAsync(AppConstants.FlowRequestedEndpoint, speed, WafeJsonContext.Default.ValueRequestInt32, cancellationToken);
    }

    public Task<bool> SetSchedulePlanAsync(string plan, CancellationToken cancellationToken = default)
        => PutValueAsync(AppConstants.SchedulePlanEndpoint, plan, WafeJsonContext.Default.ValueRequestString, cancellationToken);

    // ==================== HELPER METHODS ====================

    private async Task<T?> GetAsync<T>(string endpoint, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Endpoint} failed with status {StatusCode}", endpoint, (int)response.StatusCode);
                return null;
            }

            _logger.LogDebug("GET {Endpoint} → {Body}", endpoint, body);
            return JsonSerializer.Deserialize(body, typeInfo);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "GET {Endpoint} failed", endpoint);
            return null;
        }
    }

    private async Task<bool> PutValueAsync<T>(string endpoint, T value, JsonTypeInfo<ValueRequest<T>> typeInfo, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PUT {Endpoint} = {Value}", endpoint, value);

            using var content = JsonBody.Create(new ValueRequest<T> { Value = value }, typeInfo);
            using var response = await _httpClient.PutAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("PUT {Endpoint} failed with status {StatusCode}", endpoint, (int)response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "PUT {Endpoint} failed", endpoint);
            return false;
        }
    }
}
