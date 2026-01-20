using System.Text.Json.Serialization;

namespace RecuperationSystem.Shared.Models;

/// <summary>
/// Generic value request wrapper for PUT/POST operations
/// </summary>
/// <typeparam name="T">The type of value being sent</typeparam>
public class ValueRequest<T>
{
    [JsonPropertyName("value")]
    public T Value { get; set; } = default!;
}
