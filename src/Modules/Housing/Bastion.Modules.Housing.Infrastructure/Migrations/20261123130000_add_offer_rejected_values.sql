-- migrate:up
-- Values of the last detail page that the parser refused as implausible, by column name, as the portal wrote them,
-- e.g. {"build_year": "195014"}; the column itself stays empty. NULL when nothing was refused.
ALTER TABLE housing.offer ADD COLUMN rejected_values jsonb;

-- migrate:down
ALTER TABLE housing.offer DROP COLUMN rejected_values;
