namespace RecuperationSystem.Desktop.Configuration;

/// <summary>
/// Configuration for Wafe API authentication credentials
/// </summary>
public class WafeCredentials
{
    /// <summary>
    /// Wafe API username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Wafe API password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
