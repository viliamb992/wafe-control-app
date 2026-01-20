using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Response from GET /api/v1/info
/// Detailed system information and diagnostics
/// </summary>
public class SystemInfo
{
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; set; }
    
    [JsonPropertyName("firmwareVersion")]
    public string? FirmwareVersion { get; set; }
    
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }
    
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }
    
    [JsonPropertyName("installationDate")]
    public DateTime? InstallationDate { get; set; }
    
    [JsonPropertyName("lastMaintenance")]
    public DateTime? LastMaintenance { get; set; }
    
    [JsonPropertyName("filterLifeRemaining")]
    public int? FilterLifeRemaining { get; set; }
    
    [JsonPropertyName("operatingHours")]
    public int? OperatingHours { get; set; }
}
