using System.Text.Json.Serialization;

namespace WafeControl.Shared.Models;

/// <summary>
/// Authentication request model for /api/auth/context.
/// A successful login returns 201 Created with a Sandcastle-Key header.
/// </summary>
public class AuthRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
    
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}
