using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Response from GET /api/v1/messages
/// System notifications and messages
/// </summary>
public class MessagesResponse
{
    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = new();
    
    [JsonPropertyName("unreadCount")]
    public int UnreadCount { get; set; }
}

/// <summary>
/// Individual system message or notification
/// </summary>
public class Message
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; } // "info", "warning", "error", "success"
    
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("message")]
    public string? MessageText { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("read")]
    public bool Read { get; set; }
    
    [JsonPropertyName("priority")]
    public string? Priority { get; set; } // "low", "medium", "high"
}
