using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.ServiceDiscovery;

namespace NotesCommander.MauiServiceDefaults;

public static class ServiceDefaultExtensions
{
    public static IServiceCollection AddMauiServiceDefaults(this IServiceCollection services)
    {
        services.AddServiceDiscovery();

        services.ConfigureHttpClientDefaults(http =>
        {
            // Enable service discovery and default resilience policies for all HttpClient instances
            http.AddServiceDiscovery();
            http.AddStandardResilienceHandler();
        });

        return services;
    }
}
