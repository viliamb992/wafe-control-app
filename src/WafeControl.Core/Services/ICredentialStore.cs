namespace WafeControl.Core.Services;

/// <summary>
/// A remembered login. <paramref name="SavedAt"/> is when the password was last typed; null where the platform
/// doesn't keep it (Windows) or for a login saved by a version without time limits.
/// </summary>
public sealed record StoredLogin(string Username, string Password, DateTimeOffset? SavedAt = null);

public interface ICredentialStore
{
    Task<StoredLogin?> TryGetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StoredLogin login, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
