-- migrate:up
COMMENT ON SCHEMA housing IS 'Housing module';

-- migrate:down
COMMENT ON SCHEMA housing IS NULL;
