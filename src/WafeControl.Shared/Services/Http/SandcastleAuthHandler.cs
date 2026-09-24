using System.Net;
using Microsoft.Extensions.Logging;

namespace WafeControl.Shared.Services.Http;

/// <summary>
/// Attaches the session's Sandcastle-Key to every API request. When the API answers 401/403,
/// signs in again with the session credentials and retries the request once; if that fails the
/// session is expired so the UI can ask the user to sign in.
/// </summary>
public sealed class SandcastleAuthHandler : DelegatingHandler
{
    private readonly WafeSession _session;
    private readonly ILogger<SandcastleAuthHandler> _logger;

    public SandcastleAuthHandler(WafeSession session, ILogger<SandcastleAuthHandler> logger)
    {
        _session = session;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (SandcastleAuth.IsLoginRequest(request))
            return await base.SendAsync(request, cancellationToken);

        var keyUsed = _session.Key;
        SetKey(request, keyUsed);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) || _session.Credentials is null)
            return response;

        _logger.LogInformation("{Method} {Path} returned {StatusCode}; renewing session",
            request.Method, request.RequestUri?.AbsolutePath, (int)response.StatusCode);

        if (!await TryRenewAsync(keyUsed, cancellationToken))
        {
            _logger.LogWarning("Session renewal failed; session expired");
            _session.Expire();
            return response;
        }

        response.Dispose();
        SetKey(request, _session.Key);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<bool> TryRenewAsync(string? staleKey, CancellationToken cancellationToken)
    {
        await _session.RenewLock.WaitAsync(cancellationToken);
        try
        {
            // Another request renewed the session while this one was waiting.
            if (_session.Key is { } current && current != staleKey)
                return true;

            if (_session.Credentials is not { } credentials)
                return false;

            using var loginRequest = SandcastleAuth.CreateLoginRequest(credentials.Username, credentials.Password);
            using var loginResponse = await base.SendAsync(loginRequest, cancellationToken);

            if (!SandcastleAuth.TryReadKey(loginResponse, out var key))
                return false;

            _session.Renew(key);
            _logger.LogInformation("Session renewed");
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Session renewal request failed");
            return false;
        }
        finally
        {
            _session.RenewLock.Release();
        }
    }

    private static void SetKey(HttpRequestMessage request, string? key)
    {
        request.Headers.Remove(AppConstants.SandcastleKeyHeader);
        if (!string.IsNullOrEmpty(key))
            request.Headers.TryAddWithoutValidation(AppConstants.SandcastleKeyHeader, key);
    }
}
