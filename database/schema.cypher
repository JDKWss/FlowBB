// FlowBB - schemat Neo4j (constraints i indeksy).
// Idempotentny: kazde polecenie ma IF NOT EXISTS, wiec skrypt mozna uruchamiac wielokrotnie.
// Dziala na Neo4j 5 Community i Aura: uzywa wylacznie constraintow unikalnosci i indeksow zakresu.
// Constraintow istnienia (IS NOT NULL) i kluczy wezla NIE uzywamy: wymagaja edycji Enterprise.
// Pola wymagane pilnuja adaptery w Infrastructure/Neo4j, patrz docs/NEO4J_CONTRACT.md.
//
// Uruchomienie: patrz database/README.md. Kolejnosc: najpierw ten plik, potem seed.

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
