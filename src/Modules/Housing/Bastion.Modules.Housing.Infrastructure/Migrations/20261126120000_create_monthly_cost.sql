-- migrate:up
-- The typical admin rent per m² of a group of offers (the whole country, a voivodeship, a place, a district or a
-- neighborhood, each also by building type), from the offers that state a plausible rent. Replaced as a whole by the
-- CalculateMonthlyCosts job. The columns that do not belong to the basis are null; district_id is the district, or the
-- neighborhood for a neighborhood group.
CREATE TABLE housing.rent_rate (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    basis integer NOT NULL,
    voivodeship integer,
    place_id integer REFERENCES housing.place (id) ON DELETE CASCADE,
    district_id integer REFERENCES housing.district (id) ON DELETE CASCADE,
    building_type integer,
    median_per_m2 numeric(10,2) NOT NULL,
    sample_size integer NOT NULL,
    calculated_at timestamptz NOT NULL,
    UNIQUE NULLS NOT DISTINCT (basis, voivodeship, place_id, district_id, building_type)
);

-- What an offer costs a month: its price plus the admin rent, and where the rent comes from (rent_source). The rent
-- stated on the offer is never changed; a rent read from the description keeps the words it came from (rent_quote).
CREATE TABLE housing.monthly_cost (
    offer_id integer PRIMARY KEY REFERENCES housing.offer (id) ON DELETE CASCADE,
    rent numeric(12,2) NOT NULL,
    rent_source integer NOT NULL,
    total numeric(12,2) NOT NULL,
    currency integer NOT NULL,
    rent_quote text,
    calculated_at timestamptz NOT NULL
);

CREATE INDEX ON housing.monthly_cost (total);

-- How an estimated rent was made: the rate per m² it used, the most local group of offers it took a rate from (basis,
-- the group's rate and size) and the nearest offers it took a rate from (how many, their median rate and the distance to
-- the farthest one).
CREATE TABLE housing.rent_estimate (
    offer_id integer PRIMARY KEY REFERENCES housing.monthly_cost (offer_id) ON DELETE CASCADE,
    rate_per_m2 numeric(10,2) NOT NULL,
    basis integer,
    basis_per_m2 numeric(10,2),
    sample_size integer,
    neighbors integer NOT NULL,
    neighbors_per_m2 numeric(10,2),
    neighbors_km numeric(6,2)
);

-- migrate:down
DROP TABLE housing.rent_estimate;
DROP TABLE housing.monthly_cost;
DROP TABLE housing.rent_rate;
