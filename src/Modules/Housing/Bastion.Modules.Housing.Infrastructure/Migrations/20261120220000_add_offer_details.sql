-- migrate:up
-- Fields of an offer by meaning, filled by any parser (listing or detail page); NULL means unknown.
ALTER TABLE housing.offer ADD COLUMN rent numeric(12,2);
ALTER TABLE housing.offer ADD COLUMN rent_currency integer;
ALTER TABLE housing.offer ADD COLUMN deposit numeric(12,2);
ALTER TABLE housing.offer ADD COLUMN deposit_currency integer;
ALTER TABLE housing.offer ADD COLUMN available_from date;
-- 0 is the ground floor, -1 the basement.
ALTER TABLE housing.offer ADD COLUMN floor smallint;
ALTER TABLE housing.offer ADD COLUMN building_floors smallint;
ALTER TABLE housing.offer ADD COLUMN build_year smallint;
ALTER TABLE housing.offer ADD COLUMN building_type integer;
ALTER TABLE housing.offer ADD COLUMN heating integer;
ALTER TABLE housing.offer ADD COLUMN condition integer;
ALTER TABLE housing.offer ADD COLUMN latitude numeric(9,6);
ALTER TABLE housing.offer ADD COLUMN longitude numeric(9,6);
-- How far the real place may be from the point: 0 is exact, more when the advertiser blurs it.
ALTER TABLE housing.offer ADD COLUMN location_radius_m integer;
-- As written by the advertiser (HTML), after anonymization.
ALTER TABLE housing.offer ADD COLUMN description text;
-- Hash of the listing fields without the portal's dates (a bump); a change makes the details due again.
-- NULL for offers stored before, until the next import sees them.
ALTER TABLE housing.offer ADD COLUMN content_hash text;

CREATE TABLE housing.offer_feature (
    offer_id integer NOT NULL REFERENCES housing.offer (id) ON DELETE CASCADE,
    feature integer NOT NULL,
    PRIMARY KEY (offer_id, feature)
);

-- Process state of the detail page fetch, apart from the offer's data.
-- content_hash is the offer's content_hash at the last attempt; a different one means the details are due again.
CREATE TABLE housing.offer_detail_fetch (
    offer_id integer PRIMARY KEY REFERENCES housing.offer (id) ON DELETE CASCADE,
    content_hash text NOT NULL,
    attempted_at timestamptz NOT NULL,
    -- The last successful fetch; NULL while every attempt has failed.
    fetched_at timestamptz,
    -- Failed attempts in a row for this content_hash; 0 after a success.
    failed_attempts smallint NOT NULL,
    last_error text
);

-- The details queue: listed offers of a source, newest first (FetchDetails reads it in batches).
CREATE INDEX offer_details_queue ON housing.offer (source_id, external_created_at DESC NULLS LAST, id DESC)
    WHERE removed_at IS NULL;

-- migrate:down
DROP INDEX housing.offer_details_queue;
DROP TABLE housing.offer_detail_fetch;
DROP TABLE housing.offer_feature;
ALTER TABLE housing.offer DROP COLUMN content_hash;
ALTER TABLE housing.offer DROP COLUMN description;
ALTER TABLE housing.offer DROP COLUMN location_radius_m;
ALTER TABLE housing.offer DROP COLUMN longitude;
ALTER TABLE housing.offer DROP COLUMN latitude;
ALTER TABLE housing.offer DROP COLUMN condition;
ALTER TABLE housing.offer DROP COLUMN heating;
ALTER TABLE housing.offer DROP COLUMN building_type;
ALTER TABLE housing.offer DROP COLUMN build_year;
ALTER TABLE housing.offer DROP COLUMN building_floors;
ALTER TABLE housing.offer DROP COLUMN floor;
ALTER TABLE housing.offer DROP COLUMN available_from;
ALTER TABLE housing.offer DROP COLUMN deposit_currency;
ALTER TABLE housing.offer DROP COLUMN deposit;
ALTER TABLE housing.offer DROP COLUMN rent_currency;
ALTER TABLE housing.offer DROP COLUMN rent;
