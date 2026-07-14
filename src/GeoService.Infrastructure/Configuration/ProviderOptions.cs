namespace GeoService.Infrastructure.Configuration;

/// <summary>
/// Configuration options for a single geolocation provider.
/// Bound from appsettings.json or environment variables — never hardcoded.
/// </summary>
public sealed class ProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// When false the provider is excluded from the rotation entirely.
    /// Allows disabling a provider without removing code or redeploying.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Base URL override — useful for testing with mock servers.</summary>
    public string? BaseUrl { get; set; }
}

public sealed class GeolocationProvidersOptions
{
    public const string SectionName = "GeolocationProviders";

    public ProviderOptions Geoapify    { get; set; } = new();
    public ProviderOptions IpStack     { get; set; } = new();
    public ProviderOptions Ip2Location { get; set; } = new();

    /// <summary>
    /// Returns the ProviderOptions for a given provider name.
    /// Used generically so the DI layer never needs a name-based switch.
    /// </summary>
    public ProviderOptions? GetByName(string providerName) => providerName switch
    {
        "Geoapify"    => Geoapify,
        "IPStack"     => IpStack,
        "IP2Location" => Ip2Location,
        _             => null
    };
}
