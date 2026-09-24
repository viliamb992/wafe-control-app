using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using WafeControl.Shared;
using WafeControl.Shared.Services;
using WafeControl.Shared.Services.Http;
using WafeControl.Tests.Helpers;

namespace WafeControl.Tests.Services;

public class WafeApiServiceTests
{
    private readonly WafeSession _session = new();

    private WafeApiService CreateSut(StubHttpHandler stub)
    {
        var handler = new SandcastleAuthHandler(_session, NullLogger<SandcastleAuthHandler>.Instance) { InnerHandler = stub };
        var client = new HttpClient(handler) { BaseAddress = new Uri(AppConstants.WafeApiBaseUrl) };
        return new WafeApiService(client, _session, NullLogger<WafeApiService>.Instance);
    }

    private static StubHttpHandler Ok() => new(_ => StubHttpHandler.Json("{}"));

    // ── AuthenticateAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_KeyHeader_StartsSessionWithCredentials()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.LoginOk("k1"));

        var result = await CreateSut(stub).AuthenticateAsync("alice", "secret", TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal("k1", _session.Key);
        Assert.Equal(("alice", "secret"), _session.Credentials);
        var login = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Equal("/api/auth/context", login.Path);
        Assert.Equal("""{"username":"alice","password":"secret"}""", login.Body);
    }

    [Fact]
    public async Task RequestBodies_HaveContentLength()
    {
        // Regression: the Wafe server answers chunked request bodies (JsonContent's default) with 500.
        var stub = new StubHttpHandler(r => StubHttpHandler.IsLogin(r) ? StubHttpHandler.LoginOk("k1") : StubHttpHandler.Json("{}"));
        var sut = CreateSut(stub);

        await sut.AuthenticateAsync("alice", "secret", TestContext.Current.CancellationToken);
        await sut.SetFlowSpeedAsync(150, TestContext.Current.CancellationToken);

        Assert.All(stub.Requests, r => Assert.Equal(r.Body!.Length, r.ContentLength));
    }

