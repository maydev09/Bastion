-- migrate:up
ALTER TABLE housing.place ADD COLUMN role integer;

-- Where the nearest bigger places are: for each tier (nearest town, county seat, voivodeship capital,
-- metropolis, big cities around) the places in a straight line, nearest first (rank 1).
-- Computed by the ImportPlaces job; a tier the place itself meets has no rows.
CREATE TABLE housing.place_nearby (
    place_id integer NOT NULL REFERENCES housing.place (id) ON DELETE CASCADE,
    tier integer NOT NULL,
    rank smallint NOT NULL,
    nearby_id integer NOT NULL REFERENCES housing.place (id) ON DELETE CASCADE,
    km numeric(5,1) NOT NULL,
    PRIMARY KEY (place_id, tier, rank)
);

CREATE INDEX ON housing.place_nearby (nearby_id);

-- migrate:down
DROP TABLE housing.place_nearby;
ALTER TABLE housing.place DROP COLUMN role;
