-- migrate:up
-- Furnished becomes an attribute with a "no" (Answer: 1 yes, 2 no, 3 not stated; NULL not known yet)
-- instead of the amenity 8. Offers with that amenity are furnished; the rest are filled by parsing
-- the stored detail pages again.
ALTER TABLE housing.offer ADD COLUMN furnished integer;

UPDATE housing.offer SET furnished = 1
WHERE id IN (SELECT offer_id FROM housing.offer_feature WHERE feature = 8);

DELETE FROM housing.offer_feature WHERE feature = 8;

-- The change history keeps amenity lists as [old, new]; 8 leaves both, and a change that was only
-- about it is no change any more.
UPDATE housing.offer_change
SET changes = jsonb_set(changes, '{features}', jsonb_build_array(
    (SELECT coalesce(jsonb_agg(f ORDER BY i), '[]') FROM jsonb_array_elements(changes -> 'features' -> 0) WITH ORDINALITY AS a (f, i) WHERE f <> '8'),
    (SELECT coalesce(jsonb_agg(f ORDER BY i), '[]') FROM jsonb_array_elements(changes -> 'features' -> 1) WITH ORDINALITY AS a (f, i) WHERE f <> '8')))
WHERE changes ? 'features';

UPDATE housing.offer_change SET changes = changes - 'features'
WHERE changes -> 'features' -> 0 = changes -> 'features' -> 1;

DELETE FROM housing.offer_change WHERE changes = '{}';

-- migrate:down
INSERT INTO housing.offer_feature (offer_id, feature)
SELECT id, 8 FROM housing.offer WHERE furnished = 1;

ALTER TABLE housing.offer DROP COLUMN furnished;
