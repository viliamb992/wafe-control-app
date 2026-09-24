using System.Text.Json;
using System.Text.Json.Serialization;
using WafeControl.Core.Services;

namespace WafeControl.WinUI.Services;

/// <summary>
/// Keeps the user's settings in %AppData%\WafeControl\settings.json.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    internal static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WafeControl",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new UserSettings();

            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllBytes(SettingsPath), JsonOptions) ?? new UserSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged or locked file must not stop the app from starting; defaults apply.
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllBytes(SettingsPath, JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions));
    }
}
