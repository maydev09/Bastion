-- migrate:up
-- CalculateMonthlyCosts became RecalculateDerivedData (the same calculation, now with the statistics exclusions); its
-- runs keep going under the new name, and their counts keep their keys.
UPDATE housing.job_run SET job = 'RecalculateDerivedData' WHERE job = 'CalculateMonthlyCosts';

-- migrate:down
-- The old runs are the ones without the exclusions in their counts.
UPDATE housing.job_run SET job = 'CalculateMonthlyCosts'
WHERE job = 'RecalculateDerivedData' AND summary IS NOT NULL AND NOT summary ? 'excludedOffers';
