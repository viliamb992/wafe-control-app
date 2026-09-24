using System.Reflection;

namespace WafeControl.Core;

/// <summary>
/// The app version for display, e.g. "1.2.0" or "1.3.0-beta.1".
/// </summary>
public static class AppVersion
{
    /// <summary>
    /// From the app's <see cref="AssemblyInformationalVersionAttribute"/> (the build's <c>Version</c>).
    /// </summary>
    public static string Current { get; } = FromInformational(
        (Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Drops the "+commit" suffix the SDK appends to the informational version.
    /// </summary>
    public static string FromInformational(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return "0.0.0";

        var plus = informationalVersion.IndexOf('+');
        return plus >= 0 ? informationalVersion[..plus] : informationalVersion;
    }
}
