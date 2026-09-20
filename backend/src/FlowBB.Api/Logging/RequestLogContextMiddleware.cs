using Serilog.Context;

namespace FlowBB.Api.Logging;

/// <summary>
/// Dodaje do kontekstu logow kazdego zadania <c>TraceId</c> oraz, gdy sa dostepne i poprawne, identyfikatory wydarzenia i grupy
/// pobrane z trasy (albo z query <c>eventId</c> dla mapy PULSE). Dzieki temu kazdy log z czasu zadania, takze z adapterow i
/// handlera wyjatkow, ma te pola bez zmian w modulach. Nie dodaje identyfikatora uzytkownika, wspolrzednych ani tresci zadania.
/// </summary>
/// <remarks>
/// <c>EventId</c> jest zarezerwowana przez Microsoft.Extensions.Logging, dlatego identyfikator wydarzenia FlowBB
/// nazywa sie <c>FlowEventId</c> (analogicznie <c>FlowCrewId</c>).
/// </remarks>
public sealed class RequestLogContextMiddleware(RequestDelegate next)
{
    public const string TraceIdProperty = "TraceId";
    public const string EventIdProperty = "FlowEventId";
    public const string CrewIdProperty = "FlowCrewId";

    private const string EventIdKey = "eventId";
    private const string CrewIdKey = "groupId";

    public async Task InvokeAsync(HttpContext context)
    {
        using var trace = LogContext.PushProperty(TraceIdProperty, TraceIdentifiers.Resolve(context));
        using var eventScope = PushIfPresent(EventIdProperty, ReadGuid(context, EventIdKey, allowQuery: true));
        using var crewScope = PushIfPresent(CrewIdProperty, ReadGuid(context, CrewIdKey, allowQuery: false));

        await next(context);
    }

    private static Guid? ReadGuid(HttpContext context, string key, bool allowQuery)
    {
        var raw = context.Request.RouteValues.TryGetValue(key, out var routeValue)
            ? routeValue?.ToString()
            : allowQuery ? context.Request.Query[key].ToString() : null;

        // Tylko poprawny, niepusty Guid trafia do logu (bez dowolnych tekstow z zadania).
        return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
    }

    private static IDisposable PushIfPresent(string property, Guid? value) =>
        value is null ? NoopScope.Instance : LogContext.PushProperty(property, value.Value);

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
            // Brak wlasciwosci do zdjecia z kontekstu.
        }
    }
}
