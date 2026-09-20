using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlowBB.Api.ExceptionHandling;

/// <summary>
/// Ostatnia linia obrony dla wyjatkow nieoczekiwanych. Loguje wyjatek po stronie serwera i zwraca klientowi
/// bezpieczny <c>ProblemDetails</c> z <c>traceId</c>: bez stack trace, nazwy typu wyjatku i oryginalnego komunikatu
/// (w tym komunikatow Neo4j). Przewidywalne wyniki biznesowe (400/404/409) mapuja endpointy jawnie na podstawie result type'ow.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string UnexpectedErrorTitle = "An unexpected error occurred.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (IsClientAbort(httpContext, exception))
        {
            logger.LogDebug("Request was canceled by the client. TraceId: {TraceId}", ResolveTraceId(httpContext));
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            ResolveTraceId(httpContext));

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = CreateProblemDetails(httpContext)
        });
    }

    private static bool IsClientAbort(HttpContext httpContext, Exception exception) =>
        exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested;

    private static ProblemDetails CreateProblemDetails(HttpContext httpContext) => new()
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = UnexpectedErrorTitle,
        Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        Extensions = { ["traceId"] = ResolveTraceId(httpContext) }
    };

    /// <summary>
    /// Ten sam identyfikator trafia do logu i do odpowiedzi. Zgodnie z domyslna konwencja ASP.NET Core dla ProblemDetails
    /// jest to <c>Activity.Current.Id</c> (W3C), a bez aktywnosci <c>HttpContext.TraceIdentifier</c>. Dzieki temu <c>traceId</c>
    /// ma taki sam format we wszystkich odpowiedziach bledow API.
    /// </summary>
    private static string ResolveTraceId(HttpContext httpContext) => Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
