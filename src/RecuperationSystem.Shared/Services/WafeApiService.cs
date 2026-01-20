using System.Net.Http.Json;
using System.Text.Json;
using RecuperationSystem.Shared.Models;
using Serilog;

namespace RecuperationSystem.Shared.Services;

public class WafeApiService : IWafeApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private string? _sandcastleKey;

    public WafeApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConstants.WafeApiBaseUrl)
        };
    }

    // ==================== AUTHENTICATION ====================

    public async Task<bool> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new AuthRequest { Username = username, Password = password };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? POST {Url}", $"{AppConstants.WafeApiBaseUrl}{AppConstants.AuthContextEndpoint}");
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PostAsJsonAsync(AppConstants.AuthContextEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Auth Response Status: {StatusCode}", response.StatusCode);
            Log.Debug("? Auth Response Body: {ResponseBody}", responseBody);
            
            // Auth endpoint returns 201 Created on success
            if (response.StatusCode == System.Net.HttpStatusCode.Created || response.IsSuccessStatusCode)
            {
                // Extract Sandcastle-Key from response headers
                if (response.Headers.TryGetValues("Sandcastle-Key", out var keys))
                {
                    _sandcastleKey = keys.FirstOrDefault();
                    Log.Information("Sandcastle-Key received and stored");
                    Log.Debug("Sandcastle-Key value: {Key}", _sandcastleKey);
                    return true;
                }
                else
                {
                    Log.Warning("Authentication returned success but no Sandcastle-Key header found");
                    Log.Debug("Response headers: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
                    
                    // Try to extract from Set-Cookie as fallback
                    if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                    {
                        foreach (var cookie in cookies)
                        {
                            if (cookie.Contains("Sandcastle-Key="))
                            {
                                var keyValue = cookie.Split(';')[0].Split('=')[1];
                                _sandcastleKey = keyValue;
                                Log.Information("Sandcastle-Key extracted from Set-Cookie");
                                Log.Debug("Sandcastle-Key value: {Key}", _sandcastleKey);
                                return true;
                            }
                        }
                    }
                    
                    return false;
                }
            }
            
            Log.Warning("Authentication failed with status: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Auth Exception");
            return false;
        }
    }

    // ==================== GET ENDPOINTS ====================

    public async Task<SystemStatus?> GetMainStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            Log.Information("? GET {Endpoint}", AppConstants.MainEndpoint);
            
            var response = await _httpClient.GetAsync(AppConstants.MainEndpoint, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Status code: {StatusCode}, Response Body: {ResponseBody}", response.StatusCode, responseBody);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SystemStatus>(cancellationToken: cancellationToken);
            }
            
            Log.Warning("Failed to get main status: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting main status");
            return null;
        }
    }

    public async Task<HeaderInfo?> GetHeaderInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            Log.Information("? GET {Endpoint}", AppConstants.HeaderEndpoint);
            
            var response = await _httpClient.GetAsync(AppConstants.HeaderEndpoint, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Header Info Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<HeaderInfo>(cancellationToken: cancellationToken);
            }
            
            Log.Warning("Failed to get header info: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting header info");
            return null;
        }
    }

    public async Task<SystemInfo?> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            Log.Information("? GET {Endpoint}", AppConstants.InfoEndpoint);
            
            var response = await _httpClient.GetAsync(AppConstants.InfoEndpoint, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? System Info Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SystemInfo>(cancellationToken: cancellationToken);
            }
            
            Log.Warning("Failed to get system info: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting system info");
            return null;
        }
    }

    public async Task<MessagesResponse?> GetMessagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            Log.Information("? GET {Endpoint}", AppConstants.MessagesEndpoint);
            
            var response = await _httpClient.GetAsync(AppConstants.MessagesEndpoint, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Messages Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken: cancellationToken);
            }
            
            Log.Warning("Failed to get messages: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting messages");
            return null;
        }
    }

    public async Task<ScheduleResponse?> GetScheduleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            Log.Information("? GET {Endpoint}", AppConstants.ScheduleEndpoint);
            
            var response = await _httpClient.GetAsync(AppConstants.ScheduleEndpoint, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Schedule Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ScheduleResponse>(cancellationToken: cancellationToken);
            }
            
            Log.Warning("Failed to get schedule: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting schedule");
            return null;
        }
    }

    // ==================== CONTROL ENDPOINTS (PUT) ====================

    public async Task<bool> SetStopActiveAsync(bool stop, CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            var request = new ValueRequest<bool> { Value = stop };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? PUT {Endpoint} - Setting stop-active to {Value} (stop={Stop}, start={Start})", 
                AppConstants.StopActiveEndpoint, stop, stop, !stop);
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PutAsJsonAsync(AppConstants.StopActiveEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Stop-Active Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting stop-active");
            return false;
        }
    }

    public async Task<bool> SetSilentModeAsync(bool silent, CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            var request = new ValueRequest<bool> { Value = silent };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? PUT {Endpoint} - Setting silent mode to {Silent}", 
                AppConstants.SilentActiveEndpoint, silent);
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PutAsJsonAsync(AppConstants.SilentActiveEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Silent Mode Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting silent mode");
            return false;
        }
    }

    public async Task<bool> SetHolidayModeAsync(bool holiday, CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            var request = new ValueRequest<bool> { Value = holiday };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? PUT {Endpoint} - Setting holiday mode to {Holiday}", 
                AppConstants.HolidayActiveEndpoint, holiday);
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PutAsJsonAsync(AppConstants.HolidayActiveEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Holiday Mode Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting holiday mode");
            return false;
        }
    }

    public async Task<bool> SetBoostAsync(int seconds, CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            var request = new ValueRequest<int> { Value = seconds };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? PUT {Endpoint} - Setting boost to {Seconds} seconds ({Minutes} minutes)", 
                AppConstants.BoostRemainingEndpoint, seconds, seconds / 60);
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PutAsJsonAsync(AppConstants.BoostRemainingEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Boost Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting boost");
            return false;
        }
    }

    public async Task<bool> SetAuthorityModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            var request = new ValueRequest<string> { Value = mode };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? PUT {Endpoint} - Setting authority mode to {Mode}", 
                AppConstants.AuthorityEndpoint, mode);
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PutAsJsonAsync(AppConstants.AuthorityEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Authority Mode Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting authority mode");
            return false;
        }
    }

    public async Task<bool> SetFlowSpeedAsync(int speed, CancellationToken cancellationToken = default)
    {
        try
        {
            if (speed < AppConstants.MinFlowSpeed || speed > AppConstants.MaxFlowSpeed)
            {
                Log.Warning("Flow speed {Speed} out of range ({Min}-{Max})", speed, AppConstants.MinFlowSpeed, AppConstants.MaxFlowSpeed);
                return false;
            }

            AddAuthHeader();
            var request = new ValueRequest<int> { Value = speed };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? PUT {Endpoint} - Setting flow speed to {Speed} m³/h", 
                AppConstants.FlowRequestedEndpoint, speed);
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PutAsJsonAsync(AppConstants.FlowRequestedEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Flow Speed Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting flow speed");
            return false;
        }
    }

    public async Task<bool> SetSchedulePlanAsync(string plan, CancellationToken cancellationToken = default)
    {
        try
        {
            AddAuthHeader();
            var request = new ValueRequest<string> { Value = plan };
            var requestJson = JsonSerializer.Serialize(request);
            
            Log.Information("? PUT {Endpoint} - Setting schedule plan", AppConstants.SchedulePlanEndpoint);
            Log.Debug("? Request Body: {RequestBody}", requestJson);
            
            var response = await _httpClient.PutAsJsonAsync(AppConstants.SchedulePlanEndpoint, request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            Log.Debug("? Schedule Plan Response: {StatusCode}", response.StatusCode);
            Log.Debug("? Response Body: {ResponseBody}", responseBody);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting schedule plan");
            return false;
        }
    }

    // ==================== HELPER METHODS ====================

    private void AddAuthHeader()
    {
        if (!string.IsNullOrEmpty(_sandcastleKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("Sandcastle-Key");
            _httpClient.DefaultRequestHeaders.Add("Sandcastle-Key", _sandcastleKey);
            Log.Debug("Added Sandcastle-Key header to request");
        }
        else
        {
            Log.Warning("No Sandcastle-Key available for authenticated request");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
