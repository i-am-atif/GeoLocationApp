using System.Text.Json;
using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Core.Models;
using GeoService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeoService.Infrastructure.Providers;

/// <summary>
/// Geoapify IP Geolocation provider.
/// Docs: https://apidocs.geoapify.com/docs/ip-geolocation/
/// </summary>
public sealed class GeoapifyProvider : IGeolocationProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<GeoapifyProvider> _logger;
    private readonly ProviderOptions _options;

    public string ProviderName => "Geoapify";

    public GeoapifyProvider(
        HttpClient http,
        IOptions<GeolocationProvidersOptions> options,
        ILogger<GeoapifyProvider> logger)
    {
        _http = http;
        _logger = logger;
        _options = options.Value.Geoapify;
    }

    public async Task<GeolocationResult> LookupAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.BaseUrl ?? "https://api.geoapify.com";
        var url = $"{baseUrl}/v1/ipinfo?ip={Uri.EscapeDataString(ipAddress)}&apiKey={_options.ApiKey}";

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
        // Geoapify response shape:
        // { country: { name, currency: { code } }, city: { name }, state: { name },
        //   postcode, location: { latitude, longitude }, timezone: { name },
        //   connection: { organization } }
        var country = root.TryGetProperty("country", out var c) ? c : default;
        var city    = root.TryGetProperty("city",    out var ci) ? ci : default;
        var state   = root.TryGetProperty("state",   out var s) ? s : default;
        var loc     = root.TryGetProperty("location", out var l) ? l : default;
        var tz      = root.TryGetProperty("timezone", out var t) ? t : default;
        var conn    = root.TryGetProperty("connection", out var cn) ? cn : default;

        string currency = string.Empty;
        if (country.ValueKind == JsonValueKind.Object &&
            country.TryGetProperty("currency", out var cur) &&
            cur.TryGetProperty("code", out var code))
        {
            currency = code.GetString() ?? string.Empty;
        }

        return new GeolocationResult
        {
            Country = country.ValueKind == JsonValueKind.Object
                ? country.TryGetProperty("name", out var cn2) ? cn2.GetString() ?? "" : ""
                : "",
            State   = state.ValueKind == JsonValueKind.Object
                ? state.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : ""
                : "",
            City    = city.ValueKind == JsonValueKind.Object
                ? city.TryGetProperty("name", out var cityName) ? cityName.GetString() ?? "" : ""
                : "",
            Zipcode = root.TryGetProperty("postcode", out var zip) ? zip.GetString() ?? "" : "",
            Coordinates = new Coordinates
            {
                Lat = loc.ValueKind == JsonValueKind.Object && loc.TryGetProperty("latitude",  out var lat) ? lat.GetDouble() : 0,
                Lng = loc.ValueKind == JsonValueKind.Object && loc.TryGetProperty("longitude", out var lng) ? lng.GetDouble() : 0,
            },
            TimeZone = tz.ValueKind == JsonValueKind.Object
                ? tz.TryGetProperty("name", out var tzName) ? tzName.GetString() ?? "" : ""
                : "",
            Isp      = conn.ValueKind == JsonValueKind.Object
                ? conn.TryGetProperty("organization", out var org) ? org.GetString() ?? "" : ""
                : "",
            Currency = currency,
        };
    }
}
