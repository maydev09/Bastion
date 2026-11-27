-- migrate:up
-- The collecting jobs got names that say what they are for; their run history keeps going under the new names.
-- FetchDetails and CalculateMonthlyCosts are gone (their work is a step of every collecting run); their old runs stay.
UPDATE housing.job_run SET job = 'ImportNewOffers' WHERE job = 'ImportOffers';
UPDATE housing.job_run SET job = 'SyncAllOffers' WHERE job = 'ScanOffers';

-- migrate:down
UPDATE housing.job_run SET job = 'ScanOffers' WHERE job = 'SyncAllOffers';
UPDATE housing.job_run SET job = 'ImportOffers' WHERE job = 'ImportNewOffers';
