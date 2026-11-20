-- migrate:up
-- Places from the official register (SIMC), filled by the ImportPlaces job.
CREATE TABLE housing.place (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    simc text NOT NULL UNIQUE,
    name text NOT NULL,
    -- The name in lower case ASCII words, e.g. "bielsko-biala"; offers are matched by it.
    name_key text NOT NULL,
    voivodeship integer NOT NULL,
    county text,
    municipality text,
    kind integer,
    population integer,
    population_year smallint,
    population_source integer,
    latitude numeric(9,6),
    longitude numeric(9,6)
);

CREATE INDEX ON housing.place (voivodeship, name_key);

-- The portal's own key of the offer's place, e.g. OtoDom's path voivodeship/county/municipality/place.
ALTER TABLE housing.offer ADD COLUMN location_key text;
ALTER TABLE housing.offer ADD COLUMN place_id integer REFERENCES housing.place (id) ON DELETE SET NULL;
-- PostgreSQL does not index foreign keys; every offer list joins the place.
CREATE INDEX ON housing.offer (place_id);

-- migrate:down
ALTER TABLE housing.offer DROP COLUMN place_id;
ALTER TABLE housing.offer DROP COLUMN location_key;
DROP TABLE housing.place;
