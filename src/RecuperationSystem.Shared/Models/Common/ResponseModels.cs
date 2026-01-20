using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Generic API response wrapper
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("data")]
    public T? Data { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Simple success/error response for PUT/POST operations
/// </summary>
public class OperationResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
