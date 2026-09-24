using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WafeControl.Core;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels;
using WafeControl.Tests.Helpers;

namespace WafeControl.Tests;

public class ServiceRegistrationTests
{
    private static ServiceProvider BuildProvider(StubHttpHandler stub)
    {
        var services = new ServiceCollection();
        services.AddWafeControlCore(polling => polling.StateChangeIntervalMs = 10);
        services.AddSingleton(Substitute.For<ICredentialStore>());
        services.AddSingleton(Substitute.For<ISettingsStore>());
        services.AddSingleton(Substitute.For<IStartupRegistration>());
        services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => stub));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public async Task LoginThroughAuthService_KeyIsUsedByStatusRequests()
    {
        // Regression: the API client is transient, so each service used to get its own instance and
        // the Sandcastle-Key stored by the login never reached the status requests.
        var stub = new StubHttpHandler(r => StubHttpHandler.IsLogin(r)
            ? StubHttpHandler.LoginOk("k1")
            : StubHttpHandler.Json("""{"gen":1,"temperatures":[20.0],"authority":"manual"}"""));
        await using var provider = BuildProvider(stub);

        var auth = provider.GetRequiredService<IAuthenticationService>();
        var systemControl = provider.GetRequiredService<ISystemControlService>();

        Assert.True(await auth.LoginAsync("alice", "secret", rememberMe: false, TestContext.Current.CancellationToken));
        var status = await systemControl.RefreshStatusAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(status);
        Assert.Equal("manual", status.Authority);
        // A refresh reads /main and /header; both carry the key.
        var statusRequests = stub.Requests.Where(r => !StubHttpHandler.IsLogin(r)).ToList();
        Assert.Equal(2, statusRequests.Count);
        Assert.Contains(statusRequests, r => r.Path.EndsWith("/v1/main"));
        Assert.Contains(statusRequests, r => r.Path.EndsWith("/v1/header"));
        Assert.All(statusRequests, r => Assert.Equal("k1", r.SandcastleKey));
    }

    [Fact]
    public async Task AppViewModel_ResolvesAsSingleton()
    {
        await using var provider = BuildProvider(new StubHttpHandler(_ => StubHttpHandler.Json("{}")));

        var first = provider.GetRequiredService<AppViewModel>();
        var second = provider.GetRequiredService<AppViewModel>();

        Assert.Same(first, second);
    }
}
