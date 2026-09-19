// FlowBB — zapytania dla Neo4j Query.
// Dane syntetyczne: DEMO DATA / SYMULACJA.
// WAŻNE: uruchamiaj po jednym pełnym bloku, od pierwszego słowa do średnika.

// 1. OGRANICZENIA — każde polecenie uruchom osobno

CREATE CONSTRAINT user_id_unique IF NOT EXISTS
FOR (n:User) REQUIRE n.UserId IS UNIQUE;

CREATE CONSTRAINT user_email_unique IF NOT EXISTS
FOR (n:User) REQUIRE n.Email IS UNIQUE;

CREATE CONSTRAINT event_id_unique IF NOT EXISTS
FOR (n:Event) REQUIRE n.EventId IS UNIQUE;

CREATE CONSTRAINT venue_id_unique IF NOT EXISTS
FOR (n:Venue) REQUIRE n.VenueId IS UNIQUE;

CREATE CONSTRAINT owner_id_unique IF NOT EXISTS
FOR (n:BusinessOwner) REQUIRE n.OwnerId IS UNIQUE;

CREATE CONSTRAINT owner_email_unique IF NOT EXISTS
FOR (n:BusinessOwner) REQUIRE n.Email IS UNIQUE;

CREATE CONSTRAINT tag_id_unique IF NOT EXISTS
FOR (n:Tag) REQUIRE n.TagId IS UNIQUE;

// 2. UŻYTKOWNICY — zaznacz od UNWIND do średnika i uruchom

