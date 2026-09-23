using System.Text.Json.Serialization;
using RecuperationSystem.Shared.Models;

namespace RecuperationSystem.Shared.Serialization;

/// <summary>
/// Source-generated JSON metadata for every Wafe API payload.
/// Keeps serialization reflection-free so the app can be trimmed (Android/iOS).
/// </summary>
[JsonSerializable(typeof(AuthRequest))]
[JsonSerializable(typeof(SystemStatus))]
[JsonSerializable(typeof(HeaderInfo))]
[JsonSerializable(typeof(SystemInfo))]
[JsonSerializable(typeof(ScheduleResponse))]
[JsonSerializable(typeof(ValueRequest<bool>))]
[JsonSerializable(typeof(ValueRequest<int>))]
[JsonSerializable(typeof(ValueRequest<string>))]
public partial class WafeJsonContext : JsonSerializerContext
{
}
