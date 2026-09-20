// FlowBB schema migration 002: range index used by event listing.
// PULSE, Attendance and Crew use unique constraints from migration 001.

CREATE INDEX event_start_at IF NOT EXISTS
FOR (n:Event) ON (n.StartAt);

MATCH (version:SchemaVersion {Key: 'flowbb'})
WHERE version.Version = 1
SET version.Version = 2,
    version.Name = '002_event_start_at_index',
    version.AppliedAt = datetime();
