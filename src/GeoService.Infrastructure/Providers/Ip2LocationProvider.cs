using System.Text.Json;
using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Core.Models;
using GeoService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeoService.Infrastructure.Providers;

/// <summary>
/// IP2Location.io geolocation provider.
/// Docs: https://www.ip2location.io/ip2location-documentation
/// </summary>
public sealed class Ip2LocationProvider : IGeolocationProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<Ip2LocationProvider> _logger;
    private readonly ProviderOptions _options;

    public string ProviderName => "IP2Location";

    public Ip2LocationProvider(
        HttpClient http,
        IOptions<GeolocationProvidersOptions> options,
        ILogger<Ip2LocationProvider> logger)
    {
        _http = http;
        _logger = logger;
        _options = options.Value.Ip2Location;
    }

    public async Task<GeolocationResult> LookupAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.BaseUrl ?? "https://api.ip2location.io";
        var url = $"{baseUrl}/?key={_options.ApiKey}&ip={Uri.EscapeDataString(ipAddress)}&format=json";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GeolocationProviderException(ProviderName, $"HTTP request failed: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new GeolocationProviderException(
                ProviderName,
                $"Provider returned HTTP {(int)response.StatusCode}");
        }

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return MapResponse(doc.RootElement);
        }
        catch (JsonException ex)
        {
            throw new GeolocationProviderException(ProviderName, $"Failed to parse response: {ex.Message}", ex);
        }
    }

    private static GeolocationResult MapResponse(JsonElement root)
    {
        // IP2Location.io response shape:
        // { country_name, region_name, city_name, zip_code,
        //   latitude, longitude, time_zone, as (ISP/AS name), currency_code }
        return new GeolocationResult
        {
            Country  = GetString(root, "country_name"),
            State    = GetString(root, "region_name"),
            City     = GetString(root, "city_name"),
            Zipcode  = GetString(root, "zip_code"),
            Coordinates = new Coordinates
            {
                Lat = root.TryGetProperty("latitude",  out var lat) ? lat.GetDouble() : 0,
                Lng = root.TryGetProperty("longitude", out var lng) ? lng.GetDouble() : 0,
            },
            TimeZone = GetString(root, "time_zone"),
            Isp      = GetString(root, "as"),        // "as" field contains AS org name
            Currency = GetString(root, "currency_code"),
        };
    }

    private static string GetString(JsonElement el, string property)
        => el.TryGetProperty(property, out var v) ? v.GetString() ?? "" : "";
}
