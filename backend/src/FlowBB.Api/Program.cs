using FlowBB.Api.ExceptionHandling;
using FlowBB.Api.Hubs;
using FlowBB.Api.Logging;
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// TODO(#21): Register and map Attendance, Pulse, Events, Crew and Routing after their adapters are available.

app.Run();

public partial class Program;
