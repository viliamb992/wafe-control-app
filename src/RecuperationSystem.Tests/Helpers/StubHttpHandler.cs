using System.Net;
using RecuperationSystem.Shared;

namespace RecuperationSystem.Tests.Helpers;

/// <summary>
/// A request as seen on the wire, captured at send time (request content is disposed afterwards).
/// </summary>
internal sealed record RecordedRequest(HttpMethod Method, string Path, string? SandcastleKey, string? Body, long? ContentLength);

/// <summary>
/// Innermost HTTP handler for tests: answers every request with <paramref name="respond"/> and records it.
/// </summary>
internal sealed class StubHttpHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests
    {
        get { lock (_requests) return [.. _requests]; }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = request.Headers.TryGetValues(AppConstants.SandcastleKeyHeader, out var values) ? values.Single() : null;
        // Read the declared length before the body: reading buffers the content and would make any length computable.
        var contentLength = request.Content?.Headers.ContentLength;
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!.AbsolutePath, key, body, contentLength);

        lock (_requests) _requests.Add(recorded);
        return respond(recorded);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    public static HttpResponseMessage LoginOk(string key)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Add(AppConstants.SandcastleKeyHeader, key);
        return response;
    }

    public static bool IsLogin(RecordedRequest r) => r.Method == HttpMethod.Post && r.Path.EndsWith("/auth/context");
}
