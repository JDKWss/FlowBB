using FlowBB.Application.Abstractions.Persistence;
using FlowBB.Application.Pulse;
using FlowBB.Domain.Attendance;
using FlowBB.Domain.Common;
using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>
/// Adapter <see cref="IAttendanceRepository"/>. Zapis relacji <c>IS_GOING_TO</c> i odczyt licznika oraz modal splitu
/// dzieja sie w jednej transakcji, wiec wynik nie moze pochodzic z innego stanu niz zapis. <c>MERGE</c> na relacji
/// blokuje oba wezly, wiec rownolegle zapisy tej samej pary nie tworza duplikatu.
/// </summary>
public sealed class Neo4jAttendanceRepository(IDriver driver, Neo4jOptions options) : IAttendanceRepository
{
    // Znacznik r.__IsNew istnieje tylko w obrebie tej instrukcji: jest ustawiany, odczytywany i usuwany przed commitem,
    // wiec inna transakcja nigdy go nie widzi. Pozwala odroznic utworzenie relacji od dopasowania istniejacej.
    private const string UpsertQuery = """
        MATCH (u:User {UserId: $userId})
        MATCH (e:Event {EventId: $eventId})
        MERGE (u)-[r:IS_GOING_TO]->(e)
        ON CREATE SET r.__IsNew = true
        ON MATCH SET r.__IsNew = false
        SET r.TransportMode = $mode,
            r.OriginLatitude = u.HomeLatitude,
            r.OriginLongitude = u.HomeLongitude,
            r.UpdatedAt = $updatedAt
        WITH r, r.__IsNew AS IsNew, (u.HomeLatitude IS NULL OR u.HomeLongitude IS NULL) AS MissingHome
        REMOVE r.__IsNew
        RETURN IsNew, MissingHome
        """;

    // Rownolegle usuniecia tej samej pary: bez blokady kazde z nich widzi relacje i zglasza WasDeleted = true.
    // No-op SET zaklada blokade zapisu na wezle uzytkownika, wiec kolejne usuniecie czeka na commit poprzedniego
    // i dopiero potem czyta relacje (Read Committed).
    private const string LockUserQuery = """
        MATCH (u:User {UserId: $userId})
        SET u.UserId = u.UserId
        """;

    private const string DeleteQuery = """
        MATCH (:User {UserId: $userId})-[r:IS_GOING_TO]->(:Event {EventId: $eventId})
        DELETE r
        RETURN count(r) AS Deleted
        """;

    private const string AggregateQuery = """
        MATCH (:User)-[g:IS_GOING_TO]->(:Event {EventId: $eventId})
        RETURN g.TransportMode AS Mode, count(g) AS Total
        """;

    public async Task<AttendanceUpsertPersistenceResult?> UpsertAsync(
        AttendanceIntent attendance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attendance);

        await using var session = OpenSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            var isNew = await WriteSnapshotAsync(tx, attendance, cancellationToken);
            if (isNew is null)
            {
                return null;
            }

            var (participants, split) = await ReadAggregatesAsync(tx, attendance.EventId, cancellationToken);
            return new AttendanceUpsertPersistenceResult(isNew.Value, participants, split);
        });
    }

    public async Task<AttendanceDeletePersistenceResult> DeleteAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var session = OpenSession();
        return await session.ExecuteWriteAsync(async tx =>
        {
            var parameters = IdParameters(eventId, userId);
            await (await tx.RunAsync(LockUserQuery, parameters)).ConsumeAsync();

            var cursor = await tx.RunAsync(DeleteQuery, parameters);
            var deleted = (await cursor.ToListAsync(cancellationToken))[0]["Deleted"].As<long>();

            var (participants, split) = await ReadAggregatesAsync(tx, eventId, cancellationToken);
            return new AttendanceDeletePersistenceResult(deleted > 0, participants, split);
        });
    }

    private IAsyncSession OpenSession()
    {
        return driver.AsyncSession(config => config.WithDatabase(options.Database));
    }

    private static Dictionary<string, object?> IdParameters(Guid eventId, Guid userId)
    {
        return new Dictionary<string, object?>
        {
            ["eventId"] = Neo4jValueConversions.ToDatabaseId(eventId),
            ["userId"] = Neo4jValueConversions.ToDatabaseId(userId)
        };
    }

    /// <returns><c>true</c> dla nowej relacji, <c>false</c> dla zaktualizowanej, <c>null</c> gdy brak uzytkownika lub wydarzenia.</returns>
    private static async Task<bool?> WriteSnapshotAsync(
        IAsyncQueryRunner tx,
        AttendanceIntent attendance,
        CancellationToken cancellationToken)
    {
        var parameters = IdParameters(attendance.EventId, attendance.UserId);
        parameters["mode"] = attendance.TransportMode.ToString();
        parameters["updatedAt"] = attendance.UpdatedAt;

        var cursor = await tx.RunAsync(UpsertQuery, parameters);
        var records = await cursor.ToListAsync(cancellationToken);
        if (records.Count == 0)
        {
            return null;
        }

        if (records[0]["MissingHome"].As<bool>())
        {
            // Wyjatek w transakcji cofa zapis: snapshot bez punktu startu zepsulby modal split i trase.
            throw new InvalidOperationException(
                $"User {attendance.UserId:D} has no HomeLatitude/HomeLongitude, so the attendance snapshot cannot be built.");
        }

        return records[0]["IsNew"].As<bool>();
    }

    private static async Task<(int Participants, ModalSplit Split)> ReadAggregatesAsync(
        IAsyncQueryRunner tx,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?> { ["eventId"] = Neo4jValueConversions.ToDatabaseId(eventId) };
        var cursor = await tx.RunAsync(AggregateQuery, parameters);
        var rows = await cursor.ToListAsync(cancellationToken);

        var counts = rows.ToDictionary(
            row => Neo4jValueConversions.ToTransportModeOrUnknown(row["Mode"]),
            row => Convert.ToInt32(row["Total"].As<long>()));

        int Count(TransportMode mode) => counts.GetValueOrDefault(mode);
        var split = new ModalSplit(
            Count(TransportMode.PublicTransport),
            Count(TransportMode.Walking),
            Count(TransportMode.Bike),
            Count(TransportMode.Car),
            Count(TransportMode.Unknown));

        return (counts.Values.Sum(), split);
    }
}
