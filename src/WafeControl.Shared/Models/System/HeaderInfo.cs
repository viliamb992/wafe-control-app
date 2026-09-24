using System.Text.Json;
using System.Text.Json.Serialization;

namespace WafeControl.Shared.Models;

/// <summary>
/// Response from GET /api/v1/header, e.g.
/// <c>{"gen": 1790184364, "message": null, "name": "byt 1.001, sn 1234567", "sn": "1234567000000", "gid": 1000,
/// "timestamp": 1790184364.642964, "type": "W201 E", "online": true, "pm": true}</c>.
/// </summary>
public class HeaderInfo
{
    [JsonPropertyName("gen")]
    public long Gen { get; set; }

    /// <summary>
    /// Always null so far, so its shape is unknown. Kept raw so an unexpected value can't break the whole response.
    /// </summary>
    [JsonPropertyName("message")]
    public JsonElement? Message { get; set; }

    /// <summary>
    /// Unit name as set in the Wafe portal.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("sn")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("gid")]
    public int Gid { get; set; }

    /// <summary>
    /// Unix time in seconds, with a fraction.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }

    /// <summary>
    /// Unit type, e.g. "W201 E".
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    /// <summary>
    /// Meaning unknown.
    /// </summary>
    [JsonPropertyName("pm")]
    public bool Pm { get; set; }

    public DateTimeOffset Time => DateTimeOffset.FromUnixTimeMilliseconds((long)(Timestamp * 1000));
}
