-- migrate:up
COMMENT ON SCHEMA flat_offers IS 'Flat offers module';

-- migrate:down
COMMENT ON SCHEMA flat_offers IS NULL;
