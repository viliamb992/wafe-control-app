using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecuperationSystem.Desktop.Services;
using RecuperationSystem.Desktop.ViewModels;
using RecuperationSystem.Shared;
using RecuperationSystem.Shared.Services;

namespace RecuperationSystem.Desktop.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure options
        services.Configure<PollingConfiguration>(configuration.GetSection("Polling"));
        
        // Register shared services — HttpClient is managed by IHttpClientFactory
        services.AddTransient<RetryHandler>();
        services.AddHttpClient<IWafeApiService, WafeApiService>(client =>
        {
            client.BaseAddress = new Uri(AppConstants.WafeApiBaseUrl);
        }).AddHttpMessageHandler<RetryHandler>();
        
        // Register desktop services
        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<ISystemControlService, SystemControlService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        
        // Register ViewModels
        services.AddTransient<AppViewModel>();
        
        return services;
    }
}
