using Microsoft.Win32;
using Serilog;

namespace WafeControl.WinUI.Services;

/// <summary>
/// Carries over what versions up to 1.0.1 (named RecuperationSystem / "Wafe Recuperation") left behind:
/// settings, remembered login, logs and the Start with Windows entry. Each item moves only when
/// the new location is still empty, so it's safe to run on every launch.
/// </summary>
public static class LegacyInstallMigration
{
    private const string LegacyFolderName = "RecuperationSystem";
    private const string LegacyStartupValueName = "WafeRecuperation";

    private static readonly string LegacyAppData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyFolderName);

    private static readonly string LegacyLocalAppData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyFolderName);

    /// <summary>
    /// Runs before the logger exists, since the logger creates the new folder; a failure just leaves the old logs.
    /// </summary>
    public static void MoveLogs(string logDirectory)
    {
        try
        {
            var oldLogs = Path.Combine(LegacyLocalAppData, "logs");
            if (!Directory.Exists(oldLogs) || Directory.Exists(logDirectory))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(logDirectory)!);
            Directory.Move(oldLogs, logDirectory);
            DeleteIfEmpty(LegacyLocalAppData);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Must run before the settings and the remembered login are first read.
    /// </summary>
    public static void MoveUserData()
    {
        try
        {
            MoveFile(Path.Combine(LegacyAppData, "settings.json"), JsonSettingsStore.SettingsPath);
            MoveFile(Path.Combine(LegacyAppData, "credentials.dat"), DpapiCredentialStore.CredPath);
            DeleteIfEmpty(LegacyAppData);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not move settings from {Folder}", LegacyAppData);
        }

        try
        {
            MoveStartupEntry();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Log.Warning(ex, "Could not move the Start with Windows entry {Name}", LegacyStartupValueName);
        }
    }

    private static void MoveFile(string oldPath, string newPath)
    {
        if (!File.Exists(oldPath) || File.Exists(newPath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.Move(oldPath, newPath);
        Log.Information("Moved {OldPath} to {NewPath}", oldPath, newPath);
    }

    private static void DeleteIfEmpty(string directory)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            Directory.Delete(directory);
    }

    /// <summary>
    /// Takes over the old entry only when it started the copy this one replaced (same folder, as after an upgrade),
    /// keeping Task Manager's on/off state. An entry of a copy elsewhere is left to that copy.
    /// </summary>
    private static void MoveStartupEntry()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RegistryStartupRegistration.RunKeyPath, writable: true);
        if (run?.GetValue(LegacyStartupValueName) is not string oldCommand)
            return;

        var oldFolder = Path.GetDirectoryName(oldCommand.Trim('"'));
        if (!string.Equals(oldFolder, Path.GetDirectoryName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase))
            return;

        run.SetValue(RegistryStartupRegistration.ValueName, RegistryStartupRegistration.Command);
        run.DeleteValue(LegacyStartupValueName, throwOnMissingValue: false);

        using var approved = Registry.CurrentUser.OpenSubKey(RegistryStartupRegistration.ApprovedKeyPath, writable: true);
        if (approved?.GetValue(LegacyStartupValueName) is byte[] state)
            approved.SetValue(RegistryStartupRegistration.ValueName, state, RegistryValueKind.Binary);
        approved?.DeleteValue(LegacyStartupValueName, throwOnMissingValue: false);

        Log.Information("Moved Start with Windows entry {OldName} to {NewName}", LegacyStartupValueName, RegistryStartupRegistration.ValueName);
    }
}
