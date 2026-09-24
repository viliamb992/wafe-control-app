using System.Globalization;
using WafeControl.Shared;

namespace WafeControl.Core.Localization;

/// <summary>
/// Display names for the unit's operating modes (API "authority").
/// </summary>
public static class ModeNames
{
    /// <summary>
    /// Shown when there's no value.
    /// </summary>
    public const string NoValue = "–";

    public static string Operating(string? mode) => mode switch
    {
        AppConstants.ModeIntelligent => Strings.ModeIntelligent,
        AppConstants.ModeManual => Strings.ModeManual,
        AppConstants.ModeSchedule => Strings.ModeSchedule,
        null or "" => NoValue,
        // A mode this app doesn't know yet: show the API name rather than nothing.
        _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(mode),
    };
}
