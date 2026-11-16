-- migrate:up
COMMENT ON SCHEMA job_offers IS 'Job offers module';

-- migrate:down
COMMENT ON SCHEMA job_offers IS NULL;
