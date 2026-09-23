using Microsoft.Extensions.DependencyInjection;
using RecuperationSystem.Core.Configuration;
using RecuperationSystem.Core.Localization;
using RecuperationSystem.Core.Services;
using RecuperationSystem.Core.ViewModels;
using RecuperationSystem.Core.ViewModels.Schedule;
using RecuperationSystem.Shared;
using RecuperationSystem.Shared.Services;
using RecuperationSystem.Shared.Services.Http;

namespace RecuperationSystem.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Wafe API client, session handling, services and view models shared by every app.
    /// The app must also register an <see cref="ICredentialStore"/>, an <see cref="ISettingsStore"/> and an
    /// <see cref="IStartupRegistration"/> for its platform, and call <see cref="LocalizationService.Initialize"/>
    /// before creating its UI.
    /// </summary>
    public static IServiceCollection AddRecuperationCore(
        this IServiceCollection services,
        Action<PollingConfiguration>? configurePolling = null)
    {
        services.AddLogging();

        var polling = services.AddOptions<PollingConfiguration>();
        if (configurePolling is not null)
            polling.Configure(configurePolling);

        // The session is the single owner of the Sandcastle-Key; API clients are transient and stateless.
        services.AddSingleton<WafeSession>();
        services.AddTransient<SandcastleAuthHandler>();

        // Handler order is outer → inner: resilience retries wrap the auth handler, so every attempt carries the current key.
        var apiClient = services.AddHttpClient<IWafeApiService, WafeApiService>(client =>
        {
            client.BaseAddress = new Uri(AppConstants.WafeApiBaseUrl);
        });
        apiClient.AddStandardResilienceHandler();
        apiClient.AddHttpMessageHandler<SandcastleAuthHandler>();

        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<ISystemControlService, SystemControlService>();

        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());

        services.AddSingleton<AppViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<SettingsViewModel>();

        return services;
    }
}
