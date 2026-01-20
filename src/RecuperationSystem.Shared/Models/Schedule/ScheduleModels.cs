using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Response from GET /api/v1/schedule
/// Current schedule configuration
/// </summary>
public class ScheduleResponse
{
    [JsonPropertyName("plan")]
    public string Plan { get; set; } = string.Empty;
    
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    
    [JsonPropertyName("scheduleEntries")]
    public List<ScheduleEntry>? ScheduleEntries { get; set; }
}

/// <summary>
/// Individual schedule entry for a specific time period
/// </summary>
public class ScheduleEntry
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty; // "min", "auto", "boost"
    
    [JsonPropertyName("dayOfWeek")]
    public int DayOfWeek { get; set; } // 0-6 (Sunday-Saturday)
    
    [JsonPropertyName("startTime")]
    public TimeSpan StartTime { get; set; }
    
    [JsonPropertyName("endTime")]
    public TimeSpan EndTime { get; set; }
    
    [JsonPropertyName("flowSpeed")]
    public int? FlowSpeed { get; set; }
}
