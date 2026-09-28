using System.Net.NetworkInformation;

namespace WafeControl.Core.Services;

/// <summary>
/// Whether the device has a network at all; tells "no internet" apart from "the Wafe server doesn't answer".
/// </summary>
public interface INetworkStatus
{
    bool IsAvailable { get; }

    /// <summary>
    /// Raised when <see cref="IsAvailable"/> may have changed; possibly on a background thread.
    /// </summary>
    event EventHandler? Changed;
}

/// <summary>
/// <see cref="INetworkStatus"/> from System.Net: any network interface up. Used on Windows; mobile registers its own.
/// </summary>
public sealed class SystemNetworkStatus : INetworkStatus, IDisposable
{
    public SystemNetworkStatus()
    {
        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
    }

    public bool IsAvailable
    {
        get
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch (NetworkInformationException)
            {
                // Can't tell: assume there is one, so a server failure isn't reported as "no internet".
                return true;
            }
        }
    }

    public event EventHandler? Changed;

    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
}
