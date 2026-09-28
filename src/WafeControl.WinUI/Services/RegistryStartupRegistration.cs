using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WafeControl.Core.Services;

namespace WafeControl.WinUI.Services;

/// <summary>
/// Starts the app at sign-in through the per-user Run key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run), the mechanism for unpackaged apps.
/// Windows lists the entry in Task Manager → Startup apps, where the user can also disable it.
/// </summary>
public sealed class RegistryStartupRegistration : IStartupRegistration
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "WafeControl";

    // The entry's name up to 1.0.1 (LegacyInstallMigration renames it).
    private const string LegacyValueName = "WafeRecuperation";

    // Where Task Manager → Startup apps records its on/off switch; first byte odd = disabled, missing = enabled.
    internal const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    internal static readonly string Command = $"\"{Environment.ProcessPath}\"";

    private readonly ILogger<RegistryStartupRegistration> _logger;

    public RegistryStartupRegistration(ILogger<RegistryStartupRegistration> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// True only when the entry starts this copy of the app and Task Manager hasn't disabled it.
    /// An entry left by a copy elsewhere (moved folder, another build) counts as off; turning it on points it here.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (!string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase))
                return false;

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
            return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } state || state[0] % 2 == 0;
        }
    }

    /// <summary>
    /// Removes the Start with Windows entry (and the one used up to 1.0.1). Runs from the uninstaller hook, before
    /// logging exists, so it never throws.
    /// </summary>
    public static void RemoveEntries()
    {
        foreach (var name in new[] { ValueName, LegacyValueName })
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
                    key?.DeleteValue(name, throwOnMissingValue: false);
                using (var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true))
                    approved?.DeleteValue(name, throwOnMissingValue: false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                // Left behind; Windows shows it as a startup app that can't be found.
            }
        }
    }

    public bool TrySetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
                key.SetValue(ValueName, Command);
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);

            // Clear Task Manager's switch too, so turning it on here isn't overruled by an old "disabled".
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
            approved?.DeleteValue(ValueName, throwOnMissingValue: false);

            _logger.LogInformation("Start with Windows {State}: {Command}", enabled ? "enabled" : "disabled", Command);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _logger.LogError(ex, "Could not change start with Windows to {Enabled}", enabled);
            return false;
        }
    }
}
