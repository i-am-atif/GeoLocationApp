namespace GeoService.Core.Models;

/// <summary>
/// Unified geolocation response returned to callers regardless of which provider served the request.
/// </summary>
public sealed class GeolocationResult
{
    public string Country { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Zipcode { get; init; } = string.Empty;
    public Coordinates Coordinates { get; init; } = new();
    public string TimeZone { get; init; } = string.Empty;
    public string Isp { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
}

public sealed class Coordinates
{
    public double Lat { get; init; }
    public double Lng { get; init; }
}
