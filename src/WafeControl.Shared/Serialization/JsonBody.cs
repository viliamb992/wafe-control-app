using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace WafeControl.Shared.Serialization;

/// <summary>
/// JSON request bodies for the Wafe API.
/// </summary>
internal static class JsonBody
{
    private static readonly MediaTypeHeaderValue JsonMediaType = new("application/json") { CharSet = "utf-8" };

    /// <summary>
    /// Serializes up front so the request carries a Content-Length. The Wafe server can't read chunked
    /// bodies (what <c>JsonContent</c>/<c>PostAsJsonAsync</c> send) and answers them with 500.
    /// </summary>
    public static ByteArrayContent Create<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(value, typeInfo));
        content.Headers.ContentType = JsonMediaType;
        return content;
    }
}
