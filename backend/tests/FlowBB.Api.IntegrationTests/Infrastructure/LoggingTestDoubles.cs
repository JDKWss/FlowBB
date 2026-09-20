using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

public sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

/// <summary><see cref="ILogger{TCategoryName}"/> zapisujacy wpisy w pamieci (dla testow klas z wstrzykniętym loggerem).</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
}

/// <summary>Sink Serilog zapisujacy zdarzenia w pamieci. Program.cs czyta <c>ILogEventSink</c> z DI (ReadFrom.Services).</summary>
public sealed class CapturingSink : ILogEventSink
{
    public List<LogEvent> Events { get; } = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);

    /// <summary>Wszystkie wartosci zdarzenia (komunikat i wlasciwosci) jako jeden tekst, do sprawdzania wyciekow.</summary>
    public static string Flatten(LogEvent logEvent) =>
        logEvent.RenderMessage() + " " + string.Join(' ', logEvent.Properties.Select(p => $"{p.Key}={p.Value}")) +
        " " + logEvent.Exception;
}
