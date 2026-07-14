using GeoService.Core.Interfaces;
using GeoService.Infrastructure.Configuration;
using GeoService.Infrastructure.Orchestration;
using GeoService.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GeoService.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all geolocation infrastructure services.
    ///
    /// To ADD a new provider:
    ///   1. Implement IGeolocationProvider in a new class.
    ///   2. Add its ProviderOptions property to GeolocationProvidersOptions and
    ///      its case to GetByName().
    ///   3. Add the provider's configuration section to appsettings.json.
    ///   4. Add services.AddHttpClient&lt;YourProvider&gt;() and
    ///      services.AddSingleton&lt;IGeolocationProvider, YourProvider&gt;() below.
    ///   No orchestration code changes are needed.
    ///
    /// To DISABLE a provider: set "Enabled": false in its configuration section.
    ///   The Enabled flag is read from ProviderOptions — no name-based switch needed.
    ///
    /// To REMOVE a provider: delete its class and remove its two registration lines.
    /// </summary>
    public static IServiceCollection AddGeolocationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind typed options (API keys loaded from config / env vars — never hardcoded)
        services.Configure<GeolocationProvidersOptions>(
            configuration.GetSection(GeolocationProvidersOptions.SectionName));

        // Register named HttpClients for each provider
        services.AddHttpClient<GeoapifyProvider>();
        services.AddHttpClient<IpStackProvider>();
        services.AddHttpClient<Ip2LocationProvider>();

        // Register all providers. Order here = round-robin order.
        services.AddSingleton<IGeolocationProvider>(sp =>
            ActivatorUtilities.CreateInstance<GeoapifyProvider>(sp));
        services.AddSingleton<IGeolocationProvider>(sp =>
            ActivatorUtilities.CreateInstance<IpStackProvider>(sp));
        services.AddSingleton<IGeolocationProvider>(sp =>
            ActivatorUtilities.CreateInstance<Ip2LocationProvider>(sp));

        // Build the active provider list by reading the Enabled flag from each
        // provider's own ProviderOptions — no hardcoded name switch here.
        services.AddSingleton<IReadOnlyList<IGeolocationProvider>>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GeolocationProvidersOptions>>().Value;
            var all  = sp.GetServices<IGeolocationProvider>();

            var enabled = all
                .Where(p =>
                {
                    var providerOpts = opts.GetByName(p.ProviderName);
                    // Unknown providers (e.g. added dynamically in tests) default to enabled
                    return providerOpts?.Enabled ?? true;
                })
                .ToList()
                .AsReadOnly();

            return enabled;
        });

        // Thread-safe round-robin counter — singleton so state persists across requests
        services.AddSingleton<RoundRobinCounter>();

        // Orchestrator depends on the filtered IReadOnlyList, not raw IEnumerable
        services.AddSingleton<IGeolocationOrchestrator>(sp =>
        {
            var providers = sp.GetRequiredService<IReadOnlyList<IGeolocationProvider>>();
            var counter   = sp.GetRequiredService<RoundRobinCounter>();
            var logger    = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GeolocationOrchestrator>>();
            return new GeolocationOrchestrator(providers, counter, logger);
        });

        return services;
    }
}
