using WafeControl.Core.Services;

namespace WafeControl.Mobile.Services;

/// <summary>
/// Phones don't start apps at sign-in; the setting isn't shown on mobile.
/// </summary>
public sealed class NoStartupRegistration : IStartupRegistration
{
    public bool IsEnabled => false;

    public bool TrySetEnabled(bool enabled) => !enabled;
}
