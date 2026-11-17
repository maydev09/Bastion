-- migrate:up
ALTER TABLE flat_offers.offer ADD COLUMN listing_json jsonb;

-- migrate:down
ALTER TABLE flat_offers.offer DROP COLUMN listing_json;
