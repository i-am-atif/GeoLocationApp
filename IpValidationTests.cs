using FluentAssertions;
using GeoService.Core;
using Xunit;

namespace GeoService.Tests;

public sealed class IpValidationTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("192.168.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("2001:db8::1")]                      // IPv6
    [InlineData("::1")]                              // IPv6 loopback
    [InlineData("2606:4700:4700::1111")]             // Cloudflare IPv6
    public void Valid_ip_addresses_are_accepted(string ip)
        => IpAddressValidator.IsValid(ip).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not-an-ip")]
    [InlineData("999.999.999.999")]
    [InlineData("256.0.0.1")]
    [InlineData("google.com")]
    [InlineData("8.8.8")]           // incomplete octet
    [InlineData("8.8.8.8.8")]       // too many octets
    [InlineData("abc")]
    public void Invalid_ip_addresses_are_rejected(string? ip)
        => IpAddressValidator.IsValid(ip).Should().BeFalse();
}
