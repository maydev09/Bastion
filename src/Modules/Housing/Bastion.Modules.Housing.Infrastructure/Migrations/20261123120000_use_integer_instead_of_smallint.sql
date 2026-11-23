-- migrate:up
-- smallint rejected values that the C# int accepts (a build year of 195014 on Otodom stopped FetchDetails);
-- the column type follows the domain type, and value rules live in C#.
ALTER TABLE housing.offer
    ALTER COLUMN floor TYPE integer,
    ALTER COLUMN building_floors TYPE integer,
    ALTER COLUMN build_year TYPE integer,
    ALTER COLUMN missed_scans TYPE integer;
ALTER TABLE housing.offer_detail_fetch ALTER COLUMN failed_attempts TYPE integer;
ALTER TABLE housing.place ALTER COLUMN population_year TYPE integer;
ALTER TABLE housing.place_nearby ALTER COLUMN rank TYPE integer;

-- migrate:down
-- Fails when a stored value does not fit smallint.
ALTER TABLE housing.place_nearby ALTER COLUMN rank TYPE smallint;
ALTER TABLE housing.place ALTER COLUMN population_year TYPE smallint;
ALTER TABLE housing.offer_detail_fetch ALTER COLUMN failed_attempts TYPE smallint;
ALTER TABLE housing.offer
    ALTER COLUMN missed_scans TYPE smallint,
    ALTER COLUMN build_year TYPE smallint,
    ALTER COLUMN building_floors TYPE smallint,
    ALTER COLUMN floor TYPE smallint;
