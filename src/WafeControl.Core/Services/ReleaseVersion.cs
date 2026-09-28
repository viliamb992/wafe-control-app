using System.Globalization;

namespace WafeControl.Core.Services;

/// <summary>
/// A release's version, "1.3.0" or "1.3.0-beta.1", ordered as in Semantic Versioning (1.3.0-beta.1 &lt; 1.3.0).
/// </summary>
public sealed record ReleaseVersion(int Major, int Minor, int Patch, string? Prerelease = null) : IComparable<ReleaseVersion>
{
    public bool IsPrerelease => Prerelease is not null;

    /// <summary>
    /// Null for anything that isn't major.minor.patch with an optional "-suffix" (a "+build" part is ignored).
    /// </summary>
    public static ReleaseVersion? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var plus = text.IndexOf('+');
        if (plus >= 0)
            text = text[..plus];

        var dash = text.IndexOf('-');
        var core = dash >= 0 ? text[..dash] : text;
        var prerelease = dash >= 0 ? text[(dash + 1)..] : null;
        if (prerelease is "")
            return null;

        var parts = core.Split('.');
        return parts.Length == 3
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)
                ? new ReleaseVersion(major, minor, patch, prerelease)
                : null;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
            return 1;

        var result = Major.CompareTo(other.Major);
        if (result == 0)
            result = Minor.CompareTo(other.Minor);
        if (result == 0)
            result = Patch.CompareTo(other.Patch);
        return result != 0 ? result : ComparePrerelease(Prerelease, other.Prerelease);
    }

    public override string ToString() => Prerelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Prerelease}";

    // No suffix is newer than any suffix; otherwise dot-separated parts, numbers numerically and before words.
    private static int ComparePrerelease(string? a, string? b)
    {
        if (a is null || b is null)
            return a is null ? (b is null ? 0 : 1) : -1;

        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftIsNumber = int.TryParse(left[i], NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumber = int.TryParse(right[i], NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            var result = (leftIsNumber, rightIsNumber) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left[i], right[i]),
            };
            if (result != 0)
                return result;
        }

        return left.Length.CompareTo(right.Length);
    }
}
