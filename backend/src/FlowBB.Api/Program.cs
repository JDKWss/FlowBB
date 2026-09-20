using FlowBB.Api.ExceptionHandling;
using FlowBB.Api.Endpoints.Events;
using FlowBB.Api.Endpoints.Pulse;
using FlowBB.Api.Endpoints.Routing;
using FlowBB.Api.Hubs;
using FlowBB.Infrastructure.Neo4j;
using Scalar.AspNetCore;
using Serilog;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddPulseHub();
builder.Services.AddNeo4jPersistence();
builder.Services.AddEventsModule();
builder.Services.AddAttendanceModule();
builder.Services.AddPulseModule();
builder.Services.AddRoutingModule();

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

if (builder.Configuration.GetValue<bool>(Neo4jDatabaseInitializer.SeedOnStartupVariable))
{
    app.Logger.LogInformation("Initializing Neo4j schema and idempotent seed data.");
    await Neo4jDatabaseInitializer.InitializeAsync();
}

app.UseSerilogRequestLogging();
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
