namespace GeoService.Infrastructure.Orchestration;

/// <summary>
/// Thread-safe, lock-free counter used to implement round-robin provider selection.
/// Uses <see cref="Interlocked.Increment"/> so concurrent requests each advance
/// the counter independently without blocking each other.
/// </summary>
public sealed class RoundRobinCounter
{
    private long _counter = -1;

    /// <summary>
    /// Returns the next starting index in [0, providerCount).
    /// Safe to call concurrently from multiple threads.
    /// </summary>
    public int Next(int providerCount)
    {
        if (providerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(providerCount), "Must be > 0");

        var value = Interlocked.Increment(ref _counter);
        // Guard against overflow wrap-around to negative values
        if (value < 0)
        {
            Interlocked.Exchange(ref _counter, 0);
            value = 0;
        }
        return (int)(value % providerCount);
    }
}
