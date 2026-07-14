namespace GeoService.Core;

/// <summary>
/// Thrown by the orchestrator when every configured provider has failed (including retries).
/// </summary>
public sealed class AllProvidersFailedException : Exception
{
    public IReadOnlyList<string> ProvidersAttempted { get; }

    public AllProvidersFailedException(IReadOnlyList<string> providersAttempted)
        : base($"All geolocation providers failed. Attempted: {string.Join(", ", providersAttempted)}")
    {
        ProvidersAttempted = providersAttempted;
    }
}
