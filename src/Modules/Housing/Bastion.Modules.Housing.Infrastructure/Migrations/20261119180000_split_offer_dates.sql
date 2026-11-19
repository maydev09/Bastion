-- migrate:up
-- Our own dates of an offer (created_at, updated_at) and the portal's dates (external_*).
-- The portal's dates are empty for offers stored before; a re-parse fills them from the raw pages.
ALTER TABLE housing.offer RENAME COLUMN first_seen_at TO created_at;
ALTER TABLE housing.offer ADD COLUMN external_created_at timestamptz;
ALTER TABLE housing.offer ADD COLUMN external_updated_at timestamptz;

-- An OtoDom advertising tile (invalid date 1999-02-29) was stored as an offer before the parser skipped it.
DELETE FROM housing.offer WHERE source_id = 1 AND external_id = '9000062330';

-- migrate:down
ALTER TABLE housing.offer DROP COLUMN external_updated_at;
ALTER TABLE housing.offer DROP COLUMN external_created_at;
ALTER TABLE housing.offer RENAME COLUMN created_at TO first_seen_at;
