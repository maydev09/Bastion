-- migrate:up
CREATE TABLE flat_offers.source (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name text NOT NULL UNIQUE
);

INSERT INTO flat_offers.source (name) VALUES ('OtoDom');

ALTER TABLE flat_offers.offer ADD COLUMN source_id integer REFERENCES flat_offers.source (id);
UPDATE flat_offers.offer o SET source_id = s.id FROM flat_offers.source s WHERE s.name = o.source;
ALTER TABLE flat_offers.offer ALTER COLUMN source_id SET NOT NULL;
ALTER TABLE flat_offers.offer ADD CONSTRAINT offer_source_id_external_id_key UNIQUE (source_id, external_id);
ALTER TABLE flat_offers.offer DROP COLUMN source;

-- migrate:down
ALTER TABLE flat_offers.offer ADD COLUMN source text CHECK (source IN ('OtoDom'));
UPDATE flat_offers.offer o SET source = s.name FROM flat_offers.source s WHERE s.id = o.source_id;
ALTER TABLE flat_offers.offer ALTER COLUMN source SET NOT NULL;
ALTER TABLE flat_offers.offer ADD CONSTRAINT offer_source_external_id_key UNIQUE (source, external_id);
ALTER TABLE flat_offers.offer DROP COLUMN source_id;
DROP TABLE flat_offers.source;
