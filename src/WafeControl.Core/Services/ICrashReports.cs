namespace WafeControl.Core.Services;

/// <summary>
/// Turns crash reports on and off for the platform (the user's choice is kept in <see cref="UserSettings.CrashReports"/>).
/// </summary>
public interface ICrashReports
{
    /// <summary>
    /// Reports can be sent: a release build with a reporting address. Otherwise the setting is hidden.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Turning reports on applies at once; otherwise at the next start (the setting says so).
    /// </summary>
    bool AppliesImmediately { get; }

    /// <summary>
    /// Starts or stops reporting for the rest of this session, as far as the platform allows.
    /// </summary>
    void Apply(bool enabled);
}

/// <summary>
/// No crash reports: development builds and platforms without a reporting address.
/// </summary>
public sealed class NoCrashReports : ICrashReports
{
    public bool IsAvailable => false;

    public bool AppliesImmediately => true;

    public void Apply(bool enabled)
    {
    }
}
