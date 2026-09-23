using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Response from GET /api/v1/info, e.g.
/// <c>{"contacts": {"service": {"mail": "servis@wafe.cz", "name": "WAFE s.r.o.", "web": "https://wafe.eu/"}},
/// "unit": {"model": "W0201CEdEUBQ210", "sn": "1234567000000", "type": "W201 E"}}</c>.
/// </summary>
public class SystemInfo
{
    [JsonPropertyName("contacts")]
    public SystemContacts? Contacts { get; set; }

    [JsonPropertyName("unit")]
    public UnitInfo? Unit { get; set; }
}

public class SystemContacts
{
    [JsonPropertyName("service")]
    public ContactInfo? Service { get; set; }
}

public class ContactInfo
{
    [JsonPropertyName("mail")]
    public string? Mail { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("web")]
    public string? Web { get; set; }
}

public class UnitInfo
{
    /// <summary>
    /// Full model code, e.g. "W0201CEdEUBQ210".
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("sn")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Unit type, e.g. "W201 E".
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
