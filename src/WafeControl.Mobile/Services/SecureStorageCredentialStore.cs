using System.Globalization;
using Microsoft.Extensions.Logging;
using WafeControl.Core.Services;

namespace WafeControl.Mobile.Services;

/// <summary>
/// Remembers the login in the platform's secure storage: Android Keystore, iOS Keychain. The time the password
/// was typed is kept next to it, for the time limit.
/// </summary>
public sealed class SecureStorageCredentialStore(ILogger<SecureStorageCredentialStore> logger) : ICredentialStore
{
    private const string UsernameKey = "wafe.username";
    private const string PasswordKey = "wafe.password";
    private const string SavedAtKey = "wafe.saved-at";

    public async Task<StoredLogin?> TryGetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var username = await SecureStorage.Default.GetAsync(UsernameKey);
            var password = await SecureStorage.Default.GetAsync(PasswordKey);
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                return null;

            // Missing for a login saved by an earlier version.
            var savedAtText = await SecureStorage.Default.GetAsync(SavedAtKey);
            DateTimeOffset? savedAt = DateTimeOffset.TryParse(savedAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
                ? time
                : null;
            return new StoredLogin(username, password, savedAt);
        }
        catch (Exception ex)
        {
            // E.g. the key was lost after a restore from backup; ask to sign in again.
            logger.LogWarning(ex, "Remembered login can't be read; forgetting it");
            SecureStorage.Default.RemoveAll();
            return null;
        }
    }

    public async Task SaveAsync(StoredLogin login, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(UsernameKey, login.Username);
        await SecureStorage.Default.SetAsync(PasswordKey, login.Password);
        if (login.SavedAt is { } savedAt)
            await SecureStorage.Default.SetAsync(SavedAtKey, savedAt.ToString("O", CultureInfo.InvariantCulture));
        else
            SecureStorage.Default.Remove(SavedAtKey);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(UsernameKey);
        SecureStorage.Default.Remove(PasswordKey);
        SecureStorage.Default.Remove(SavedAtKey);
        return Task.CompletedTask;
    }
}
