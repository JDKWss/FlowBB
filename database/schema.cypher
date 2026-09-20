// FlowBB - zgodny wstecznie snapshot schematu w wersji 2.
// Zrodlem prawdy sa numerowane skrypty database/migrations/*.cypher.
// Idempotentny: polecenia DDL maja IF NOT EXISTS, a znacznik wersji nie jest cofany.
// Dziala na Neo4j 5 Community i Aura: uzywa wylacznie constraintow unikalnosci i indeksow zakresu.
// Constraintow istnienia (IS NOT NULL) i kluczy wezla NIE uzywamy: wymagaja edycji Enterprise.
// Pola wymagane pilnuja adaptery w Infrastructure/Neo4j, patrz docs/NEO4J_CONTRACT.md.
//
// Uruchomienie: patrz database/README.md. Nowe instalacje stosuja migracje po kolei.

CREATE CONSTRAINT schema_version_key_unique IF NOT EXISTS
FOR (n:SchemaVersion) REQUIRE n.Key IS UNIQUE;

// --- Constraints unikalnosci: MVP ---

CREATE CONSTRAINT user_id_unique IF NOT EXISTS
FOR (n:User) REQUIRE n.UserId IS UNIQUE;

CREATE CONSTRAINT event_id_unique IF NOT EXISTS
FOR (n:Event) REQUIRE n.EventId IS UNIQUE;

CREATE CONSTRAINT venue_id_unique IF NOT EXISTS
FOR (n:Venue) REQUIRE n.VenueId IS UNIQUE;

CREATE CONSTRAINT crew_id_unique IF NOT EXISTS
FOR (n:Crew) REQUIRE n.CrewId IS UNIQUE;

CREATE CONSTRAINT tag_id_unique IF NOT EXISTS
FOR (n:Tag) REQUIRE n.TagId IS UNIQUE;

// --- Constraints unikalnosci: pozostalosc poza zakresem MVP ---
// Zostaja, bo seed nadal laczy te wezly przez MERGE, a baza po starszym uruchomieniu ma je juz zalozone.
// Logowanie i funkcje B2B sa poza zakresem hackathonu (AGENTS.md sekcja 3).

CREATE CONSTRAINT user_email_unique IF NOT EXISTS
FOR (n:User) REQUIRE n.Email IS UNIQUE;

CREATE CONSTRAINT owner_id_unique IF NOT EXISTS
FOR (n:BusinessOwner) REQUIRE n.OwnerId IS UNIQUE;

CREATE CONSTRAINT owner_email_unique IF NOT EXISTS
FOR (n:BusinessOwner) REQUIRE n.Email IS UNIQUE;

// --- Indeksy ---

// Lista wydarzen: filtr i sortowanie po StartAt (IEventRepository.ListAsync).
CREATE INDEX event_start_at IF NOT EXISTS
FOR (n:Event) ON (n.StartAt);

MERGE (version:SchemaVersion {Key: 'flowbb'})
WITH version, coalesce(version.Version, 0) < 2 AS shouldAdvance
SET version.Version = CASE WHEN shouldAdvance THEN 2 ELSE version.Version END,
    version.Name = CASE WHEN shouldAdvance THEN '002_event_start_at_index' ELSE version.Name END,
    version.AppliedAt = CASE WHEN shouldAdvance THEN datetime() ELSE version.AppliedAt END;
