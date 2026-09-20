using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowBB.Infrastructure.Routing.Mzk;

/// <summary>
/// Kształt plików z <c>data/gtfs/mzk/parsed/</c>. Nazwy pól w JSON są w snake_case, więc wystarczy polityka
/// nazewnictwa z BCL i nie trzeba atrybutów na każdej właściwości.
/// </summary>
internal static class MzkTimetableJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.Strict,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };
}

internal sealed class MzkDeparturePageJson
{
    public string Line { get; set; } = string.Empty;

    public int Seq { get; set; }

    public string StopName { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public MzkServiceDeparturesJson Departures { get; set; } = new();
}

internal sealed class MzkServiceDeparturesJson
{
    public List<MzkDepartureJson> Weekday { get; set; } = [];

    public List<MzkDepartureJson> WeekdayHoliday { get; set; } = [];

    public List<MzkDepartureJson> Saturday { get; set; } = [];

    public List<MzkDepartureJson> Sunday { get; set; } = [];

    public List<MzkDepartureJson> For(MzkServiceType service) => service switch
    {
        MzkServiceType.Weekday => Weekday,
        MzkServiceType.WeekdaySchoolHoliday => WeekdayHoliday,
        MzkServiceType.Saturday => Saturday,
        MzkServiceType.Sunday => Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unknown MZK service type.")
    };
}

internal sealed class MzkDepartureJson
{
    public int Sec { get; set; }

    public List<string> Flags { get; set; } = [];

    public bool DepotRun { get; set; }
}

internal sealed class MzkCalendarDayJson
{
    public string Day { get; set; } = string.Empty;

    public string Service { get; set; } = string.Empty;
}

internal sealed class MzkStopJson
{
    public string StopName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
