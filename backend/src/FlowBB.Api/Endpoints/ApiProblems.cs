using System.Text.Json;

namespace FlowBB.Api.Endpoints;

internal static class ApiProblems
{
    public static IResult BadRequest(string title) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status400BadRequest);

    public static IResult NotFound(string title) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status404NotFound);

    public static IResult Conflict(string title) =>
        TypedResults.Problem(title: title, statusCode: StatusCodes.Status409Conflict);
}

internal static class RouteIds
{
    public static bool TryParse(string? raw, out Guid id) => Guid.TryParse(raw, out id) && id != Guid.Empty;
}

internal static class ApiRequests
{
    public static async ValueTask<T?> ReadJsonAsync<T>(HttpRequest request, CancellationToken cancellationToken)
    {
        // Bez naglowka application/json ReadFromJsonAsync rzuca InvalidOperationException (co konczylo sie 500).
        if (!request.HasJsonContentType())
        {
            return default;
        }

        try
        {
            return await request.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