UNWIND [
  {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', Email: 'ania.demo@flowbb.local', Name: 'Ania Nowak'},
  {UserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', Email: 'bartek.demo@flowbb.local', Name: 'Bartek Kowalski'},
  {UserId: 'cccccccc-cccc-cccc-cccc-cccccccccccc', Email: 'celina.demo@flowbb.local', Name: 'Celina Wiśniewska'},
  {UserId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', Email: 'dawid.demo@flowbb.local', Name: 'Dawid Pietrzyk'}
] AS row
MERGE (u:User {UserId: row.UserId})
SET u.Email = row.Email,
    u.PasswordHash = 'DEMO_HASH_NOT_FOR_AUTHENTICATION',
    u.Name = row.Name
RETURN count(u) AS UsersCreatedOrUpdated;

// 3. MIEJSCA

UNWIND [
  {VenueId: 'venue-rynek-bb', Name: 'Rynek w Bielsku-Białej', Address: 'Rynek, 43-300 Bielsko-Biała', Latitude: 49.82245, Longitude: 19.04431},
  {VenueId: 'venue-aquarium-bb', Name: 'Klubokawiarnia Aquarium', Address: 'ul. 3 Maja 11, 43-300 Bielsko-Biała', Latitude: 49.82360, Longitude: 19.04420},
  {VenueId: 'venue-cavatina-bb', Name: 'Cavatina Hall', Address: 'ul. Dworkowa 2, 43-300 Bielsko-Biała', Latitude: 49.82050, Longitude: 19.05050}
] AS row
MERGE (v:Venue {VenueId: row.VenueId})
SET v.Name = row.Name,
    v.Address = row.Address,
    v.Latitude = row.Latitude,
    v.Longitude = row.Longitude
RETURN count(v) AS VenuesCreatedOrUpdated;

// 4. WŁAŚCICIELE BIZNESOWI

UNWIND [
  {OwnerId: 'owner-aquarium', CompanyName: 'Aquarium Team (DEMO)', Email: 'kontakt@aquarium.demo.local', IsVerified: true},
  {OwnerId: 'owner-cavatina', CompanyName: 'Cavatina Management (DEMO)', Email: 'kontakt@cavatina.demo.local', IsVerified: true}
] AS row
MERGE (o:BusinessOwner {OwnerId: row.OwnerId})
SET o.CompanyName = row.CompanyName,
    o.Email = row.Email,
    o.IsVerified = row.IsVerified
RETURN count(o) AS OwnersCreatedOrUpdated;

// 5. TAGI

UNWIND [
  {TagId: 'tag-muzyka-na-zywo', Name: 'Muzyka na żywo'},
  {TagId: 'tag-planszowki', Name: 'Planszówki'},
  {TagId: 'tag-it', Name: 'IT'},
  {TagId: 'tag-gory', Name: 'Góry'},
  {TagId: 'tag-kultura', Name: 'Kultura'}
] AS row
MERGE (t:Tag {TagId: row.TagId})
SET t.Name = row.Name
RETURN count(t) AS TagsCreatedOrUpdated;

// 6. WYDARZENIA

UNWIND [
  {EventId: '11111111-1111-1111-1111-111111111111', Title: 'Koncert na Rynku', Description: 'Wieczorny koncert w centrum Bielska-Białej.', EventUrl: 'https://example.invalid/koncert-na-rynku', DateTime: '2026-09-19T19:00:00+02:00'},
  {EventId: '22222222-2222-2222-2222-222222222222', Title: 'Wieczór z Planszówkami', Description: 'Spotkanie dla osób, które chcą poznać ludzi przy grach.', EventUrl: 'https://example.invalid/planszowki', DateTime: '2026-09-25T18:00:00+02:00'},
  {EventId: '33333333-3333-3333-3333-333333333333', Title: 'Hackathon Bielsko 2030', Description: 'Warsztaty i pomysły na przyszłość miasta.', EventUrl: 'https://example.invalid/hackathon', DateTime: '2026-09-28T09:00:00+02:00'},
  {EventId: '44444444-4444-4444-4444-444444444444', Title: 'Nocne wejście na Szyndzielnię', Description: 'Wspólny trekking z latarkami.', EventUrl: 'https://example.invalid/szyndzielnia', DateTime: '2026-10-02T20:00:00+02:00'}
] AS row
MERGE (e:Event {EventId: row.EventId})
SET e.Title = row.Title,
    e.Description = row.Description,
    e.EventUrl = row.EventUrl,
    e.DateTime = datetime(row.DateTime)
RETURN count(e) AS EventsCreatedOrUpdated;

// 7. BUSINESSOWNER -[:MANAGES]-> VENUE

UNWIND [
  {OwnerId: 'owner-aquarium', VenueId: 'venue-aquarium-bb'},
  {OwnerId: 'owner-cavatina', VenueId: 'venue-cavatina-bb'}
] AS row
MATCH (o:BusinessOwner {OwnerId: row.OwnerId})
MATCH (v:Venue {VenueId: row.VenueId})
MERGE (o)-[:MANAGES]->(v)
RETURN count(*) AS ManagesRelationships;

// 8. EVENT -[:HOSTED_AT]-> VENUE

UNWIND [
  {EventId: '11111111-1111-1111-1111-111111111111', VenueId: 'venue-rynek-bb'},
  {EventId: '22222222-2222-2222-2222-222222222222', VenueId: 'venue-aquarium-bb'},
  {EventId: '33333333-3333-3333-3333-333333333333', VenueId: 'venue-cavatina-bb'},
  {EventId: '44444444-4444-4444-4444-444444444444', VenueId: 'venue-cavatina-bb'}
] AS row
MATCH (e:Event {EventId: row.EventId})
MATCH (v:Venue {VenueId: row.VenueId})
MERGE (e)-[:HOSTED_AT]->(v)
RETURN count(*) AS HostedAtRelationships;

// 9. EVENT -[:HAS_TAG]-> TAG

UNWIND [
  {EventId: '11111111-1111-1111-1111-111111111111', TagId: 'tag-muzyka-na-zywo'},
  {EventId: '11111111-1111-1111-1111-111111111111', TagId: 'tag-kultura'},
  {EventId: '22222222-2222-2222-2222-222222222222', TagId: 'tag-planszowki'},
  {EventId: '33333333-3333-3333-3333-333333333333', TagId: 'tag-it'},
  {EventId: '44444444-4444-4444-4444-444444444444', TagId: 'tag-gory'}
] AS row
MATCH (e:Event {EventId: row.EventId})
MATCH (t:Tag {TagId: row.TagId})
MERGE (e)-[:HAS_TAG]->(t)
RETURN count(*) AS HasTagRelationships;

// 10. USER -[:LIKES_TAG]-> TAG

UNWIND [
  {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', TagId: 'tag-muzyka-na-zywo'},
  {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', TagId: 'tag-it'},
  {UserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', TagId: 'tag-planszowki'},
  {UserId: 'cccccccc-cccc-cccc-cccc-cccccccccccc', TagId: 'tag-it'},
  {UserId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', TagId: 'tag-gory'}
] AS row
MATCH (u:User {UserId: row.UserId})
MATCH (t:Tag {TagId: row.TagId})
MERGE (u)-[:LIKES_TAG]->(t)
RETURN count(*) AS LikesTagRelationships;

// 11. USER -[:IS_GOING_TO]-> EVENT

UNWIND [
  {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', EventId: '11111111-1111-1111-1111-111111111111'},
  {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', EventId: '33333333-3333-3333-3333-333333333333'},
  {UserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', EventId: '11111111-1111-1111-1111-111111111111'},
  {UserId: 'cccccccc-cccc-cccc-cccc-cccccccccccc', EventId: '22222222-2222-2222-2222-222222222222'},
  {UserId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', EventId: '44444444-4444-4444-4444-444444444444'}
] AS row
MATCH (u:User {UserId: row.UserId})
MATCH (e:Event {EventId: row.EventId})
MERGE (u)-[:IS_GOING_TO]->(e)
RETURN count(*) AS GoingToRelationships;

// 12. USER -[:IS_INTERESTED_IN]-> EVENT

UNWIND [
  {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', EventId: '22222222-2222-2222-2222-222222222222'},
  {UserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', EventId: '33333333-3333-3333-3333-333333333333'},
  {UserId: 'cccccccc-cccc-cccc-cccc-cccccccccccc', EventId: '33333333-3333-3333-3333-333333333333'},
  {UserId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', EventId: '11111111-1111-1111-1111-111111111111'}
] AS row
MATCH (u:User {UserId: row.UserId})
MATCH (e:Event {EventId: row.EventId})
MERGE (u)-[:IS_INTERESTED_IN]->(e)
RETURN count(*) AS InterestedInRelationships;

// 13. USER -[:FOLLOWS]-> VENUE

UNWIND [
  {UserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', VenueId: 'venue-rynek-bb'},
  {UserId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', VenueId: 'venue-aquarium-bb'},
  {UserId: 'cccccccc-cccc-cccc-cccc-cccccccccccc', VenueId: 'venue-cavatina-bb'},
  {UserId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', VenueId: 'venue-cavatina-bb'}
] AS row
MATCH (u:User {UserId: row.UserId})
MATCH (v:Venue {VenueId: row.VenueId})
MERGE (u)-[:FOLLOWS]->(v)
RETURN count(*) AS FollowsRelationships;

// 14. USER -[:FRIENDS_WITH]-> USER — oba kierunki

UNWIND [
  {User1Id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', User2Id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'},
  {User1Id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', User2Id: 'cccccccc-cccc-cccc-cccc-cccccccccccc'},
  {User1Id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', User2Id: 'dddddddd-dddd-dddd-dddd-dddddddddddd'}
] AS row
MATCH (u1:User {UserId: row.User1Id})
MATCH (u2:User {UserId: row.User2Id})
MERGE (u1)-[:FRIENDS_WITH]->(u2)
MERGE (u2)-[:FRIENDS_WITH]->(u1)
RETURN count(*) * 2 AS FriendsWithRelationships;

// 15. KONTROLA — oczekiwane: 18 węzłów i 35 relacji

MATCH (n)
OPTIONAL MATCH ()-[r]->()
RETURN count(DISTINCT n) AS Nodes, count(DISTINCT r) AS Relationships;

// 16. WYŚWIETLENIE CAŁEGO GRAFU

MATCH (a)-[r]->(b)
RETURN a, r, b;

// 17. UŻYTKOWNICY, WYDARZENIA, MIEJSCA I TAGI

MATCH (u:User)-[:IS_GOING_TO]->(e:Event)-[:HOSTED_AT]->(v:Venue)
OPTIONAL MATCH (e)-[:HAS_TAG]->(t:Tag)
RETURN u.Name AS User, e.Title AS Event, v.Name AS Venue,
       collect(t.Name) AS Tags
ORDER BY Event, User;

// Opcjonalne usunięcie wszystkich danych — uruchom tylko świadomie:
// MATCH (n) DETACH DELETE n;
