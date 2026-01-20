using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Response from GET /api/v1/header
/// Header information for the UI
/// </summary>
public class HeaderInfo
{
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }
    
    [JsonPropertyName("online")]
    public bool Online { get; set; }
    
    [JsonPropertyName("lastUpdate")]
    public DateTime? LastUpdate { get; set; }
    
    [JsonPropertyName("firmwareVersion")]
    public string? FirmwareVersion { get; set; }
    
    [JsonPropertyName("connectionStatus")]
    public string? ConnectionStatus { get; set; }
}
