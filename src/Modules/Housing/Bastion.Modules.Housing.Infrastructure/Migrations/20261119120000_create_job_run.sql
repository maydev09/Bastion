-- migrate:up
-- Runs of the module's background jobs.
CREATE TABLE housing.job_run (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    job text NOT NULL,
    trigger integer NOT NULL,
    status integer NOT NULL,
    queued_at timestamptz NOT NULL,
    started_at timestamptz,
    finished_at timestamptz,
    error_code text,
    summary jsonb
);

CREATE INDEX ON housing.job_run (job, id);

-- migrate:down
DROP TABLE housing.job_run;
