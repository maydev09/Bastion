-- migrate:up
-- The usual prices per m² of a group of listed offers (the place, places of one size in a voivodeship or in the country,
-- or the country, each by rooms: 1, 2, 3, 4 for four and more, null for not known): Tukey's fences on ln(price per m²).
-- Recalculated every night by the RecalculateDerivedData job; a saved offer is compared with them at once. A group keeps its
-- row (and id) while it has a fence. The columns that do not belong to the basis are null.
CREATE TABLE housing.price_fence (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    basis integer NOT NULL,
    rooms integer,
    place_id integer REFERENCES housing.place (id) ON DELETE CASCADE,
    voivodeship integer,
    place_size integer,
    low_per_m2 numeric(10,2) NOT NULL,
    high_per_m2 numeric(10,2) NOT NULL,
    currency integer NOT NULL,
    sample_size integer NOT NULL,
    calculated_at timestamptz NOT NULL,
    UNIQUE NULLS NOT DISTINCT (basis, rooms, place_id, voivodeship, place_size)
);

-- An offer the statistics leave out, and why (reason): not a flat for a monthly rent, or numbers that cannot be right.
-- The offer stays listed. Written when the offer is saved and for every listed offer every night; a removed offer keeps
-- its last one. price_fence_id: the fence the offer was compared with.
-- calculated_at: when the inputs were read; a calculation never overwrites a later one.
CREATE TABLE housing.statistics_exclusion (
    offer_id integer PRIMARY KEY REFERENCES housing.offer (id) ON DELETE CASCADE,
    reason integer NOT NULL,
    price_fence_id integer REFERENCES housing.price_fence (id) ON DELETE SET NULL,
    calculated_at timestamptz NOT NULL
);

CREATE INDEX ON housing.statistics_exclusion (price_fence_id);

-- migrate:down
DROP TABLE housing.statistics_exclusion;
DROP TABLE housing.price_fence;
