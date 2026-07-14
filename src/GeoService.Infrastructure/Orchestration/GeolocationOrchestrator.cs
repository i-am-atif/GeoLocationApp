using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeoService.Infrastructure.Orchestration;

/// <summary>
/// Implements round-robin provider selection with one retry per provider and
/// sequential fallback. Contains NO provider-specific logic — all provider
/// details are encapsulated in the <see cref="IGeolocationProvider"/> implementations.
/// </summary>
public sealed class GeolocationOrchestrator : IGeolocationOrchestrator
{
    private readonly IReadOnlyList<IGeolocationProvider> _providers;
    private readonly RoundRobinCounter _counter;
    private readonly ILogger<GeolocationOrchestrator> _logger;

    private const int RetriesPerProvider = 1; // one retry = two total attempts per provider

    public GeolocationOrchestrator(
        IEnumerable<IGeolocationProvider> providers,
        RoundRobinCounter counter,
        ILogger<GeolocationOrchestrator> logger)
    {
        _providers = providers.ToList().AsReadOnly();
        _counter   = counter;
        _logger    = logger;

        if (_providers.Count == 0)
            throw new InvalidOperationException("At least one geolocation provider must be registered.");
    }

    public async Task<GeolocationResult> GetLocationAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var startIndex = _counter.Next(_providers.Count);
        var attempted  = new List<string>(_providers.Count);

        for (int i = 0; i < _providers.Count; i++)
        {
            var provider = _providers[(startIndex + i) % _providers.Count];
            attempted.Add(provider.ProviderName);

            for (int attempt = 0; attempt <= RetriesPerProvider; attempt++)
            {
                var isRetry = attempt > 0;
                if (isRetry)
                {
                    _logger.LogWarning(
                        "Retrying provider {Provider} for IP {IpAddress} (attempt {Attempt}/{Max})",
                        provider.ProviderName, ipAddress, attempt + 1, RetriesPerProvider + 1);
                }
                else
                {
                    _logger.LogInformation(
                        "Trying provider {Provider} for IP {IpAddress}",
                        provider.ProviderName, ipAddress);
                }

                try
                {
                    var result = await provider.LookupAsync(ipAddress, cancellationToken);

                    _logger.LogInformation(
                        "Provider {Provider} succeeded for IP {IpAddress}",
                        provider.ProviderName, ipAddress);

                    return result;
                }
                catch (OperationCanceledException)
                {
                    throw; // Never swallow cancellation
                }
                catch (GeolocationProviderException ex)
                {
                    _logger.LogWarning(ex,
                        "Provider {Provider} failed on attempt {Attempt} for IP {IpAddress}: {Reason}",
                        provider.ProviderName, attempt + 1, ipAddress, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Unexpected error from provider {Provider} on attempt {Attempt} for IP {IpAddress}",
                        provider.ProviderName, attempt + 1, ipAddress);
                }
            }

            _logger.LogWarning(
                "Provider {Provider} exhausted for IP {IpAddress}. Falling back.",
                provider.ProviderName, ipAddress);
        }

        // All providers failed
        _logger.LogCritical(
            "All geolocation providers failed for IP {IpAddress}. Providers attempted: {Providers}",
            ipAddress, string.Join(", ", attempted));

        throw new AllProvidersFailedException(attempted);
    }
}
