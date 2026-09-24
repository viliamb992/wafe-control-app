using System.Text.Json.Serialization;

namespace WafeControl.Shared.Models;

/// <summary>
/// Response from GET /api/v1/main
/// Main system status and control state
/// </summary>
public class SystemStatus
{
    [JsonPropertyName("gen")]
    public int Gen { get; set; }
    
    [JsonPropertyName("temperatures")]
    public List<double?>? Temperatures { get; set; }
    
    [JsonPropertyName("humidity")]
    public double? Humidity { get; set; }
    
    [JsonPropertyName("co2")]
    public int? Co2 { get; set; }
    
    [JsonPropertyName("power-min")]
    public int PowerMin { get; set; }
    
    [JsonPropertyName("power-max")]
    public int PowerMax { get; set; }
    
    [JsonPropertyName("power-requested")]
    public int PowerRequested { get; set; }
    
    [JsonPropertyName("power-current")]
    public int PowerCurrent { get; set; }
    
    [JsonPropertyName("flow-min")]
    public int FlowMin { get; set; }
    
    [JsonPropertyName("flow-max")]
    public int FlowMax { get; set; }
    
    [JsonPropertyName("flow-requested")]
    public int FlowRequested { get; set; }
    
    [JsonPropertyName("flow-current")]
    public int FlowCurrent { get; set; }
    
    [JsonPropertyName("authority")]
    public string Authority { get; set; } = "intelligent";
    
    [JsonPropertyName("authority-available")]
    public List<string>? AuthorityAvailable { get; set; }
    
    [JsonPropertyName("capabilities")]
    public List<string>? Capabilities { get; set; }
    
    [JsonPropertyName("fireplace-active")]
    public int FireplaceActive { get; set; }
    
    [JsonPropertyName("fireplace-enabled")]
    public bool FireplaceEnabled { get; set; }
    
    [JsonPropertyName("holiday-active")]
    public bool HolidayActive { get; set; }
    
    [JsonPropertyName("boost-duration")]
    public int BoostDuration { get; set; }
    
    [JsonPropertyName("boost-remaining")]
    public int BoostRemaining { get; set; }
    
    [JsonPropertyName("circulation-active")]
    public bool CirculationActive { get; set; }
    
    [JsonPropertyName("silent-active")]
    public bool SilentActive { get; set; }
    
    [JsonPropertyName("stop-active")]
    public bool StopActive { get; set; }
    
    [JsonPropertyName("stop-progress")]
    public int StopProgress { get; set; }
    
    [JsonPropertyName("filters")]
    public FilterStatus? Filters { get; set; }
    
    [JsonPropertyName("uptime")]
    public int Uptime { get; set; }
    
    // Helper properties for easier access
    public int FlowActual => FlowCurrent;
    public bool IsSystemRunning => !StopActive;
}
