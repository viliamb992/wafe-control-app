using System.Diagnostics.CodeAnalysis;
using RecuperationSystem.Shared.Models;
using RecuperationSystem.Shared.Serialization;

namespace RecuperationSystem.Shared.Services.Http;

/// <summary>
/// The Wafe login exchange: POST credentials to auth/context, receive a Sandcastle-Key.
/// </summary>
internal static class SandcastleAuth
{
    public static readonly Uri LoginUri = new(new Uri(AppConstants.WafeApiBaseUrl), AppConstants.AuthContextEndpoint);

    public static HttpRequestMessage CreateLoginRequest(string username, string password) =>
        new(HttpMethod.Post, LoginUri)
        {
            Content = JsonBody.Create(
                new AuthRequest { Username = username, Password = password },
                WafeJsonContext.Default.AuthRequest)
        };

    public static bool IsLoginRequest(HttpRequestMessage request) =>
        request.Method == HttpMethod.Post
        && request.RequestUri is { IsAbsoluteUri: true } uri
        && uri.AbsolutePath.Equals(LoginUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the Sandcastle-Key from a successful login response: the header, or a Set-Cookie fallback.
    /// </summary>
    public static bool TryReadKey(HttpResponseMessage response, [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (!response.IsSuccessStatusCode)
            return false;

        if (response.Headers.TryGetValues(AppConstants.SandcastleKeyHeader, out var keys))
        {
            key = keys.FirstOrDefault(k => !string.IsNullOrEmpty(k));
            if (key is not null)
                return true;
        }

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var prefix = AppConstants.SandcastleKeyHeader + "=";
            key = cookies
                .Select(c => c.Split(';', 2)[0].Trim())
                .Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(c => c[prefix.Length..])
                .FirstOrDefault(v => v.Length > 0);
        }

        return key is not null;
    }
}
