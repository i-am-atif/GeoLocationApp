using FluentAssertions;
using GeoService.Infrastructure.Orchestration;
using Xunit;

namespace GeoService.Tests;

public sealed class RoundRobinCounterTests
{
    [Fact]
    public void Returns_sequential_indices_for_single_threaded_calls()
    {
        var counter = new RoundRobinCounter();

        counter.Next(3).Should().Be(0);
        counter.Next(3).Should().Be(1);
        counter.Next(3).Should().Be(2);
        counter.Next(3).Should().Be(0); // wraps
    }

    [Fact]
    public void Works_with_single_provider()
    {
        var counter = new RoundRobinCounter();

        for (int i = 0; i < 5; i++)
            counter.Next(1).Should().Be(0);
    }

    [Fact]
    public void All_indices_are_within_bounds_under_concurrent_load()
    {
        const int Threads  = 50;
        const int PerThread = 100;
        const int Providers = 3;

        var counter = new RoundRobinCounter();
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();

        Parallel.For(0, Threads, _ =>
        {
            for (int i = 0; i < PerThread; i++)
                results.Add(counter.Next(Providers));
        });

        results.Should().OnlyContain(v => v >= 0 && v < Providers);
        results.Should().HaveCount(Threads * PerThread);
    }

    [Fact]
    public void Each_provider_receives_equal_share_under_concurrent_load()
    {
        const int Total     = 3000;
        const int Providers = 3;

        var counter = new RoundRobinCounter();
        var buckets = new int[Providers];

        Parallel.For(0, Total, _ =>
        {
            var idx = counter.Next(Providers);
            Interlocked.Increment(ref buckets[idx]);
        });

        // Allow ±5% deviation from perfect balance
        var expected = Total / Providers;
        foreach (var count in buckets)
            count.Should().BeCloseTo(expected, delta: (uint)(expected * 0.05));
    }

    [Fact]
    public void Throws_when_provider_count_is_zero()
    {
        var counter = new RoundRobinCounter();
        var act = () => counter.Next(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
