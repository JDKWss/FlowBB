// FlowBB schema migration 001: uniqueness constraints.
// Idempotent on Neo4j 5.26 Community and Aura.

CREATE CONSTRAINT schema_version_key_unique IF NOT EXISTS
FOR (n:SchemaVersion) REQUIRE n.Key IS UNIQUE;

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

CREATE CONSTRAINT user_email_unique IF NOT EXISTS
FOR (n:User) REQUIRE n.Email IS UNIQUE;

CREATE CONSTRAINT owner_id_unique IF NOT EXISTS
FOR (n:BusinessOwner) REQUIRE n.OwnerId IS UNIQUE;

CREATE CONSTRAINT owner_email_unique IF NOT EXISTS
FOR (n:BusinessOwner) REQUIRE n.Email IS UNIQUE;

MERGE (version:SchemaVersion {Key: 'flowbb'})
WITH version, coalesce(version.Version, 0) < 1 AS shouldAdvance
SET version.Version = CASE WHEN shouldAdvance THEN 1 ELSE version.Version END,
    version.Name = CASE WHEN shouldAdvance THEN '001_constraints' ELSE version.Name END,
    version.AppliedAt = CASE WHEN shouldAdvance THEN datetime() ELSE version.AppliedAt END;
