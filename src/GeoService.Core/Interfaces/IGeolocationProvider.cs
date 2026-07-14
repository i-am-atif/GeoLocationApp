using GeoService.Core.Models;

namespace GeoService.Core.Interfaces;

/// <summary>
/// Contract that every geolocation provider must implement.
/// Adding a new provider requires only:
///   1. Creating a class that implements this interface.
///   2. Adding its configuration section.
///   3. Registering it in DI.
/// No changes to orchestration logic are needed.
/// </summary>
public interface IGeolocationProvider
{
    /// <summary>Human-readable name used in logs and failure responses.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Attempt to resolve geolocation for the given IP address.
    /// </summary>
    /// <param name="ipAddress">IPv4 or IPv6 address string.</param>
    /// <param name="cancellationToken">Propagated cancellation.</param>
    /// <returns>A populated <see cref="GeolocationResult"/> on success.</returns>
    /// <exception cref="GeolocationProviderException">
    /// Thrown on any provider-level failure (HTTP error, parse error, rate-limit, etc.).
    /// </exception>
    Task<GeolocationResult> LookupAsync(string ipAddress, CancellationToken cancellationToken = default);
}
