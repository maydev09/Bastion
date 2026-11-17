-- migrate:up
-- Ids are set here, not generated: they match the Source enum in C# (ADR 0014).
CREATE TABLE housing.source (
    id integer PRIMARY KEY,
    name text NOT NULL UNIQUE
);

INSERT INTO housing.source (id, name) VALUES (1, 'OtoDom');

CREATE TABLE housing.raw_capture (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_id integer NOT NULL REFERENCES housing.source (id),
    kind integer NOT NULL,
    content_type text NOT NULL,
    url text NOT NULL,
    http_status integer NOT NULL,
    fetched_at timestamptz NOT NULL,
    size integer NOT NULL,
    sha256 text NOT NULL
);

CREATE INDEX ON housing.raw_capture (fetched_at);

CREATE TABLE housing.offer (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_id integer NOT NULL REFERENCES housing.source (id),
    external_id text NOT NULL,
    title text NOT NULL,
    url text NOT NULL,
    property_type integer NOT NULL,
    transaction integer NOT NULL,
    price numeric(12,2),
    price_currency integer,
    area_m2 numeric(8,2),
    rooms integer,
    listing_hash text NOT NULL,
    -- The capture that last changed the offer; NULL once retention has deleted it.
    raw_capture_id integer REFERENCES housing.raw_capture (id) ON DELETE SET NULL,
    first_seen_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    UNIQUE (source_id, external_id)
);

CREATE INDEX ON housing.offer (raw_capture_id);

-- migrate:down
DROP TABLE housing.offer;
DROP TABLE housing.raw_capture;
DROP TABLE housing.source;
