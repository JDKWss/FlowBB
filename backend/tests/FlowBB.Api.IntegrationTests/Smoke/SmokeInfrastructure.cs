using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Smoke;

/// <summary>Adres uruchomionego API, przeciwko ktoremu dzialaja testy smoke. Bez zmiennej testy sa pomijane.</summary>
internal static class SmokeEnvironment
{
    public const string BaseUrlVariable = "FLOWBB_SMOKE_BASE_URL";

    public static string? BaseUrl => Environment.GetEnvironmentVariable(BaseUrlVariable);

    public static string? SkipReason => string.IsNullOrWhiteSpace(BaseUrl)
        ? $"Set {BaseUrlVariable} (e.g. http://localhost:8080) to run the smoke tests against a running stack."
        : null;
}

public sealed class SmokeFactAttribute : FactAttribute
{
    public SmokeFactAttribute() => Skip = SmokeEnvironment.SkipReason;
}

public sealed class SmokeTheoryAttribute : TheoryAttribute
{
    public SmokeTheoryAttribute() => Skip = SmokeEnvironment.SkipReason;
}

/// <summary>Testy smoke zmieniaja stan wspolnej bazy, wiec nie moga dzialac rownolegle z soba.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SmokeCollection
{
    public const string Name = "Smoke";
}

/// <summary>Dane z seedu demo (database/flowbb-demo-seed.cypher), na ktorych opieraja sie testy smoke.</summary>
internal static class SmokeSeed
{
    /// <summary>"Koncert na Rynku": 82 uczestnikow, jedyne wydarzenie z mikrogrupami.</summary>
    public static readonly Guid Concert = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>"Nocny Bieg na Blonich": 46 uczestnikow, bez grup. Uzywany do zapisu i wypisu uzytkownika testowego.</summary>
    public static readonly Guid Run = Guid.Parse("33333333-3333-3333-3333-333333333333");

    /// <summary>Grupa z wolnymi miejscami (4 z 6).</summary>
    public static readonly Guid OpenCrew = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Pelna grupa (6 z 6).</summary>
    public static readonly Guid FullCrew = Guid.Parse("66666666-6666-6666-6666-666666666666");

    /// <summary>Syntetyczny uzytkownik, ktory nie uczestniczy w wydarzeniu Run i nie nalezy do zadnej grupy.</summary>
    public static readonly Guid FreeUser = Guid.Parse("d1000000-0000-0000-0000-000000000082");

    /// <summary>Uzytkownik klienta (DEMO_USER_ID): w seedzie nie uczestniczy w zadnym wydarzeniu ani grupie.</summary>
    public static readonly Guid DemoUser = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid Unknown = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
}

/// <summary>Cienki klient HTTP i pomocniki dziedzinowe dla testow smoke.</summary>
public sealed class SmokeClient : IDisposable
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(SmokeEnvironment.BaseUrl ?? "http://localhost"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    public Task<HttpResponseMessage> GetAsync(string path) => _http.GetAsync(path);

    public Task<HttpResponseMessage> DeleteAsync(string path) => _http.DeleteAsync(path);

    public Task<HttpResponseMessage> PostJsonAsync(string path, object body) => _http.PostAsJsonAsync(path, body);

    public Task<HttpResponseMessage> PostRawAsync(string path, string body, string? contentType)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = contentType is null ? null : new(contentType);
        return _http.PostAsync(path, content);
    }

    public Task<HttpResponseMessage> DeclareAsync(Guid eventId, Guid userId, string transportMode) =>
        PostJsonAsync($"/api/events/{eventId}/attendance", new { userId, transportMode });

    public Task<HttpResponseMessage> WithdrawAsync(Guid eventId, Guid userId) =>
        DeleteAsync($"/api/events/{eventId}/attendance/{userId}");

    public Task<HttpResponseMessage> JoinAsync(Guid crewId, Guid userId) =>
        PostJsonAsync($"/api/groups/{crewId}/members", new { userId });

    public Task<HttpResponseMessage> LeaveAsync(Guid crewId, Guid userId) =>
        DeleteAsync($"/api/groups/{crewId}/members/{userId}");

    public static Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>GET z oczekiwanym 200; zwraca tresc JSON.</summary>
    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await GetAsync(path);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, "GET {0}", path);
        return await ReadAsync(response);
    }

    public Task<JsonElement> PulseAsync(Guid eventId) => GetJsonAsync($"/api/pulse/events/{eventId}");

    public async Task<int> ParticipantsAsync(Guid eventId) =>
        (await PulseAsync(eventId)).GetProperty("participantsCount").GetInt32();

    /// <summary>Liczba uczestnikow bez dogodnego powrotu (DemoReturnGapPolicy: PublicTransport, koniec o 22:00 lub pozniej).</summary>
    public async Task<int> ParticipantsWithoutReturnAsync(Guid eventId) =>
        (await PulseAsync(eventId)).GetProperty("participantsWithoutReturn").GetInt32();

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Baza testow smoke: przed i po kazdym tescie usuwa deklaracje i czlonkostwa uzytkownika testowego (operacje sa
/// idempotentne), wiec kolejne uruchomienie zaczyna od stanu seedu.
/// </summary>
public abstract class SmokeTestBase : IAsyncLifetime
{
    protected SmokeClient Api { get; } = new();

    public async Task InitializeAsync()
    {
        var before = await SnapshotAsync();
        await ResetAsync();
        (await SnapshotAsync()).Should().Be(
            before,
            "the smoke users must not belong to the seed data (events {0}/{3}, crews {1}, {2}); the reset just removed seeded data, "
            + "so restart the API to restore the seed and check SmokeSeed",
            SmokeSeed.Run, SmokeSeed.OpenCrew, SmokeSeed.FullCrew, SmokeSeed.Concert);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await ResetAsync();
        }
        finally
        {
            Api.Dispose();
        }
    }

    private async Task<string> SnapshotAsync()
    {
        var participants = await Api.ParticipantsAsync(SmokeSeed.Run);
        var concertParticipants = await Api.ParticipantsAsync(SmokeSeed.Concert);
        var returnGap = await Api.ParticipantsWithoutReturnAsync(SmokeSeed.Run);
        var groups = await Api.GetJsonAsync($"/api/events/{SmokeSeed.Concert}/groups");
        var members = groups.EnumerateArray().Select(group => group.GetProperty("currentMembers").GetInt32());
        return $"participants={participants}; concertParticipants={concertParticipants}; returnGap={returnGap}; "
            + $"crewMembers={string.Join(',', members)}";
    }

    private async Task ResetAsync()
    {
        using var attendance = await Api.WithdrawAsync(SmokeSeed.Run, SmokeSeed.FreeUser);
        using var demoAttendance = await Api.WithdrawAsync(SmokeSeed.Concert, SmokeSeed.DemoUser);
        using var openCrew = await Api.LeaveAsync(SmokeSeed.OpenCrew, SmokeSeed.FreeUser);
        using var fullCrew = await Api.LeaveAsync(SmokeSeed.FullCrew, SmokeSeed.FreeUser);
        attendance.EnsureSuccessStatusCode();
        demoAttendance.EnsureSuccessStatusCode();
        openCrew.EnsureSuccessStatusCode();
        fullCrew.EnsureSuccessStatusCode();
    }
}
