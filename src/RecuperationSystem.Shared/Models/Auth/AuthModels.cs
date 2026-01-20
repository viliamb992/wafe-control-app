using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Authentication request model for /api/auth/context
/// </summary>
public class AuthRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Authentication response from /api/auth/context
/// Returns 201 Created with Sandcastle-Key header
/// </summary>
public class AuthResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("success")]
    public bool? Success { get; set; }
}
