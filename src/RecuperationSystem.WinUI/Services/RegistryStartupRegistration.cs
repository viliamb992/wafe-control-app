using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using RecuperationSystem.Core.Services;

namespace RecuperationSystem.WinUI.Services;

/// <summary>
/// Starts the app at sign-in through the per-user Run key
/// (HKCU\Software\Microsoft\Windows\CurrentVersion\Run), the mechanism for unpackaged apps.
/// Windows lists the entry in Task Manager → Startup apps, where the user can also disable it.
/// </summary>
public sealed class RegistryStartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WafeRecuperation";

    // Where Task Manager → Startup apps records its on/off switch; first byte odd = disabled, missing = enabled.
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static readonly string Command = $"\"{Environment.ProcessPath}\"";

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
