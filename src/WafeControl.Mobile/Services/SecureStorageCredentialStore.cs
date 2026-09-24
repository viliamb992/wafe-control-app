using Microsoft.Extensions.Logging;
using WafeControl.Core.Services;

namespace WafeControl.Mobile.Services;

/// <summary>
/// Remembers the login in the platform's secure storage: Android Keystore, iOS Keychain.
/// </summary>
public sealed class SecureStorageCredentialStore(ILogger<SecureStorageCredentialStore> logger) : ICredentialStore
{
    private const string UsernameKey = "wafe.username";
    private const string PasswordKey = "wafe.password";

    public async Task<(string Username, string Password)?> TryGetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var username = await SecureStorage.Default.GetAsync(UsernameKey);
            var password = await SecureStorage.Default.GetAsync(PasswordKey);
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                return null;

            return (username, password);
        }
        catch (Exception ex)
        {
            // E.g. the key was lost after a restore from backup; ask to sign in again.
            logger.LogWarning(ex, "Remembered login can't be read; forgetting it");
            SecureStorage.Default.RemoveAll();
            return null;
        }
    }

    public async Task SaveAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(UsernameKey, username);
        await SecureStorage.Default.SetAsync(PasswordKey, password);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(UsernameKey);
        SecureStorage.Default.Remove(PasswordKey);
        return Task.CompletedTask;
    }
}
