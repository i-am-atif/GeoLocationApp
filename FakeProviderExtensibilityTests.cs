using FluentAssertions;
using GeoService.Core.Interfaces;
using GeoService.Infrastructure.Orchestration;
using GeoService.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GeoService.Tests;

/// <summary>
/// Demonstrates that a brand-new provider (FakeProvider) slots directly into
/// the orchestrator without any changes to orchestration logic.
/// This satisfies the extensibility requirement:
///   "Adding a new provider should ideally require only adding a new provider
///    implementation, adding its configuration, and registering it."
/// </summary>
public sealed class FakeProviderExtensibilityTests
{
    private const string TestIp = "1.1.1.1";

    [Fact]
    public async Task New_fake_provider_works_in_orchestrator_without_code_changes()
    {
        var expected     = TestData.SampleResult("Sydney");
        var fakeProvider = new FakeProvider("AcmeGeo").ThenReturn(expected);

        // Plug the brand-new provider in — orchestrator code is untouched
        var sut    = new GeolocationOrchestrator(
            new IGeolocationProvider[] { fakeProvider },
            new RoundRobinCounter(),
            NullLogger<GeolocationOrchestrator>.Instance);
        var result = await sut.GetLocationAsync(TestIp);

        result.City.Should().Be("Sydney");
        fakeProvider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Fake_provider_participates_in_round_robin_naturally()
    {
        var real = new FakeProvider("RealProvider").ThenReturn(TestData.SampleResult("Tokyo"));
        var fake = new FakeProvider("AcmeGeo").ThenReturn(TestData.SampleResult("Sydney"));

        var counter = new RoundRobinCounter();
        var sut     = new GeolocationOrchestrator(
            new IGeolocationProvider[] { real, fake },
            counter,
            NullLogger<GeolocationOrchestrator>.Instance);

        var r1 = await sut.GetLocationAsync(TestIp);
        var r2 = await sut.GetLocationAsync(TestIp);

        r1.City.Should().Be("Tokyo");   // started at real
        r2.City.Should().Be("Sydney");  // started at fake
    }

    [Fact]
    public async Task Fake_provider_participates_in_fallback_chain()
    {
        var expected = TestData.SampleResult("Cape Town");
        var failing  = new FakeProvider("Failing").ThenFail().ThenFail();
        var fake     = new FakeProvider("AcmeGeo").ThenReturn(expected);

        var sut    = new GeolocationOrchestrator(
            new IGeolocationProvider[] { failing, fake },
            new RoundRobinCounter(),
            NullLogger<GeolocationOrchestrator>.Instance);
        var result = await sut.GetLocationAsync(TestIp);

        result.City.Should().Be("Cape Town");
        failing.CallCount.Should().Be(2); // initial + retry
        fake.CallCount.Should().Be(1);
    }
}
