using FlowBB.Api.ExceptionHandling;
using FlowBB.Api.Endpoints.AirQuality;
using FlowBB.Api.Endpoints.Crews;
using FlowBB.Api.Endpoints.Events;
using FlowBB.Api.Endpoints.Pulse;
using FlowBB.Api.Endpoints.Routing;
using FlowBB.Api.Extensions;
using FlowBB.Api.Health;
using FlowBB.Api.Hubs;
using FlowBB.Api.Logging;
using FlowBB.Infrastructure.Neo4j;
using FlowBB.Infrastructure.Routing;
using Scalar.AspNetCore;
using Serilog;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger: logger hosta nie nadpisuje globalnego Log.Logger (kod uzywa ILogger<T>, nie statycznego Log.*),
// dzieki czemu kilka hostow w jednym procesie (np. testy integracyjne) nie zapisuje do swoich sinkow nawzajem.
builder.Host.UseSerilog(
    (context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services),
    preserveStaticLogger: true);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddPulseHub();
builder.Services.AddNeo4jPersistence();
builder.Services.AddNeo4jReadiness();
builder.Services.AddEventsModule();
builder.Services.AddAttendanceModule();
builder.Services.AddPulseModule();
builder.Services.AddRoutingModule(builder.Configuration.GetRoutingMode());
builder.Services.AddCrewModule();

var giosBaseUrl = builder.Configuration["AirQuality:GiosBaseUrl"] ?? "https://api.gios.gov.pl/pjp-api/";
if (!Uri.TryCreate(giosBaseUrl, UriKind.Absolute, out var giosBaseUri))
{
    throw new InvalidOperationException("AirQuality:GiosBaseUrl must be an absolute URI.");
}

var airQualityCacheMinutes = builder.Configuration.GetValue<double?>("AirQuality:CacheMinutes") ?? 30;
var airQualityFallbackCacheSeconds = builder.Configuration.GetValue<double?>("AirQuality:FallbackCacheSeconds") ?? 60;
var airQualityTimeoutSeconds = builder.Configuration.GetValue<double?>("AirQuality:TimeoutSeconds") ?? 4;
var airQualityFreshnessMinutes = builder.Configuration.GetValue<double?>("AirQuality:FreshnessMinutes") ?? 90;
var airQualityPolicy = new FlowBB.Application.AirQuality.GetEventAirQuality.AirQualityPolicyOptions(
    TimeSpan.FromMinutes(airQualityCacheMinutes),
    TimeSpan.FromSeconds(airQualityTimeoutSeconds),
    TimeSpan.FromMinutes(airQualityFreshnessMinutes),
    TimeSpan.FromSeconds(airQualityFallbackCacheSeconds));
builder.Services.AddAirQualityModule(airQualityPolicy, giosBaseUri);

var routingServiceUrl = builder.Configuration["Routing:ServiceUrl"] ?? "http://routing:8000";
if (!Uri.TryCreate(routingServiceUrl, UriKind.Absolute, out var routingServiceUri))
{
    throw new InvalidOperationException("Routing:ServiceUrl must be an absolute URI.");
}

var routingTimeoutSeconds = builder.Configuration.GetValue<double?>("Routing:TimeoutSeconds") ?? 3;
if (routingTimeoutSeconds <= 0)
{
    throw new InvalidOperationException("Routing:TimeoutSeconds must be greater than zero.");
}

var routingOptions = new RoutingServiceOptions(
    routingServiceUri,
    TimeSpan.FromSeconds(routingTimeoutSeconds),
    builder.Configuration.GetValue("Routing:DemoFallbackEnabled", true));
builder.Services.AddSingleton(routingOptions);
builder.Services.AddHttpClient<RoutingServiceClient>(client =>
{
    client.BaseAddress = routingOptions.ServiceUrl;
    client.Timeout = routingOptions.Timeout;
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .OfType<string>()
    .ToArray();

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

StartupSummary.Log(app.Services.GetRequiredService<ILogger<Program>>(), app.Configuration, app.Environment);

if (builder.Configuration.GetValue<bool>(Neo4jDatabaseInitializer.SeedOnStartupVariable))
{
    app.Logger.LogInformation("Initializing Neo4j schema and idempotent seed data.");
    await Neo4jDatabaseInitializer.InitializeAsync();
}

// Kontekst logow (TraceId, FlowEventId, FlowCrewId) musi obejmowac takze podsumowanie zadania z UseSerilogRequestLogging.
app.UseMiddleware<RequestLogContextMiddleware>();
// Logger z DI zamiast globalnego Log.Logger (patrz preserveStaticLogger wyzej).
app.UseSerilogRequestLogging(options => options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
app.UseExceptionHandler();
app.UseCors(FrontendCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPulseHub();
app.MapEventsEndpoints();
app.MapAttendanceEndpoints();
app.MapPulseEndpoints();
app.MapRoutingEndpoints();
app.MapCrewEndpoints();
app.MapAirQualityEndpoints();
app.MapGet("/health", (TimeProvider clock) => Results.Ok(new HealthResponse("Healthy", clock.GetUtcNow())))
    .WithName("getHealth");
app.MapReadinessEndpoint();

app.Run();

public partial class Program;
