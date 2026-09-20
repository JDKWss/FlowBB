// FlowBB — idempotentny seed danych syntetycznych dla Neo4j Aura.
// Dane odpowiadaja mockom z client/src/mocks/data.ts.
// Category i Source sa kanonicznymi stringami zgodnymi z enumami backendu.
// Model nie zawiera pola DemoData. Syntetycznosc wynika z kontrolowanych identyfikatorow i domeny .invalid.

// 1. UZYTKOWNICY (82)
// Pierwszy identyfikator odpowiada DEMO_USER_ID z klienta. Pozostale sa deterministyczne.

UNWIND range(1, 82) AS i
WITH i,
     CASE WHEN i = 1
       THEN 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
       ELSE 'd1000000-0000-0000-0000-' + right('000000000000' + toString(i), 12)
     END AS userId,
     CASE (i - 1) % 4
       WHEN 0 THEN [49.81272, 19.03384]
       WHEN 1 THEN [49.82245, 19.04431]
       WHEN 2 THEN [49.79381, 19.04955]
       ELSE [49.81324, 19.05071]
     END AS origin
MERGE (u:User {UserId: userId})
SET u.Name = 'Uzytkownik ' + right('000' + toString(i), 3),
    u.Email = 'user-' + right('000' + toString(i), 3) + '@flowbb.invalid',
    u.PasswordHash = 'SYNTHETIC_ACCOUNT_NOT_FOR_LOGIN',
    u.DefaultOriginLatitude = origin[0] + ((i - 1) % 5) * 0.00008,
    u.DefaultOriginLongitude = origin[1] + ((i - 1) % 5) * 0.00008
RETURN count(u) AS UsersCreatedOrUpdated;

