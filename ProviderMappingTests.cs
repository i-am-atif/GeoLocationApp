using System.Net;
using System.Text;
using FluentAssertions;
using GeoService.Core;
using GeoService.Infrastructure.Configuration;
using GeoService.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GeoService.Tests;

/// <summary>
/// Tests that each provider correctly maps its own API response format
/// into the shared GeolocationResult model. Uses a stub HttpMessageHandler —
/// no live API calls are made.
/// </summary>
public sealed class ProviderMappingTests
{
    private static IOptions<GeolocationProvidersOptions> DefaultOptions(
        string? baseUrl = null,
        string apiKey   = "test-key") => Options.Create(new GeolocationProvidersOptions
        {
            Geoapify    = new ProviderOptions { ApiKey = apiKey, BaseUrl = baseUrl },
            IpStack     = new ProviderOptions { ApiKey = apiKey, BaseUrl = baseUrl },
            Ip2Location = new ProviderOptions { ApiKey = apiKey, BaseUrl = baseUrl },
        });

    private static HttpClient MakeClient(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(json, status);
        return new HttpClient(handler);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Geoapify
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Geoapify_maps_response_to_GeolocationResult()
    {
        const string json = """
        {
            "country":  { "name": "United States", "currency": { "code": "USD" } },
            "state":    { "name": "California" },
            "city":     { "name": "Los Angeles" },
            "postcode": "90006",
            "location": { "latitude": 34.048, "longitude": -118.292 },
            "timezone": { "name": "America/Los_Angeles" },
            "connection": { "organization": "LA DWP" }
        }
        """;

        var provider = new GeoapifyProvider(
            MakeClient(json), DefaultOptions(), NullLogger<GeoapifyProvider>.Instance);

        var result = await provider.LookupAsync("8.8.8.8");

        result.Country.Should().Be("United States");
        result.State.Should().Be("California");
        result.City.Should().Be("Los Angeles");
        result.Zipcode.Should().Be("90006");
        result.Coordinates.Lat.Should().BeApproximately(34.048, 0.001);
        result.Coordinates.Lng.Should().BeApproximately(-118.292, 0.001);
        result.TimeZone.Should().Be("America/Los_Angeles");
        result.Isp.Should().Be("LA DWP");
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Geoapify_throws_on_non_success_status()
    {
        var provider = new GeoapifyProvider(
            MakeClient("{}", HttpStatusCode.Unauthorized),
            DefaultOptions(),
            NullLogger<GeoapifyProvider>.Instance);

        await Assert.ThrowsAsync<GeolocationProviderException>(
            () => provider.LookupAsync("1.2.3.4"));
    }

    [Fact]
    public async Task Geoapify_throws_on_malformed_json()
    {
        var provider = new GeoapifyProvider(
            MakeClient("NOT_JSON"),
            DefaultOptions(),
            NullLogger<GeoapifyProvider>.Instance);

        await Assert.ThrowsAsync<GeolocationProviderException>(
            () => provider.LookupAsync("1.2.3.4"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IPStack
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IpStack_maps_response_to_GeolocationResult()
    {
        const string json = """
        {
            "country_name": "United States",
            "region_name":  "California",
            "city":         "Los Angeles",
            "zip":          "90006",
            "latitude":     34.048,
            "longitude":    -118.292,
            "time_zone":    { "id": "America/Los_Angeles" },
            "connection":   { "isp": "LA DWP" },
            "currency":     { "code": "USD" }
        }
        """;

        var provider = new IpStackProvider(
            MakeClient(json), DefaultOptions(), NullLogger<IpStackProvider>.Instance);

        var result = await provider.LookupAsync("8.8.8.8");

        result.Country.Should().Be("United States");
        result.State.Should().Be("California");
        result.City.Should().Be("Los Angeles");
        result.Zipcode.Should().Be("90006");
        result.Coordinates.Lat.Should().BeApproximately(34.048, 0.001);
        result.Coordinates.Lng.Should().BeApproximately(-118.292, 0.001);
        result.TimeZone.Should().Be("America/Los_Angeles");
        result.Isp.Should().Be("LA DWP");
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task IpStack_throws_when_API_returns_success_false()
    {
        const string json = """
        { "success": false, "error": { "info": "Invalid access key" } }
        """;

        var provider = new IpStackProvider(
            MakeClient(json), DefaultOptions(), NullLogger<IpStackProvider>.Instance);

        var ex = await Assert.ThrowsAsync<GeolocationProviderException>(
            () => provider.LookupAsync("1.2.3.4"));

        ex.Message.Should().Contain("Invalid access key");
    }

    [Fact]
    public async Task IpStack_throws_on_non_success_http_status()
    {
        var provider = new IpStackProvider(
            MakeClient("{}", HttpStatusCode.TooManyRequests),
            DefaultOptions(),
            NullLogger<IpStackProvider>.Instance);

        await Assert.ThrowsAsync<GeolocationProviderException>(
            () => provider.LookupAsync("1.2.3.4"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IP2Location
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ip2Location_maps_response_to_GeolocationResult()
    {
        const string json = """
        {
            "country_name": "United States",
            "region_name":  "California",
            "city_name":    "Los Angeles",
            "zip_code":     "90006",
            "latitude":     34.048,
            "longitude":    -118.292,
            "time_zone":    "+08:00",
            "as":           "AS11492 LA DWP",
            "currency_code":"USD"
        }
        """;

        var provider = new Ip2LocationProvider(
            MakeClient(json), DefaultOptions(), NullLogger<Ip2LocationProvider>.Instance);

        var result = await provider.LookupAsync("8.8.8.8");

        result.Country.Should().Be("United States");
        result.State.Should().Be("California");
        result.City.Should().Be("Los Angeles");
        result.Zipcode.Should().Be("90006");
        result.Coordinates.Lat.Should().BeApproximately(34.048, 0.001);
        result.Coordinates.Lng.Should().BeApproximately(-118.292, 0.001);
        result.TimeZone.Should().Be("+08:00");
        result.Isp.Should().Be("AS11492 LA DWP");
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Ip2Location_throws_on_http_error()
    {
        var provider = new Ip2LocationProvider(
            MakeClient("{}", HttpStatusCode.ServiceUnavailable),
            DefaultOptions(),
            NullLogger<Ip2LocationProvider>.Instance);

        await Assert.ThrowsAsync<GeolocationProviderException>(
            () => provider.LookupAsync("1.2.3.4"));
    }
}

/// <summary>Stub HttpMessageHandler that returns a fixed response.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly string _body;
    private readonly HttpStatusCode _status;

    public StubHttpMessageHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _body   = body;
        _status = status;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
}
