using WafeControl.Core.Services;

namespace WafeControl.Mobile.Services;

/// <summary>
/// <see cref="INetworkStatus"/> from MAUI: the phone reports internet access (Wi-Fi or mobile data).
/// </summary>
public sealed class MauiNetworkStatus : INetworkStatus, IDisposable
{
    private readonly IConnectivity _connectivity;

    public MauiNetworkStatus(IConnectivity connectivity)
    {
        _connectivity = connectivity;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsAvailable => _connectivity.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler? Changed;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose() => _connectivity.ConnectivityChanged -= OnConnectivityChanged;
}