// Przywraca dokladny punkt startowy glownego uzytkownika z golden-event.json.
MATCH (u:User {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'})
SET u.DefaultOriginLatitude = 49.81272,
    u.DefaultOriginLongitude = 19.03384
RETURN count(u) AS PrimaryUserUpdated;

// 2. MIEJSCA (4)

UNWIND [
  {Id: 'seed-venue-rynek', N: 'Rynek w Bielsku-Bialej', A: 'Rynek, Bielsko-Biala', Lat: 49.82245, Lon: 19.04431},
  {Id: 'seed-venue-blonia', N: 'Błonia Bielskie', A: 'Błonia, Bielsko-Biała', Lat: 49.79381, Lon: 19.04955},
  {Id: 'seed-venue-ksiaznica', N: 'Książnica Beskidzka', A: 'Centrum, Bielsko-Biała', Lat: 49.82055, Lon: 19.04863},
  {Id: 'seed-venue-bulwary', N: 'Bulwary Straceńskie', A: 'Straconka, Bielsko-Biała', Lat: 49.81324, Lon: 19.05071}
] AS row
MERGE (v:Venue {VenueId: row.Id})
SET v.Name = row.N,
    v.Address = row.A,
    v.Latitude = row.Lat,
    v.Longitude = row.Lon
RETURN count(v) AS VenuesCreatedOrUpdated;

// 3. ORGANIZATOR (1)

MERGE (o:BusinessOwner {OwnerId: 'seed-owner-flowbb'})
SET o.CompanyName = 'FlowBB Events',
    o.Email = 'organizer@flowbb.invalid',
    o.IsVerified = true
RETURN count(o) AS OwnersCreatedOrUpdated;

// 4. DODATKOWE, SWOBODNE TAGI (4)

UNWIND [
  {Id: 'seed-tag-culture', N: 'Muzyka na żywo'},
  {Id: 'seed-tag-sport', N: 'Bieganie nocą'},
  {Id: 'seed-tag-education', N: 'Zielone miasto'},
  {Id: 'seed-tag-community', N: 'Lokalne inicjatywy'}
] AS row
MERGE (t:Tag {TagId: row.Id})
SET t.Name = row.N
RETURN count(t) AS TagsCreatedOrUpdated;

// 5. WYDARZENIA Z MOCKA KLIENTA (4)

UNWIND [
  {Id: '11111111-1111-1111-1111-111111111111', N: 'Koncert na Rynku', D: 'Wieczorny koncert w centrum Bielska-Bialej.', U: 'https://events.flowbb.invalid/concert', S: '2026-09-19T19:00:00+02:00', E: '2026-09-19T21:30:00+02:00', C: 'Culture', Source: 'Demo'},
  {Id: '33333333-3333-3333-3333-333333333333', N: 'Nocny Bieg na Błoniach', D: 'A recreational run after dark. The late finish demonstrates the return-gap alert.', U: 'https://events.flowbb.invalid/night-run', S: '2026-09-20T21:00:00+02:00', E: '2026-09-20T23:15:00+02:00', C: 'Sport', Source: 'Demo'},
  {Id: '44444444-4444-4444-4444-444444444444', N: 'Warsztaty miejskiego ogrodnictwa', D: 'A practical session about green balconies, water retention, and neighbourhood gardens.', U: 'https://events.flowbb.invalid/gardening', S: '2026-09-21T10:30:00+02:00', E: '2026-09-21T12:30:00+02:00', C: 'Education', Source: 'Demo'},
  {Id: '55555555-5555-5555-5555-555555555555', N: 'Sąsiedzki piknik nad Białą', D: 'A family picnic, local initiatives, and a relaxed afternoon by the river.', U: 'https://events.flowbb.invalid/picnic', S: '2026-09-21T14:00:00+02:00', E: '2026-09-21T18:00:00+02:00', C: 'Community', Source: 'Demo'}
] AS row
MERGE (e:Event {EventId: row.Id})
SET e.Name = row.N,
    e.Description = row.D,
    e.EventUrl = row.U,
    e.StartAt = datetime(row.S),
    e.EndAt = datetime(row.E),
    e.Category = row.C,
    e.Source = row.Source
REMOVE e.Title, e.DateTime
RETURN count(e) AS EventsCreatedOrUpdated;

// 6. CREW Z MOCKA KLIENTA (2)

UNWIND [
  {Id: '22222222-2222-2222-2222-222222222222', N: 'Nowi w Bielsku', D: 'Mikrogrupa dla osob, ktore chca poznac miasto.', Max: 6, Tags: ['nowi-w-miescie', 'muzyka'], Point: 'Plac Chrobrego', Lat: 49.82205, Lon: 19.04318},
  {Id: '66666666-6666-6666-6666-666666666666', N: 'Sąsiedzi z Dolnego Przedmieścia', D: 'We meet nearby and walk to the concert together.', Max: 6, Tags: ['neighbours', 'walking'], Point: 'Plac Wojska Polskiego', Lat: 49.82413, Lon: 19.04561}
] AS row
MERGE (c:Crew {CrewId: row.Id})
SET c.Name = row.N,
    c.Description = row.D,
    c.MaxMembers = row.Max,
    c.Tags = row.Tags,
    c.MeetingPointName = row.Point,
    c.MeetingPointLatitude = row.Lat,
    c.MeetingPointLongitude = row.Lon
RETURN count(c) AS CrewsCreatedOrUpdated;

// 7. ORGANIZATOR -[:MANAGES]-> MIEJSCE

MATCH (o:BusinessOwner {OwnerId: 'seed-owner-flowbb'})
UNWIND ['seed-venue-rynek', 'seed-venue-blonia', 'seed-venue-ksiaznica', 'seed-venue-bulwary'] AS venueId
MATCH (v:Venue {VenueId: venueId})
MERGE (o)-[:MANAGES]->(v)
RETURN count(*) AS ManagesRelationships;

// 8. ORGANIZATOR -[:CREATED_EVENT]-> EVENT

MATCH (o:BusinessOwner {OwnerId: 'seed-owner-flowbb'})
UNWIND [
  '11111111-1111-1111-1111-111111111111',
  '33333333-3333-3333-3333-333333333333',
  '44444444-4444-4444-4444-444444444444',
  '55555555-5555-5555-5555-555555555555'
] AS eventId
MATCH (e:Event {EventId: eventId})
MERGE (o)-[:CREATED_EVENT]->(e)
RETURN count(*) AS CreatedEventRelationships;

// 9. EVENT -[:HOSTED_AT]-> VENUE
// Dla wydarzen nalezacych do seedu usuwa stara relacje, aby kazde mialo dokladnie jedno miejsce.

UNWIND [
  {E: '11111111-1111-1111-1111-111111111111', V: 'seed-venue-rynek'},
  {E: '33333333-3333-3333-3333-333333333333', V: 'seed-venue-blonia'},
  {E: '44444444-4444-4444-4444-444444444444', V: 'seed-venue-ksiaznica'},
  {E: '55555555-5555-5555-5555-555555555555', V: 'seed-venue-bulwary'}
] AS row
MATCH (e:Event {EventId: row.E}), (v:Venue {VenueId: row.V})
OPTIONAL MATCH (e)-[old:HOSTED_AT]->(:Venue)
DELETE old
MERGE (e)-[:HOSTED_AT]->(v)
RETURN count(DISTINCT e) AS HostedAtRelationships;

// 10. EVENT -[:HAS_TAG]-> TAG

UNWIND [
  {E: '11111111-1111-1111-1111-111111111111', T: 'seed-tag-culture'},
  {E: '33333333-3333-3333-3333-333333333333', T: 'seed-tag-sport'},
  {E: '44444444-4444-4444-4444-444444444444', T: 'seed-tag-education'},
  {E: '55555555-5555-5555-5555-555555555555', T: 'seed-tag-community'}
] AS row
MATCH (e:Event {EventId: row.E}), (t:Tag {TagId: row.T})
OPTIONAL MATCH (e)-[old:HAS_TAG]->(:Tag)
DELETE old
WITH DISTINCT e, t
MERGE (e)-[:HAS_TAG]->(t)
RETURN count(*) AS HasTagRelationships;

// 11. USER -[:IS_GOING_TO]-> EVENT
// Zakresy odtwarzaja participantsCount 82, 46, 28 i 64 z mocka.

UNWIND [
  {Count: 82, E: '11111111-1111-1111-1111-111111111111', Updated: '2026-09-19T12:00:00Z'},
  {Count: 46, E: '33333333-3333-3333-3333-333333333333', Updated: '2026-09-19T12:05:00Z'},
  {Count: 28, E: '44444444-4444-4444-4444-444444444444', Updated: '2026-09-19T12:10:00Z'},
  {Count: 64, E: '55555555-5555-5555-5555-555555555555', Updated: '2026-09-19T12:15:00Z'}
] AS batch
MATCH (e:Event {EventId: batch.E})
OPTIONAL MATCH (:User)-[old:IS_GOING_TO]->(e)
DELETE old
WITH DISTINCT batch, e
UNWIND range(1, batch.Count) AS i
WITH e, batch, i,
     CASE WHEN i = 1
       THEN 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
       ELSE 'd1000000-0000-0000-0000-' + right('000000000000' + toString(i), 12)
     END AS userId
MATCH (u:User {UserId: userId})
MERGE (u)-[attendance:IS_GOING_TO]->(e)
SET attendance.TransportMode = ['Walking', 'PublicTransport', 'Bike', 'Car', 'Unknown'][(i - 1) % 5],
    attendance.OriginLatitude = u.DefaultOriginLatitude,
    attendance.OriginLongitude = u.DefaultOriginLongitude,
    attendance.UpdatedAt = datetime(batch.Updated)
RETURN count(*) AS GoingToRelationships;

// 12. USER -[:FRIENDS_WITH]-> USER (lancuch, oba kierunki)

UNWIND range(1, 81) AS i
WITH
  CASE WHEN i = 1
    THEN 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    ELSE 'd1000000-0000-0000-0000-' + right('000000000000' + toString(i), 12)
  END AS firstId,
  'd1000000-0000-0000-0000-' + right('000000000000' + toString(i + 1), 12) AS secondId
MATCH (first:User {UserId: firstId}), (second:User {UserId: secondId})
MERGE (first)-[:FRIENDS_WITH]->(second)
MERGE (second)-[:FRIENDS_WITH]->(first)
RETURN count(*) * 2 AS FriendsWithRelationships;

// 13. CREW -[:FOR_EVENT]-> EVENT

UNWIND [
  '22222222-2222-2222-2222-222222222222',
  '66666666-6666-6666-6666-666666666666'
] AS crewId
MATCH (c:Crew {CrewId: crewId})
OPTIONAL MATCH (c)-[old:FOR_EVENT]->(:Event)
DELETE old
WITH DISTINCT c
MATCH (e:Event {EventId: '11111111-1111-1111-1111-111111111111'})
MERGE (c)-[:FOR_EVENT]->(e)
RETURN count(*) AS ForEventRelationships;

// 14. USER -[:MEMBER_OF]-> CREW
// Glowny uzytkownik nie nalezy do zadnej z grup. Liczniki grup wynosza 4 i 6 jak w mocku.

UNWIND [
  {C: '22222222-2222-2222-2222-222222222222', From: 2, To: 5},
  {C: '66666666-6666-6666-6666-666666666666', From: 6, To: 11}
] AS batch
MATCH (c:Crew {CrewId: batch.C})
OPTIONAL MATCH (:User)-[old:MEMBER_OF]->(c)
DELETE old
WITH DISTINCT batch, c
UNWIND range(batch.From, batch.To) AS i
WITH c,
     'd1000000-0000-0000-0000-' + right('000000000000' + toString(i), 12) AS userId
MATCH (u:User {UserId: userId})
MERGE (u)-[:MEMBER_OF]->(c)
RETURN count(*) AS CrewMemberships;

// 15. UZYTKOWNIK -[:IS_ORGANIZATION_MEMBER]-> ORGANIZATOR

MATCH (u:User {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'})
MATCH (o:BusinessOwner {OwnerId: 'seed-owner-flowbb'})
MERGE (u)-[:IS_ORGANIZATION_MEMBER]->(o)
RETURN count(*) AS OrganizationMemberships;

// __FLOWBB_SEED_END__
// Zapytania ponizej sa kontrolne i nie sa wykonywane automatycznie przez backend.

// 16. KONTROLA LICZBY WEZLOW SEEDU
// Oczekiwane: User=82, Event=4, Venue=4, BusinessOwner=1, Tag=4, Crew=2.

MATCH (n)
WHERE (n:User AND (n.UserId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' OR n.UserId STARTS WITH 'd1000000-'))
   OR (n:Event AND n.EventId IN [
        '11111111-1111-1111-1111-111111111111',
        '33333333-3333-3333-3333-333333333333',
        '44444444-4444-4444-4444-444444444444',
        '55555555-5555-5555-5555-555555555555'])
   OR (n:Crew AND n.CrewId IN [
        '22222222-2222-2222-2222-222222222222',
        '66666666-6666-6666-6666-666666666666'])
   OR (n:Venue AND n.VenueId STARTS WITH 'seed-venue-')
   OR (n:BusinessOwner AND n.OwnerId = 'seed-owner-flowbb')
   OR (n:Tag AND n.TagId STARTS WITH 'seed-tag-')
RETURN labels(n)[0] AS Label, count(*) AS Nodes
ORDER BY Label;

// 17. KONTROLA LICZNIKOW UCZESTNIKOW
// Oczekiwane kolejno: 82, 46, 28, 64.

MATCH (u:User)-[:IS_GOING_TO]->(e:Event)
WHERE e.EventId IN [
  '11111111-1111-1111-1111-111111111111',
  '33333333-3333-3333-3333-333333333333',
  '44444444-4444-4444-4444-444444444444',
  '55555555-5555-5555-5555-555555555555'
]
RETURN e.EventId AS EventId, count(DISTINCT u) AS Participants
ORDER BY EventId;

// 18. KONTROLA HOSTED_AT
// Oczekiwane: pusta lista.

MATCH (e:Event)
WHERE e.EventId IN [
  '11111111-1111-1111-1111-111111111111',
  '33333333-3333-3333-3333-333333333333',
  '44444444-4444-4444-4444-444444444444',
  '55555555-5555-5555-5555-555555555555'
]
OPTIONAL MATCH (e)-[:HOSTED_AT]->(v:Venue)
WITH e, count(v) AS venueCount
WHERE venueCount <> 1
RETURN e.EventId AS InvalidEventId, venueCount;

// 19. KONTROLA WSPOLRZEDNYCH
// Oczekiwane: InvalidCoordinates=0.

MATCH (n)
WHERE (n:User AND (n.UserId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' OR n.UserId STARTS WITH 'd1000000-') AND
      (n.DefaultOriginLatitude < -90 OR n.DefaultOriginLatitude > 90 OR
       n.DefaultOriginLongitude < -180 OR n.DefaultOriginLongitude > 180))
   OR (n:Venue AND n.VenueId STARTS WITH 'seed-venue-' AND
      (n.Latitude < -90 OR n.Latitude > 90 OR n.Longitude < -180 OR n.Longitude > 180))
   OR (n:Crew AND n.CrewId IN [
        '22222222-2222-2222-2222-222222222222',
        '66666666-6666-6666-6666-666666666666'] AND
      (n.MeetingPointLatitude < -90 OR n.MeetingPointLatitude > 90 OR
       n.MeetingPointLongitude < -180 OR n.MeetingPointLongitude > 180))
RETURN count(n) AS InvalidCoordinates;

// 20. PODGLAD DANYCH Z MOCKA

MATCH (e:Event)-[:HOSTED_AT]->(v:Venue)
OPTIONAL MATCH (u:User)-[:IS_GOING_TO]->(e)
RETURN e, v, count(DISTINCT u) AS Participants
ORDER BY e.StartAt;

// 21. KONTROLA KANONICZNYCH POL EVENT I SNAPSHOTOW ATTENDANCE
// Oczekiwane: InvalidEvents=0, InvalidAttendanceSnapshots=0.

MATCH (e:Event)
WHERE e.EventId IN [
  '11111111-1111-1111-1111-111111111111',
  '33333333-3333-3333-3333-333333333333',
  '44444444-4444-4444-4444-444444444444',
  '55555555-5555-5555-5555-555555555555'
]
WITH count(CASE
  WHEN e.Category IS NULL OR e.Source IS NULL THEN 1
END) AS InvalidEvents
MATCH (:User)-[attendance:IS_GOING_TO]->(seedEvent:Event)
WHERE seedEvent.EventId IN [
  '11111111-1111-1111-1111-111111111111',
  '33333333-3333-3333-3333-333333333333',
  '44444444-4444-4444-4444-444444444444',
  '55555555-5555-5555-5555-555555555555'
]
RETURN InvalidEvents,
       count(CASE
         WHEN attendance.TransportMode IS NULL
           OR attendance.OriginLatitude IS NULL
           OR attendance.OriginLongitude IS NULL
           OR attendance.UpdatedAt IS NULL
         THEN 1
       END) AS InvalidAttendanceSnapshots;
