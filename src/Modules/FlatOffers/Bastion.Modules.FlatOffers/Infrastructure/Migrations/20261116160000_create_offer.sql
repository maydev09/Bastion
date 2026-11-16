-- migrate:up
CREATE TABLE flat_offers.offer (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source text NOT NULL CHECK (source IN ('OtoDom')),
    external_id text NOT NULL,
    title text NOT NULL,
    url text NOT NULL,
    price numeric(12,2),
    price_currency text,
    area_m2 numeric(8,2),
    rooms integer,
    listing_hash text NOT NULL,
    first_seen_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    UNIQUE (source, external_id),
    CHECK ((price IS NULL) = (price_currency IS NULL))
);

-- migrate:down
DROP TABLE flat_offers.offer;
