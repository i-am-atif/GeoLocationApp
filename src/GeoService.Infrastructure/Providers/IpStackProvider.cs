using System.Text.Json;
using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Core.Models;
using GeoService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeoService.Infrastructure.Providers;

/// <summary>
/// IPStack geolocation provider.
/// Docs: https://ipstack.com/documentation
/// Note: free tier requires HTTP (not HTTPS). Switch BaseUrl to https for paid plans.
/// </summary>
public sealed class IpStackProvider : IGeolocationProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<IpStackProvider> _logger;
    private readonly ProviderOptions _options;

    public string ProviderName => "IPStack";

    public IpStackProvider(
        HttpClient http,
        IOptions<GeolocationProvidersOptions> options,
        ILogger<IpStackProvider> logger)
    {
        _http = http;
        _logger = logger;
        _options = options.Value.IpStack;
    }

    public async Task<GeolocationResult> LookupAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        // Free tier only supports HTTP
        var baseUrl = _options.BaseUrl ?? "http://api.ipstack.com";
        var url = $"{baseUrl}/{Uri.EscapeDataString(ipAddress)}?access_key={_options.ApiKey}";

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
        // IPStack returns an error object { success: false, error: {...} } on failure
        if (root.TryGetProperty("success", out var success) && !success.GetBoolean())
        {
            var errorInfo = root.TryGetProperty("error", out var err)
                ? err.TryGetProperty("info", out var info) ? info.GetString() : null
                : null;
            throw new GeolocationProviderException("IPStack", $"API error: {errorInfo ?? "unknown"}");
        }

        // IPStack response shape:
        // { country_name, region_name, city, zip, latitude, longitude,
        //   time_zone: { id }, connection: { isp }, currency: { code } }
        return new GeolocationResult
        {
            Country  = GetString(root, "country_name"),
            State    = GetString(root, "region_name"),
            City     = GetString(root, "city"),
            Zipcode  = GetString(root, "zip"),
            Coordinates = new Coordinates
            {
                Lat = root.TryGetProperty("latitude",  out var lat) ? lat.GetDouble() : 0,
                Lng = root.TryGetProperty("longitude", out var lng) ? lng.GetDouble() : 0,
            },
            TimeZone = root.TryGetProperty("time_zone", out var tz) && tz.ValueKind == JsonValueKind.Object
                ? GetString(tz, "id")
                : "",
            Isp = root.TryGetProperty("connection", out var conn) && conn.ValueKind == JsonValueKind.Object
                ? GetString(conn, "isp")
                : "",
            Currency = root.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.Object
                ? GetString(cur, "code")
                : "",
        };
    }

    private static string GetString(JsonElement el, string property)
        => el.TryGetProperty(property, out var v) ? v.GetString() ?? "" : "";
}
