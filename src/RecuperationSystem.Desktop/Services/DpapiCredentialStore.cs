using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RecuperationSystem.Desktop.Services;

public sealed class DpapiCredentialStore : ICredentialStore
{
    private static readonly string CredPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RecuperationSystem",
        "credentials.dat");

    public async Task<(string Username, string Password)?> TryGetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(CredPath))
            return null;

        byte[] protectedBytes;
        try
        {
            protectedBytes = await File.ReadAllBytesAsync(CredPath, cancellationToken);
        }
        catch (IOException)
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<CredentialPayload>(bytes);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Username) || string.IsNullOrEmpty(payload.Password))
                return null;

            return (payload.Username, payload.Password);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(Path.GetDirectoryName(CredPath)!);

        var payload = new CredentialPayload(username, password);
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        var protectedBytes = ProtectedData.Protect(jsonBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(CredPath, protectedBytes, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (File.Exists(CredPath))
                File.Delete(CredPath);
        }
        catch (IOException)
        {
            // ignore
        }

        return Task.CompletedTask;
    }

    private sealed record CredentialPayload(string Username, string Password);
}
