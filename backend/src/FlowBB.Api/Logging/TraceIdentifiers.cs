using System.Diagnostics;

namespace FlowBB.Api.Logging;

/// <summary>
/// Jedno zrodlo identyfikatora korelacji zadania. Zgodnie z domyslna konwencja ASP.NET Core dla ProblemDetails jest to
/// <c>Activity.Current.Id</c> (W3C), a bez aktywnosci <c>HttpContext.TraceIdentifier</c>. Ta sama wartosc trafia do odpowiedzi
/// bledu (<c>traceId</c>) i do kontekstu logow, wiec log mozna odszukac po identyfikatorze z odpowiedzi.
/// </summary>
public static class TraceIdentifiers
{
    public static string Resolve(HttpContext httpContext) => Activity.Current?.Id ?? httpContext.TraceIdentifier;
}
