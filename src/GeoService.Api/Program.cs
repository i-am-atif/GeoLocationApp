using GeoService.Core;
using GeoService.Core.Interfaces;
using GeoService.Core.Models;
using GeoService.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────────────────────
// Configuration
// API keys are loaded from (in priority order):
//   1. Environment variables  (e.g. GeolocationProviders__Geoapify__ApiKey=xxx)
//   2. User secrets           (dotnet user-secrets set "GeolocationProviders:Geoapify:ApiKey" "xxx")
//   3. appsettings.json       (placeholder values only — never real keys in source control)
// ──────────────────────────────────────────────────────────────────────────────

builder.Services.AddGeolocationInfrastructure(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower;
    opts.SerializerOptions.DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.WriteIndented               = true;
});

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ──────────────────────────────────────────────────────────────────────────────
// Endpoints
// ──────────────────────────────────────────────────────────────────────────────

app.MapGet("/api/geolocation/{ip}", async (
    string ip,
    IGeolocationOrchestrator orchestrator,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!IpAddressValidator.IsValid(ip))
    {
        return Results.BadRequest(new { error = "Invalid IP address format." });
    }

    try
    {
        var result = await orchestrator.GetLocationAsync(ip, ct);
        return Results.Ok(result);
    }
    catch (AllProvidersFailedException ex)
    {
        // Return controlled failure — no API keys, no raw provider errors
        var failure = new GeolocationFailure
        {
            ProvidersAttempted = ex.ProvidersAttempted
        };
        return Results.Json(failure, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithName("GetGeolocation")
.WithSummary("Returns geolocation for the given IP address using round-robin provider selection.");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .WithName("Health");

app.Run();

// Make Program accessible to the test project
public partial class Program { }
