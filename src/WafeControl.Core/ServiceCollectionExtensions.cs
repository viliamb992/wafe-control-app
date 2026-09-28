using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WafeControl.Core.Configuration;
using WafeControl.Core.Demo;
using WafeControl.Core.Localization;
using WafeControl.Core.Services;
using WafeControl.Core.ViewModels;
using WafeControl.Core.ViewModels.Schedule;
using WafeControl.Shared;
using WafeControl.Shared.Services;
using WafeControl.Shared.Services.Http;

namespace WafeControl.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Wafe API client, session handling, services and view models shared by every app.
    /// The app must also register an <see cref="ICredentialStore"/>, an <see cref="ISettingsStore"/> and an
    /// <see cref="IStartupRegistration"/> for its platform, and call <see cref="LocalizationService.Initialize"/>
    /// before creating its UI. It may replace <see cref="INetworkStatus"/>, <see cref="ICrashReports"/> and
    /// <see cref="IUpdateService"/>, which default to System.Net, no crash reports and no updates.
    /// </summary>
    public static IServiceCollection AddWafeControlCore(
        this IServiceCollection services,
        Action<PollingConfiguration>? configurePolling = null)
    {
        services.AddLogging();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<INetworkStatus, SystemNetworkStatus>();
        services.TryAddSingleton<ICrashReports, NoCrashReports>();
        services.TryAddSingleton<IUpdateService, NoUpdates>();

        var polling = services.AddOptions<PollingConfiguration>();
        if (configurePolling is not null)
            polling.Configure(configurePolling);

        // The session is the single owner of the Sandcastle-Key; API clients are transient and stateless.
        services.AddSingleton<WafeSession>();
        services.AddTransient<SandcastleAuthHandler>();

        // Handler order is outer → inner: resilience retries wrap the auth handler, so every attempt carries the current key.
        var apiClient = services.AddHttpClient<WafeApiService>(client =>
        {
            client.BaseAddress = new Uri(AppConstants.WafeApiBaseUrl);
        });
        apiClient.AddStandardResilienceHandler();
        apiClient.AddHttpMessageHandler<SandcastleAuthHandler>();

        // Everything talks to the API through the router, which switches to the demo unit in demo mode.
        services.AddSingleton<DemoWafeApi>();
        services.AddSingleton<IWafeApiService, DemoAwareWafeApi>();

        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<ISystemControlService, SystemControlService>();

        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());

        services.AddSingleton<AppViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<UpdateViewModel>();

        return services;
    }
}
