-- migrate:up
-- Changes of an offer's content seen on newly fetched pages, e.g. {"price": [3200, 2900]}.
-- A first value (NULL before) is not a change. Offers changed before this table existed have no history.
CREATE TABLE housing.offer_change_log (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    offer_id integer NOT NULL REFERENCES housing.offer (id) ON DELETE CASCADE,
    -- When the page showing the change was fetched.
    changed_at timestamptz NOT NULL,
    -- Column name: [old, new]; features as two sorted arrays.
    changes jsonb NOT NULL
);

CREATE INDEX ON housing.offer_change_log (offer_id);

-- migrate:down
DROP TABLE housing.offer_change_log;
