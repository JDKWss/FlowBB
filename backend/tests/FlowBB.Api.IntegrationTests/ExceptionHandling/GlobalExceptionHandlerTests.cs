using System.Text.Json;
using FlowBB.Api.ExceptionHandling;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowBB.Api.IntegrationTests.ExceptionHandling;

public sealed class GlobalExceptionHandlerTests
{
    private const string TraceId = "trace-123";
    private const string Secret = "Neo.ClientError.Security.Unauthorized neo4j://user:hunter2@db.example.io";

    private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
    }

    private sealed record Setup(GlobalExceptionHandler Handler, ListLogger<GlobalExceptionHandler> Logger, DefaultHttpContext Context);

    private static Setup CreateSetup(CancellationToken requestAborted = default)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = TraceId };
        context.Request.Method = "GET";
        context.Request.Path = "/api/events/11111111-1111-1111-1111-111111111111";
        context.Response.Body = new MemoryStream();
        context.RequestAborted = requestAborted;

        var logger = new ListLogger<GlobalExceptionHandler>();
        return new Setup(new GlobalExceptionHandler(services.GetRequiredService<IProblemDetailsService>(), logger), logger, context);
    }

    private static async Task<string> BodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body).ReadToEndAsync();
    }

    [Fact]
    public async Task UnexpectedException_ReturnsSafeProblemDetailsWithTraceId()
    {
        var setup = CreateSetup();

        var handled = await setup.Handler.TryHandleAsync(setup.Context, new InvalidOperationException(Secret), CancellationToken.None);
        var body = await BodyAsync(setup.Context);
        var json = JsonDocument.Parse(body).RootElement;

        handled.Should().BeTrue();
        setup.Context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        setup.Context.Response.ContentType.Should().StartWith("application/problem+json");
        json.GetProperty("status").GetInt32().Should().Be(500);
        json.GetProperty("title").GetString().Should().Be(GlobalExceptionHandler.UnexpectedErrorTitle);
        json.GetProperty("traceId").GetString().Should().Be(TraceId);
    }

    [Fact]
    public async Task UnexpectedException_DoesNotLeakDetailsToTheClient()
    {
        var setup = CreateSetup();
        var exception = new InvalidOperationException(Secret);

        await setup.Handler.TryHandleAsync(setup.Context, exception, CancellationToken.None);
        var body = await BodyAsync(setup.Context);

        body.Should().NotContain("hunter2").And.NotContain("neo4j://").And.NotContain("Neo.ClientError")
            .And.NotContain(nameof(InvalidOperationException)).And.NotContainEquivalentOf("stackTrace")
            .And.NotContain("   at ");
    }

    [Fact]
    public async Task UnexpectedException_IsLoggedOnceAtErrorLevelWithTraceId()
    {
        var setup = CreateSetup();
        var exception = new InvalidOperationException(Secret);

        await setup.Handler.TryHandleAsync(setup.Context, exception, CancellationToken.None);

        var entry = setup.Logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(exception);
        entry.Message.Should().Contain(TraceId).And.Contain("GET");
    }

    [Fact]
    public async Task ClientAbort_IsNotLoggedAsErrorAndDoesNotReturn500()
    {
        using var cancellation = new CancellationTokenSource();
        var setup = CreateSetup(cancellation.Token);
        await cancellation.CancelAsync();

        var handled = await setup.Handler.TryHandleAsync(setup.Context, new OperationCanceledException(), CancellationToken.None);

        handled.Should().BeTrue();
        setup.Context.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
        setup.Logger.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning);
        (await BodyAsync(setup.Context)).Should().BeEmpty();
    }

    [Fact]
    public async Task OperationCanceledWithoutClientAbort_IsTreatedAsUnexpected()
    {
        var setup = CreateSetup();

        await setup.Handler.TryHandleAsync(setup.Context, new OperationCanceledException(), CancellationToken.None);

        setup.Context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        setup.Logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ArgumentException_IsNotMappedTo400()
    {
        var setup = CreateSetup();

        await setup.Handler.TryHandleAsync(setup.Context, new ArgumentException("bad argument"), CancellationToken.None);

        setup.Context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        (await BodyAsync(setup.Context)).Should().NotContain("bad argument");
    }
}
