namespace GeoService.Core;

/// <summary>
/// Signals that a geolocation provider failed to return a usable result.
/// The message is safe for internal logging; it should NOT be forwarded to API callers.
/// </summary>
public sealed class GeolocationProviderException : Exception
{
    public string ProviderName { get; }

    public GeolocationProviderException(string providerName, string message, Exception? inner = null)
        : base(message, inner)
    {
        ProviderName = providerName;
    }
}
