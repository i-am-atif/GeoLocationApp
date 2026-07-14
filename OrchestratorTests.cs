using FluentAssertions;
using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Infrastructure.Orchestration;
using GeoService.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GeoService.Tests;

public sealed class OrchestratorTests
{
    private const string TestIp = "8.8.8.8";

    private static GeolocationOrchestrator BuildOrchestrator(
        IGeolocationProvider[] providers,
        RoundRobinCounter? counter = null)
        => new(providers, counter ?? new RoundRobinCounter(), NullLogger<GeolocationOrchestrator>.Instance);

    // ──────────────────────────────────────────────────────────────────────────
    // Successful provider response
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returns_result_from_first_provider_when_it_succeeds()
    {
        var expected = TestData.SampleResult();
        var p1 = new FakeProvider("P1").ThenReturn(expected);
        var p2 = new FakeProvider("P2");

        var sut    = BuildOrchestrator(new IGeolocationProvider[] { p1, p2 });
        var result = await sut.GetLocationAsync(TestIp);

        result.Should().BeEquivalentTo(expected);
        p1.CallCount.Should().Be(1);
        p2.CallCount.Should().Be(0, "should stop after first success");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Retry success
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Retries_provider_once_before_falling_back()
    {
        var expected = TestData.SampleResult();
        var p1 = new FakeProvider("P1").ThenFail().ThenFail();
        var p2 = new FakeProvider("P2").ThenReturn(expected);

        var sut    = BuildOrchestrator(new IGeolocationProvider[] { p1, p2 });
        var result = await sut.GetLocationAsync(TestIp);

        result.Should().BeEquivalentTo(expected);
        p1.CallCount.Should().Be(2, "should attempt initial + 1 retry");
        p2.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Returns_on_retry_success_without_falling_back()
    {
        var expected = TestData.SampleResult();
        var p1 = new FakeProvider("P1").ThenFail().ThenReturn(expected);
        var p2 = new FakeProvider("P2");

        var sut    = BuildOrchestrator(new IGeolocationProvider[] { p1, p2 });
        var result = await sut.GetLocationAsync(TestIp);

        result.Should().BeEquivalentTo(expected);
        p1.CallCount.Should().Be(2);
        p2.CallCount.Should().Be(0, "should not reach P2 when retry succeeded");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fallback success
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Falls_back_through_all_providers_until_one_succeeds()
    {
        var expected = TestData.SampleResult();
        var p1 = new FakeProvider("P1").ThenFail().ThenFail();
        var p2 = new FakeProvider("P2").ThenFail().ThenFail();
        var p3 = new FakeProvider("P3").ThenReturn(expected);

        var sut    = BuildOrchestrator(new IGeolocationProvider[] { p1, p2, p3 });
        var result = await sut.GetLocationAsync(TestIp);

        result.Should().BeEquivalentTo(expected);
        p1.CallCount.Should().Be(2);
        p2.CallCount.Should().Be(2);
        p3.CallCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // All providers failing
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Throws_AllProvidersFailedException_when_every_provider_fails()
    {
        var p1 = new FakeProvider("P1").ThenFail().ThenFail();
        var p2 = new FakeProvider("P2").ThenFail().ThenFail();

        var sut = BuildOrchestrator(new IGeolocationProvider[] { p1, p2 });

        var act = () => sut.GetLocationAsync(TestIp);

        await act.Should().ThrowAsync<AllProvidersFailedException>()
            .Where(ex => ex.ProvidersAttempted.Contains("P1") && ex.ProvidersAttempted.Contains("P2"));
    }

    [Fact]
    public async Task AllProvidersFailedException_lists_all_attempted_providers_in_order()
    {
        var p1 = new FakeProvider("Alpha").ThenFail().ThenFail();
        var p2 = new FakeProvider("Beta").ThenFail().ThenFail();
        var p3 = new FakeProvider("Gamma").ThenFail().ThenFail();

        var sut = BuildOrchestrator(new IGeolocationProvider[] { p1, p2, p3 });

        var ex = await Assert.ThrowsAsync<AllProvidersFailedException>(
            () => sut.GetLocationAsync(TestIp));

        ex.ProvidersAttempted.Should().ContainInOrder("Alpha", "Beta", "Gamma");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Critical alert logging
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logs_critical_when_all_providers_fail()
    {
        var logSink = new CapturingLogger<GeolocationOrchestrator>();
        var p1      = new FakeProvider("P1").ThenFail().ThenFail();

        var sut = new GeolocationOrchestrator(
            new IGeolocationProvider[] { p1 }, new RoundRobinCounter(), logSink);

        await Assert.ThrowsAsync<AllProvidersFailedException>(() => sut.GetLocationAsync(TestIp));

        logSink.Entries.Should().Contain(e =>
            e.Level == Microsoft.Extensions.Logging.LogLevel.Critical &&
            e.Message.Contains("P1"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Round-robin order
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Round_robin_cycles_through_providers_in_order()
    {
        var results = new List<string>();

        var p1 = new TrackingProvider("P1", results);
        var p2 = new TrackingProvider("P2", results);
        var p3 = new TrackingProvider("P3", results);

        var counter = new RoundRobinCounter();
        var sut     = BuildOrchestrator(new IGeolocationProvider[] { p1, p2, p3 }, counter);

        await sut.GetLocationAsync(TestIp); // Req 1 → P1
        await sut.GetLocationAsync(TestIp); // Req 2 → P2
        await sut.GetLocationAsync(TestIp); // Req 3 → P3
        await sut.GetLocationAsync(TestIp); // Req 4 → P1

        results.Should().ContainInOrder("P1", "P2", "P3", "P1");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Concurrent round-robin behaviour
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Concurrent_requests_each_get_a_unique_starting_provider()
    {
        const int TotalRequests = 300;
        const int ProviderCount = 3;
        var       serveCounts   = new int[ProviderCount];

        var providers = Enumerable.Range(0, ProviderCount)
            .Select(i => (IGeolocationProvider)new CountingProvider($"P{i}", serveCounts, i))
            .ToArray();

        var counter = new RoundRobinCounter();
        var sut     = new GeolocationOrchestrator(
            providers, counter, NullLogger<GeolocationOrchestrator>.Instance);

        var tasks = Enumerable.Range(0, TotalRequests)
            .Select(_ => sut.GetLocationAsync(TestIp));

        await Task.WhenAll(tasks);

        foreach (var count in serveCounts)
            count.Should().BeGreaterThan(0, "every provider should serve at least one request");

        serveCounts.Sum().Should().Be(TotalRequests);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cancellation propagation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Propagates_cancellation_without_swallowing_it()
    {
        using var cts = new CancellationTokenSource();
        var p1 = new FakeProvider("P1").ThenThrow(new OperationCanceledException(cts.Token));

        var sut = BuildOrchestrator(new IGeolocationProvider[] { p1 });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.GetLocationAsync(TestIp, cts.Token));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helper providers used only in this file
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class TrackingProvider(string name, List<string> log) : IGeolocationProvider
    {
        public string ProviderName => name;
        public Task<GeoService.Core.Models.GeolocationResult> LookupAsync(
            string ip, CancellationToken ct = default)
        {
            log.Add(name);
            return Task.FromResult(TestData.SampleResult());
        }
    }

    private sealed class CountingProvider(string name, int[] counts, int index) : IGeolocationProvider
    {
        public string ProviderName => name;
        public Task<GeoService.Core.Models.GeolocationResult> LookupAsync(
            string ip, CancellationToken ct = default)
        {
            Interlocked.Increment(ref counts[index]);
            return Task.FromResult(TestData.SampleResult());
        }
    }
}
