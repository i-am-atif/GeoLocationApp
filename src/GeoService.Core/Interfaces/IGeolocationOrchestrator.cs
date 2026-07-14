using GeoService.Core.Models;

namespace GeoService.Core.Interfaces;

/// <summary>
/// Orchestrates provider selection (round-robin), retry, and fallback logic.
/// </summary>
public interface IGeolocationOrchestrator
{
    /// <summary>
    /// Returns geolocation data for <paramref name="ipAddress"/>, trying providers
    /// in round-robin order with one retry per provider before falling back.
    /// </summary>
    Task<GeolocationResult> GetLocationAsync(string ipAddress, CancellationToken cancellationToken = default);
}
