namespace GeoService.Core.Models;

/// <summary>
/// Returned when all providers have been exhausted. Contains enough context for
/// the caller to understand what happened without leaking API keys or raw errors.
/// </summary>
public sealed class GeolocationFailure
{
    public string Message { get; init; } = "Geolocation lookup failed. All providers were attempted and returned errors.";
    public IReadOnlyList<string> ProvidersAttempted { get; init; } = Array.Empty<string>();
}
