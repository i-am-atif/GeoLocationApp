# GeoLocationApp
Geo Location Application
# GeoService — C# Geolocation Fallback Service

A .NET 8 Web API that resolves IP addresses to geolocation data using three providers
(**Geoapify**, **IPStack**, **IP2Location**) with **round-robin selection**, **per-provider retry**, and
**sequential fallback**.

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [API Key Configuration](#api-key-configuration)
3. [Running the Service](#running-the-service)
4. [Running the Tests](#running-the-tests)
5. [API Reference](#api-reference)
6. [Design Decisions](#design-decisions)
7. [Adding / Removing / Disabling a Provider](#adding--removing--disabling-a-provider)

---

## Quick Start

```bash
git clone <repo-url>
cd GeoService

# Configure API keys (see next section)
dotnet user-secrets set "GeolocationProviders:Geoapify:ApiKey"    "YOUR_KEY" --project src/GeoService.Api
dotnet user-secrets set "GeolocationProviders:IpStack:ApiKey"     "YOUR_KEY" --project src/GeoService.Api
dotnet user-secrets set "GeolocationProviders:Ip2Location:ApiKey" "YOUR_KEY" --project src/GeoService.Api

# Run
dotnet run --project src/GeoService.Api

# Test
curl http://localhost:5000/api/geolocation/8.8.8.8
```

---

## API Key Configuration

**Keys are never hardcoded.** Three mechanisms are supported, applied in priority order:

### Option 1 — .NET User Secrets (recommended for local dev)

```bash
cd src/GeoService.Api

dotnet user-secrets set "GeolocationProviders:Geoapify:ApiKey"    "<geoapify-key>"
dotnet user-secrets set "GeolocationProviders:IpStack:ApiKey"     "<ipstack-key>"
dotnet user-secrets set "GeolocationProviders:Ip2Location:ApiKey" "<ip2location-key>"
```

Secrets are stored outside the project directory and never committed to source control.

### Option 2 — Environment Variables (recommended for CI/CD and production)

```bash
export GeolocationProviders__Geoapify__ApiKey="<geoapify-key>"
export GeolocationProviders__IpStack__ApiKey="<ipstack-key>"
export GeolocationProviders__Ip2Location__ApiKey="<ip2location-key>"

dotnet run --project src/GeoService.Api
```

Note the double underscore `__` separator — this is the .NET environment variable
configuration convention for nested keys.

### Option 3 — appsettings.json (placeholder values only — never commit real keys)

The shipped `appsettings.json` contains placeholder strings like
`REPLACE_WITH_YOUR_GEOAPIFY_KEY`. These are overridden at runtime by either of
the two mechanisms above.

### Getting Free API Keys

| Provider     | Sign-up URL                     | Free tier                   |
|--------------|---------------------------------|-----------------------------|
| Geoapify     | https://www.geoapify.com        | 3,000 requests/day          |
| IPStack      | https://ipstack.com             | 100 requests/month (HTTP)   |
| IP2Location  | https://www.ip2location.io      | 30,000 requests/month       |

> **IPStack note:** the free plan only supports HTTP, not HTTPS. The provider
> implementation defaults to `http://api.ipstack.com`. Paid plans can override
> this via `GeolocationProviders:IpStack:BaseUrl` = `https://api.ipstack.com`.

---

## Running the Service

```bash
dotnet run --project src/GeoService.Api
# → Listening on http://localhost:5000

# Or with watch (hot reload):
dotnet watch --project src/GeoService.Api
```

---

## Running the Tests

```bash
dotnet test
```

All tests are fully offline — **no live API calls are made**. Providers are
replaced with in-memory fakes or stub `HttpMessageHandler`s.

### Test coverage

| Test class                       | What it covers                                                  |
|----------------------------------|-----------------------------------------------------------------|
| `OrchestratorTests`              | Successful response, retry success, fallback success, all-fail, critical log, round-robin order, concurrent round-robin, cancellation |
| `RoundRobinCounterTests`         | Sequential indices, single-provider, concurrent bounds, equal distribution, zero-count guard |
| `ProviderMappingTests`           | Each provider's response format → `GeolocationResult` mapping; HTTP errors; malformed JSON; IPStack `success:false` |
| `FakeProviderExtensibilityTests` | Proves a brand-new provider (no orchestration changes) works in all scenarios |
| `IpValidationTests`              | Valid/invalid IPv4 and IPv6 addresses                           |
| `ApiEndpointTests`               | HTTP 200, 400 (bad IP), 503 (all fail), failure body content, no key leakage |

---

## API Reference

### `GET /api/geolocation/{ip}`

Returns geolocation data for the given IPv4 or IPv6 address.

**Success (200)**
```json
{
  "country": "United States",
  "state": "California",
  "city": "Los Angeles",
  "zipcode": "90006",
  "coordinates": {
    "lat": 34.04759979248047,
    "lng": -118.29226684570312
  },
  "time_zone": "America/Los_Angeles",
  "isp": "Los Angeles Department of Water & Power",
  "currency": "USD"
}
```

**Bad IP (400)**
```json
{ "error": "Invalid IP address format." }
```

**All providers failed (503)**
```json
{
  "message": "Geolocation lookup failed. All providers were attempted and returned errors.",
  "providers_attempted": ["Geoapify", "IPStack", "IP2Location"]
}
```

### `GET /health`

Returns `{ "status": "healthy" }` with HTTP 200.

---

## Design Decisions

### 1. Three-layer architecture

```
GeoService.Api            ← HTTP endpoints only; no business logic
GeoService.Core           ← Interfaces, models, exceptions (no dependencies)
GeoService.Infrastructure ← Provider implementations + orchestrator
```

The Core layer has zero third-party dependencies. This makes it trivially
testable and keeps provider details from leaking upward.

### 2. `IGeolocationProvider` as the extensibility seam

Every provider implements one interface with one method. The orchestrator
depends only on `IEnumerable<IGeolocationProvider>` — it has no `if/switch`
on provider names, no provider-specific logic whatsoever.

### 3. `Enabled` flag resolved generically — no name-based switch in DI

Each provider has an `Enabled` boolean in `ProviderOptions`. Rather than a
`switch` on provider names in the DI layer to check this flag, the
`GeolocationProvidersOptions.GetByName()` method performs that lookup in one
place. The DI registration simply calls `GetByName(p.ProviderName)?.Enabled ?? true`
for each provider — so adding a new provider only requires adding its case to
`GetByName`, not touching DI logic.

### 4. Thread-safe round-robin with `Interlocked.Increment`

`RoundRobinCounter` uses a lock-free atomic increment. Under concurrent load,
each request atomically claims the next slot — no blocking, no contention.
A modulo maps the ever-increasing counter to the provider index range.

### 5. Retry-then-fallback, not retry-all-then-fallback

The orchestrator retries a provider exactly once (two total attempts), then
moves to the next. This minimises latency when a provider is hard-down —
retrying it repeatedly before trying a working provider wastes time.

### 6. Controlled failure surface

`AllProvidersFailedException` carries only provider names — no API keys, no
raw HTTP bodies, no stack traces. The API layer translates it to a clean 503
JSON response. Detailed errors go only to the structured logger.

### 7. IPStack `success: false` detection

IPStack returns HTTP 200 with `{ "success": false }` on auth failure. The
provider checks the body and throws `GeolocationProviderException` so the
orchestrator treats it as a real failure and falls back correctly.

### 8. Strict IP validation via round-trip check

`IPAddress.TryParse` accepts non-standard formats like `"8.8.8"` (BSD
three-part notation). The validator parses then re-serializes and compares —
if the round-trip doesn't match, the input is rejected.

---

## Adding / Removing / Disabling a Provider

### Adding a new provider

1. **Create a new class** in `GeoService.Infrastructure/Providers/`:

```csharp
public sealed class MyNewProvider : IGeolocationProvider
{
    public string ProviderName => "MyNew";

    public async Task<GeolocationResult> LookupAsync(string ipAddress, CancellationToken ct = default)
    {
        // call the API, map the response, throw GeolocationProviderException on failure
    }
}
```

2. **Add configuration** to `GeolocationProvidersOptions`:

```csharp
public ProviderOptions MyNew { get; set; } = new();

public ProviderOptions? GetByName(string providerName) => providerName switch
{
    "Geoapify"    => Geoapify,
    "IPStack"     => IpStack,
    "IP2Location" => Ip2Location,
    "MyNew"       => MyNew,      // ← add this line
    _             => null
};
```

3. **Add the key** to `appsettings.json`:

```json
"GeolocationProviders": {
  "MyNew": { "ApiKey": "REPLACE_ME", "Enabled": true }
}
```

4. **Register in DI** (`ServiceCollectionExtensions.cs`):

```csharp
services.AddHttpClient<MyNewProvider>();
services.AddSingleton<IGeolocationProvider>(sp =>
    ActivatorUtilities.CreateInstance<MyNewProvider>(sp));
```

No orchestration changes required.

### Disabling a provider

Set `"Enabled": false` in configuration (or via environment variable):

```bash
export GeolocationProviders__Geoapify__Enabled=false
```

The provider is excluded from the rotation at startup automatically.

### Removing a provider

1. Delete the provider class file.
2. Remove its `AddHttpClient<>` and `AddSingleton<>` lines from `ServiceCollectionExtensions.cs`.
3. Remove its property and `GetByName` case from `GeolocationProvidersOptions`.
4. Remove its configuration section from `appsettings.json`.

No orchestration code changes required.
