using System.Net;
using System.Text.Json;
using FluentAssertions;
using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using GeoService.Tests.Helpers;
using Xunit;

namespace GeoService.Tests;

/// <summary>
/// Integration tests for the HTTP endpoints.
/// Uses WebApplicationFactory with a replaced orchestrator — no live providers involved.
/// </summary>
public sealed class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient MakeClient(IGeolocationOrchestrator orchestrator)
        => _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(orchestrator);
            }))
            .CreateClient();

    // ──────────────────────────────────────────────────────────────────────────
    // 200 OK
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returns_200_with_geolocation_json_on_success()
    {
        var expected = TestData.SampleResult();
        var orch     = new StubOrchestrator(expected);
        var client   = MakeClient(orch);

        var response = await client.GetAsync("/api/geolocation/8.8.8.8");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("country").GetString().Should().Be("United States");
        doc.RootElement.GetProperty("city").GetString().Should().Be("Los Angeles");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 400 Bad Request for invalid IP
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("999.999.999.999")]
    [InlineData("hostname.example.com")]
    public async Task Returns_400_for_invalid_ip(string badIp)
    {
        var orch   = new StubOrchestrator(TestData.SampleResult());
        var client = MakeClient(orch);

        var response = await client.GetAsync($"/api/geolocation/{badIp}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 503 Service Unavailable when all providers fail
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returns_503_when_all_providers_fail()
    {
        var orch   = new FailingOrchestrator(["Geoapify", "IPStack", "IP2Location"]);
        var client = MakeClient(orch);

        var response = await client.GetAsync("/api/geolocation/8.8.8.8");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Geoapify");
        body.Should().NotContain("ApiKey",    "API keys must never be exposed");
        body.Should().NotContain("api_key",   "API keys must never be exposed");
        body.Should().NotContain("access_key","API keys must never be exposed");
    }

    [Fact]
    public async Task Failure_response_lists_attempted_providers()
    {
        var providers = new[] { "Geoapify", "IPStack", "IP2Location" };
        var orch      = new FailingOrchestrator(providers);
        var client    = MakeClient(orch);

        var response = await client.GetAsync("/api/geolocation/1.2.3.4");
        var body     = await response.Content.ReadAsStringAsync();

        foreach (var p in providers)
            body.Should().Contain(p);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Stub orchestrators (no real providers)
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class StubOrchestrator(GeolocationResult result) : IGeolocationOrchestrator
    {
        public Task<GeolocationResult> GetLocationAsync(string ip, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    private sealed class FailingOrchestrator(IReadOnlyList<string> providers) : IGeolocationOrchestrator
    {
        public Task<GeolocationResult> GetLocationAsync(string ip, CancellationToken ct = default)
            => throw new AllProvidersFailedException(providers);
    }
}
