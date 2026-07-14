using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Core.Models;

namespace GeoService.Tests.Helpers;

/// <summary>
/// A configurable fake provider for use in tests.
/// Demonstrates that adding a new provider requires no changes to orchestration logic —
/// just implement IGeolocationProvider and plug it in.
/// </summary>
public sealed class FakeProvider : IGeolocationProvider
{
    private readonly Queue<Func<Task<GeolocationResult>>> _responses = new();
    public string ProviderName { get; }
    public int CallCount { get; private set; }

    public FakeProvider(string name = "FakeProvider")
    {
        ProviderName = name;
    }

    /// <summary>Queue a successful response.</summary>
    public FakeProvider ThenReturn(GeolocationResult result)
    {
        _responses.Enqueue(() => Task.FromResult(result));
        return this;
    }

    /// <summary>Queue a failure.</summary>
    public FakeProvider ThenThrow(Exception ex)
    {
        _responses.Enqueue(() => Task.FromException<GeolocationResult>(ex));
        return this;
    }

    /// <summary>Queue a provider exception.</summary>
    public FakeProvider ThenFail(string reason = "simulated failure")
    {
        return ThenThrow(new GeolocationProviderException(ProviderName, reason));
    }

    public async Task<GeolocationResult> LookupAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (_responses.Count == 0)
            throw new GeolocationProviderException(ProviderName, "No more queued responses");

        return await _responses.Dequeue()();
    }
}

public static class TestData
{
    public static GeolocationResult SampleResult(string city = "Los Angeles") => new()
    {
        Country     = "United States",
        State       = "California",
        City        = city,
        Zipcode     = "90006",
        Coordinates = new Coordinates { Lat = 34.048, Lng = -118.292 },
        TimeZone    = "America/Los_Angeles",
        Isp         = "LA Dept of Water & Power",
        Currency    = "USD",
    };
}
