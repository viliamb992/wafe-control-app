using System.Text.Json.Serialization;

namespace WafeControl.Shared.Models;

/// <summary>
/// Filter status information for both fresh and waste filters
/// </summary>
public class FilterStatus
{
    [JsonPropertyName("fresh")]
    public FilterInfo? Fresh { get; set; }
    
    [JsonPropertyName("waste")]
    public FilterInfo? Waste { get; set; }
}

/// <summary>
/// Individual filter health and status information
/// </summary>
public class FilterInfo
{
    [JsonPropertyName("health")]
    public int Health { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
