-- migrate:up
-- numeric(p,s) with a small p rejects values that the C# decimal accepts (a seller may type an area of
-- 1000000 m2), which would stop the import. 29 digits before the point fit any decimal; the scale stays,
-- so stored values and the change history read as before.
ALTER TABLE housing.offer
    ALTER COLUMN price TYPE numeric(31,2),
    ALTER COLUMN area_m2 TYPE numeric(31,2),
    ALTER COLUMN rent TYPE numeric(31,2),
    ALTER COLUMN deposit TYPE numeric(31,2),
    ALTER COLUMN latitude TYPE numeric(35,6),
    ALTER COLUMN longitude TYPE numeric(35,6);
ALTER TABLE housing.place
    ALTER COLUMN latitude TYPE numeric(35,6),
    ALTER COLUMN longitude TYPE numeric(35,6);
ALTER TABLE housing.place_nearby ALTER COLUMN km TYPE numeric(30,1);

-- migrate:down
-- Fails when a stored value does not fit the old precision.
ALTER TABLE housing.place_nearby ALTER COLUMN km TYPE numeric(5,1);
ALTER TABLE housing.place
    ALTER COLUMN longitude TYPE numeric(9,6),
    ALTER COLUMN latitude TYPE numeric(9,6);
ALTER TABLE housing.offer
    ALTER COLUMN longitude TYPE numeric(9,6),
    ALTER COLUMN latitude TYPE numeric(9,6),
    ALTER COLUMN deposit TYPE numeric(12,2),
    ALTER COLUMN rent TYPE numeric(12,2),
    ALTER COLUMN area_m2 TYPE numeric(8,2),
    ALTER COLUMN price TYPE numeric(12,2);
