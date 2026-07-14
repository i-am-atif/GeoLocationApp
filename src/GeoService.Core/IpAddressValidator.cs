using System.Net;
using System.Net.Sockets;

namespace GeoService.Core;

public static class IpAddressValidator
{
    /// <summary>
    /// Returns true only for strict IPv4 (4 dot-separated octets) or IPv6 addresses.
    /// Rejects non-standard formats like "8.8.8" or "8.8.8.8.8" that IPAddress.TryParse
    /// may accept due to legacy decimal/octal notation support.
    /// </summary>
    public static bool IsValid(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return false;

        if (!IPAddress.TryParse(ipAddress, out var parsed))
            return false;

        // For IPv4, re-serialize and compare to reject compressed forms like "8.8.8" → "0.0.8.8"
        if (parsed.AddressFamily == AddressFamily.InterNetwork)
            return parsed.ToString() == ipAddress.Trim();

        // IPv6 — TryParse is strict enough
        return parsed.AddressFamily == AddressFamily.InterNetworkV6;
    }
}