    [Fact]
    public async Task AuthenticateAsync_KeyOnlyInSetCookie_UsesCookieValue()
    {
        var stub = new StubHttpHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Add("Set-Cookie", "Sandcastle-Key=k2; Path=/; HttpOnly");
            return response;
        });

        var result = await CreateSut(stub).AuthenticateAsync("alice", "secret", TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal("k2", _session.Key);
    }

    [Fact]
    public async Task AuthenticateAsync_Rejected_ReturnsFalseAndLeavesSessionEmpty()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await CreateSut(stub).AuthenticateAsync("alice", "wrong", TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Null(_session.Key);
    }

    [Fact]
    public async Task AuthenticateAsync_SuccessWithoutKey_ReturnsFalse()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));

        Assert.False(await CreateSut(stub).AuthenticateAsync("alice", "secret", TestContext.Current.CancellationToken));
    }

    // ── GetMainStatusAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMainStatusAsync_DeserializesKebabCaseResponse()
    {
        const string json = """
            {
              "gen": 42,
              "temperatures": [4.5, null, 21.3, 8.0],
              "humidity": 45.5,
              "co2": 640,
              "flow-min": 50, "flow-max": 220, "flow-requested": 140, "flow-current": 138,
              "authority": "manual",
              "authority-available": ["intelligent", "manual"],
              "boost-remaining": 600,
              "silent-active": true,
              "holiday-active": false,
              "stop-active": false,
              "filters": { "fresh": { "health": 82, "status": "ok" }, "waste": { "health": 71, "status": "ok" } }
            }
            """;
        _session.Start("alice", "secret", "k1");
        var stub = new StubHttpHandler(_ => StubHttpHandler.Json(json));

        var status = await CreateSut(stub).GetMainStatusAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(status);
        Assert.Equal(42, status.Gen);
        Assert.Equal([4.5, null, 21.3, 8.0], status.Temperatures!);
        Assert.Equal(640, status.Co2);
        Assert.Equal(140, status.FlowRequested);
        Assert.Equal(138, status.FlowCurrent);
        Assert.Equal("manual", status.Authority);
        Assert.Equal(["intelligent", "manual"], status.AuthorityAvailable!);
        Assert.Equal(600, status.BoostRemaining);
        Assert.True(status.SilentActive);
        Assert.True(status.IsSystemRunning);
        Assert.Equal(82, status.Filters?.Fresh?.Health);
        Assert.Equal("k1", Assert.Single(stub.Requests).SandcastleKey);
    }

    [Fact]
    public async Task GetMainStatusAsync_ServerError_ReturnsNull()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Assert.Null(await CreateSut(stub).GetMainStatusAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMainStatusAsync_MalformedJson_ReturnsNull()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Json("not json"));

        Assert.Null(await CreateSut(stub).GetMainStatusAsync(TestContext.Current.CancellationToken));
    }

    // ── PUT endpoints ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetFlowSpeedAsync_SendsPutWithValueBody()
    {
        _session.Start("alice", "secret", "k1");
        var stub = Ok();

        var result = await CreateSut(stub).SetFlowSpeedAsync(150, TestContext.Current.CancellationToken);

        Assert.True(result);
        var put = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.EndsWith("/api/v1/main/flow-requested", put.Path);
        Assert.Equal("""{"value":150}""", put.Body);
        Assert.Equal("k1", put.SandcastleKey);
    }

    [Theory]
    [InlineData(AppConstants.MinFlowSpeed - 1)]
    [InlineData(AppConstants.MaxFlowSpeed + 1)]
    public async Task SetFlowSpeedAsync_OutOfRange_ReturnsFalseWithoutRequest(int speed)
    {
        var stub = Ok();

        Assert.False(await CreateSut(stub).SetFlowSpeedAsync(speed, TestContext.Current.CancellationToken));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task SetAuthorityModeAsync_SendsStringValue()
    {
        var stub = Ok();

        await CreateSut(stub).SetAuthorityModeAsync("manual", TestContext.Current.CancellationToken);

        Assert.Equal("""{"value":"manual"}""", Assert.Single(stub.Requests).Body);
    }

    [Fact]
    public async Task SetStopActiveAsync_SendsBooleanValue()
    {
        var stub = Ok();

        await CreateSut(stub).SetStopActiveAsync(true, TestContext.Current.CancellationToken);

        var put = Assert.Single(stub.Requests);
        Assert.EndsWith("/api/v1/main/stop-active", put.Path);
        Assert.Equal("""{"value":true}""", put.Body);
    }

    [Fact]
    public async Task GetScheduleAsync_ParsesModesAndPlan()
    {
        var stub = new StubHttpHandler(_ => StubHttpHandler.Json("""{"modes":["min","auto","nom","boost"],"plan":"boost-0:2:0-0:3:0"}"""));

        var schedule = await CreateSut(stub).GetScheduleAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(schedule);
        Assert.Equal(["min", "auto", "nom", "boost"], schedule.Modes);
        Assert.Equal("boost-0:2:0-0:3:0", schedule.Plan);
        Assert.Equal("/api/api/v1/schedule", Assert.Single(stub.Requests).Path);
    }

    [Fact]
    public async Task SetSchedulePlanAsync_PutsWholePlanAsValue()
    {
        var stub = Ok();

        Assert.True(await CreateSut(stub).SetSchedulePlanAsync("boost-0:2:0-0:3:0 auto-0:7:0-0:7:30", TestContext.Current.CancellationToken));

        var put = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal("/api/api/v1/schedule/plan", put.Path);
        Assert.Equal("""{"value":"boost-0:2:0-0:3:0 auto-0:7:0-0:7:30"}""", put.Body);
    }

    [Fact]
    public async Task SetUnitNameAsync_PutsNameAsValue()
    {
        var stub = Ok();

        Assert.True(await CreateSut(stub).SetUnitNameAsync("Chata", TestContext.Current.CancellationToken));

        var put = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Equal("/api/api/v1/header/name", put.Path);
        Assert.Equal("""{"value":"Chata"}""", put.Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567890123456789")]  // 29 characters
    public async Task SetUnitNameAsync_EmptyOrTooLong_ReturnsFalseWithoutRequest(string name)
    {
        var stub = Ok();

        Assert.False(await CreateSut(stub).SetUnitNameAsync(name, TestContext.Current.CancellationToken));
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task PutEndpoint_Rejected_ReturnsFalse()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        Assert.False(await CreateSut(stub).SetBoostAsync(900, TestContext.Current.CancellationToken));
    }
}
