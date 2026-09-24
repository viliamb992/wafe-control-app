namespace WafeControl.Shared.Services.Http;

/// <summary>
/// The authenticated Wafe session, shared by every <see cref="WafeApiService"/> instance.
/// Register as a singleton: typed HttpClients are transient, so the Sandcastle-Key must not live on the client.
/// </summary>
public sealed class WafeSession
{
    private readonly Lock _gate = new();
    private string? _key;
    private (string Username, string Password)? _credentials;

    /// <summary>
    /// Raised when the session could not be renewed (e.g. the password was changed); the user must sign in again.
    /// May be raised on a background thread.
    /// </summary>
    public event EventHandler? Expired;

    /// <summary>
    /// Serializes renewals: IHttpClientFactory creates many handler instances, but one renewal should run at a time.
    /// </summary>
    internal SemaphoreSlim RenewLock { get; } = new(1, 1);

    public string? Key
    {
        get { lock (_gate) return _key; }
    }

    /// <summary>
    /// Credentials of the signed-in user, kept in memory so an expired key can be renewed transparently.
    /// </summary>
    public (string Username, string Password)? Credentials
    {
        get { lock (_gate) return _credentials; }
    }

    public void Start(string username, string password, string key)
    {
        lock (_gate)
        {
            _credentials = (username, password);
            _key = key;
        }
    }

    public void Renew(string key)
    {
        lock (_gate) _key = key;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _credentials = null;
            _key = null;
        }
    }

    public void Expire()
    {
        Clear();
        Expired?.Invoke(this, EventArgs.Empty);
    }
}
