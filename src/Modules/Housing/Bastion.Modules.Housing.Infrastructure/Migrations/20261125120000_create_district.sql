-- migrate:up
-- Districts of a place and the neighborhoods inside them (a neighborhood has a parent_id), with the names the portals give;
-- added when offers are saved. Names are matched by name_key, as places are.
CREATE TABLE housing.district (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    place_id integer NOT NULL REFERENCES housing.place (id) ON DELETE CASCADE,
    parent_id integer REFERENCES housing.district (id) ON DELETE CASCADE,
    name text NOT NULL,
    name_key text NOT NULL,
    UNIQUE NULLS NOT DISTINCT (place_id, parent_id, name_key)
);

CREATE INDEX ON housing.district (parent_id);

-- The most precise district of the offer: its neighborhood, else its district. The stored listing pages fill it
-- when the offers are parsed again.
ALTER TABLE housing.offer ADD COLUMN district_id integer REFERENCES housing.district (id) ON DELETE SET NULL;
CREATE INDEX ON housing.offer (district_id);

-- migrate:down
ALTER TABLE housing.offer DROP COLUMN district_id;
DROP TABLE housing.district;
