using FlowBB.Api.IntegrationTests.Infrastructure;
using FlowBB.Api.Logging;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;

namespace FlowBB.Api.IntegrationTests.Logging;

public sealed class RequestLogContextMiddlewareTests
{
    private static readonly Guid EventGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CrewGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid UserGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static (RequestLogContextMiddleware Middleware, CapturingSink Sink, ILogger Logger) Create()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        var middleware = new RequestLogContextMiddleware(_ =>
        {
            logger.Information("inside the request");
            return Task.CompletedTask;
        });
        return (middleware, sink, logger);
    }

    private static async Task<LogEvent> LogInsideRequestAsync(Action<HttpContext> arrange)
    {
        var (middleware, sink, _) = Create();
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        arrange(context);

        await middleware.InvokeAsync(context);

        return sink.Events.Single();
    }

    private static object? ScalarOf(LogEvent logEvent, string property) => ((ScalarValue)logEvent.Properties[property]).Value;

    [Fact]
    public async Task TraceId_IsAlwaysPresentAndMatchesTheResponseTraceId()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        var (middleware, sink, _) = Create();

        await middleware.InvokeAsync(context);

        ScalarOf(sink.Events.Single(), RequestLogContextMiddleware.TraceIdProperty).Should().Be(TraceIdentifiers.Resolve(context));
    }

    [Fact]
    public async Task EventIdFromRoute_IsAddedAsFlowEventId()
    {
        var logEvent = await LogInsideRequestAsync(c => c.Request.RouteValues["eventId"] = EventGuid.ToString());

        ScalarOf(logEvent, RequestLogContextMiddleware.EventIdProperty).Should().Be(EventGuid);
        logEvent.Properties.Should().NotContainKey(RequestLogContextMiddleware.CrewIdProperty);
    }

    [Fact]
    public async Task GroupIdFromRoute_IsAddedAsFlowCrewId()
    {
        var logEvent = await LogInsideRequestAsync(c => c.Request.RouteValues["groupId"] = CrewGuid.ToString());

        ScalarOf(logEvent, RequestLogContextMiddleware.CrewIdProperty).Should().Be(CrewGuid);
        logEvent.Properties.Should().NotContainKey(RequestLogContextMiddleware.EventIdProperty);
    }

    [Fact]
    public async Task EventIdFromQuery_IsUsedForThePulseHexagonsEndpoint()
    {
        var logEvent = await LogInsideRequestAsync(c => c.Request.QueryString = new QueryString($"?eventId={EventGuid}"));

        ScalarOf(logEvent, RequestLogContextMiddleware.EventIdProperty).Should().Be(EventGuid);
    }

    [Fact]
    public async Task RouteValue_TakesPrecedenceOverQuery()
    {
        var other = Guid.NewGuid();
        var logEvent = await LogInsideRequestAsync(c =>
        {
            c.Request.RouteValues["eventId"] = EventGuid.ToString();
            c.Request.QueryString = new QueryString($"?eventId={other}");
        });

        ScalarOf(logEvent, RequestLogContextMiddleware.EventIdProperty).Should().Be(EventGuid);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("")]
    public async Task InvalidOrEmptyIdentifiers_AreNotLogged(string raw)
    {
        var logEvent = await LogInsideRequestAsync(c =>
        {
            c.Request.RouteValues["eventId"] = raw;
            c.Request.RouteValues["groupId"] = raw;
        });

        logEvent.Properties.Should().NotContainKey(RequestLogContextMiddleware.EventIdProperty);
        logEvent.Properties.Should().NotContainKey(RequestLogContextMiddleware.CrewIdProperty);
    }

    [Fact]
    public async Task UserIdentifierAndQueryCoordinates_AreNeverAddedToTheContext()
    {
        var logEvent = await LogInsideRequestAsync(c =>
        {
            c.Request.RouteValues["userId"] = UserGuid.ToString();
            c.Request.RouteValues["eventId"] = EventGuid.ToString();
            c.Request.QueryString = new QueryString("?latitude=49.8225&longitude=19.0444&userId=" + UserGuid);
        });

        CapturingSink.Flatten(logEvent).Should().NotContain(UserGuid.ToString()).And.NotContain("49.8225").And.NotContain("19.0444");
        logEvent.Properties.Keys.Should().BeEquivalentTo(
            RequestLogContextMiddleware.TraceIdProperty, RequestLogContextMiddleware.EventIdProperty);
    }

    [Fact]
    public async Task ContextProperties_DoNotLeakAfterTheRequest()
    {
        var (middleware, sink, logger) = Create();
        var context = new DefaultHttpContext();
        context.Request.RouteValues["eventId"] = EventGuid.ToString();

        await middleware.InvokeAsync(context);
        logger.Information("after the request");

        sink.Events.Last().Properties.Should().BeEmpty();
    }
}
