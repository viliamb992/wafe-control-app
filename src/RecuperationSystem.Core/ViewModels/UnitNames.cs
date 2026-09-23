using System.Text.RegularExpressions;

namespace RecuperationSystem.Core.ViewModels;

public static partial class UnitNames
{
    /// <summary>
    /// The portal name without the serial number the portal adds to it: "byt 1.001, sn 1234567" → "byt 1.001".
    /// Empty when nothing else is left.
    /// </summary>
    public static string WithoutSerialNumber(string? name) =>
        string.IsNullOrWhiteSpace(name) ? string.Empty : SerialNumber().Replace(name, string.Empty).Trim(' ', ',', ';', '-');

    // "sn" plus the whole token after it, which starts with a digit: "sn 1234567", "SN: 1234567x".
    [GeneratedRegex(@"[\s,;-]*\bsn\b[\s:.#]*\d\w*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SerialNumber();
}
