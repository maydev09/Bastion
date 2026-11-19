-- migrate:up
-- An offer missing from complete full scans of the listing is marked removed, not deleted.
-- Offers stored before were last seen at least when they last changed.
ALTER TABLE housing.offer ADD COLUMN last_seen_at timestamptz;
UPDATE housing.offer SET last_seen_at = updated_at;
ALTER TABLE housing.offer ALTER COLUMN last_seen_at SET NOT NULL;
-- Complete full scans in a row that did not see the offer; 0 once it is seen again.
ALTER TABLE housing.offer ADD COLUMN missed_scans smallint NOT NULL DEFAULT 0;
-- NULL while the offer is listed; when removed, the time it was last seen.
ALTER TABLE housing.offer ADD COLUMN removed_at timestamptz;

-- migrate:down
ALTER TABLE housing.offer DROP COLUMN removed_at;
ALTER TABLE housing.offer DROP COLUMN missed_scans;
ALTER TABLE housing.offer DROP COLUMN last_seen_at;
