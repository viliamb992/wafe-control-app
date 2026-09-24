using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WafeControl.Shared;
using WafeControl.Shared.Services.Http;
using WafeControl.Tests.Helpers;

namespace WafeControl.Tests.Http;

public class SandcastleAuthHandlerTests
{
    private readonly WafeSession _session = new();

    private HttpClient CreateClient(StubHttpHandler stub) =>
        new(new SandcastleAuthHandler(_session, NullLogger<SandcastleAuthHandler>.Instance) { InnerHandler = stub })
        {
            BaseAddress = new Uri(AppConstants.WafeApiBaseUrl)
        };

    /// <summary>
    /// API that only accepts <paramref name="validKey"/> and issues <paramref name="loginKey"/> on login
    /// (null → login rejected).
    /// </summary>
    private static StubHttpHandler Api(string validKey, string? loginKey) => new(r =>
        StubHttpHandler.IsLogin(r)
            ? loginKey is null ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : StubHttpHandler.LoginOk(loginKey)
            : r.SandcastleKey == validKey ? StubHttpHandler.Json("{}") : new HttpResponseMessage(HttpStatusCode.Unauthorized));

    [Fact]
    public async Task Send_AttachesSessionKey()
    {
        _session.Start("alice", "secret", "k1");
        var stub = Api(validKey: "k1", loginKey: null);

        using var response = await CreateClient(stub).GetAsync(AppConstants.MainEndpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("k1", Assert.Single(stub.Requests).SandcastleKey);
    }

    [Fact]
    public async Task Send_WithoutSession_SendsNoKeyAndDoesNotRenew()
    {
        var stub = Api(validKey: "k1", loginKey: "k2");

        using var response = await CreateClient(stub).GetAsync(AppConstants.MainEndpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var request = Assert.Single(stub.Requests);
        Assert.Null(request.SandcastleKey);
    }

    [Fact]
    public async Task Send_KeyRejected_RenewsSessionAndRetriesWithNewKey()
    {
        _session.Start("alice", "secret", "expired");
        var stub = Api(validKey: "fresh", loginKey: "fresh");

        using var response = await CreateClient(stub).GetAsync(AppConstants.MainEndpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("fresh", _session.Key);
        Assert.Collection(stub.Requests,
            r => Assert.Equal("expired", r.SandcastleKey),
            r =>
            {
                Assert.True(StubHttpHandler.IsLogin(r));
                Assert.Null(r.SandcastleKey);
                Assert.Contains("\"username\":\"alice\"", r.Body);
            },
            r => Assert.Equal("fresh", r.SandcastleKey));
    }

    [Fact]
    public async Task Send_RenewalRejected_ExpiresSession()
    {
        _session.Start("alice", "changed-password", "expired");
        var expired = false;
        _session.Expired += (_, _) => expired = true;
        var stub = Api(validKey: "fresh", loginKey: null);

        using var response = await CreateClient(stub).GetAsync(AppConstants.MainEndpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(expired);
        Assert.Null(_session.Key);
        Assert.Null(_session.Credentials);
    }

    [Fact]
    public async Task Send_ConcurrentRejections_RenewOnlyOnce()
    {
        _session.Start("alice", "secret", "expired");
        var stub = Api(validKey: "fresh", loginKey: "fresh");
        var client = CreateClient(stub);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.GetAsync(AppConstants.MainEndpoint)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(stub.Requests, StubHttpHandler.IsLogin);
    }
}
