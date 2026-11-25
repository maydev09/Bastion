using System.Text.Json;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;
using Dapper;
using Npgsql;

namespace Bastion.Modules.Housing.Infrastructure.Repositories;

public sealed class OfferRepository(NpgsqlDataSource db) : IOfferRepository
{
    // Updates only a changed offer, and never with a page older than the stored data.
    // Returns no row for an unchanged offer, otherwise whether it was inserted (xmax = 0).
    private const string SaveSql = """
        INSERT INTO housing.offer AS o
            (source_id, external_id, title, url, property_type, transaction, price, price_currency, area_m2, rooms,
             rent, rent_currency, floor, external_created_at, external_updated_at, location_key, place_id,
             listing_hash, content_hash, raw_capture_id, created_at, updated_at, last_seen_at)
        VALUES
            (@SourceId, @ExternalId, @Title, @Url, @PropertyType, @Transaction, @Price, @PriceCurrency, @AreaM2, @Rooms,
             @Rent, @RentCurrency, @Floor, @ExternalCreatedAt, @ExternalUpdatedAt, @LocationKey, @PlaceId,
             @ListingHash, @ContentHash, @RawCaptureId, @SeenAt, @SeenAt, @SeenAt)
        ON CONFLICT (source_id, external_id) DO UPDATE SET
            title = excluded.title,
            url = excluded.url,
            property_type = excluded.property_type,
            transaction = excluded.transaction,
            price = excluded.price,
            price_currency = excluded.price_currency,
            area_m2 = excluded.area_m2,
            rooms = excluded.rooms,
            rent = COALESCE(excluded.rent, o.rent),
            rent_currency = COALESCE(excluded.rent_currency, o.rent_currency),
            floor = COALESCE(excluded.floor, o.floor),
            external_created_at = excluded.external_created_at,
            external_updated_at = excluded.external_updated_at,
            listing_hash = excluded.listing_hash,
            content_hash = excluded.content_hash,
            raw_capture_id = excluded.raw_capture_id,
            updated_at = excluded.updated_at
        WHERE o.listing_hash <> excluded.listing_hash
          AND o.updated_at <= excluded.updated_at
        RETURNING (xmax = 0) AS inserted
        """;

    // Listing tags fill the features only until the detail page is fetched: it shows the full set and replaces it.
    // A changed tag changes content_hash, so the details (and the set) are fetched again.
    private const string AddFeaturesSql = """
        INSERT INTO housing.offer_feature (offer_id, feature)
        SELECT o.id, l.feature
        FROM unnest(@ExternalIds, @Features) AS l (external_id, feature)
        JOIN housing.offer o ON o.source_id = @SourceId AND o.external_id = l.external_id
        WHERE NOT EXISTS (SELECT 1 FROM housing.offer_detail_fetch f WHERE f.offer_id = o.id AND f.fetched_at IS NOT NULL)
        ON CONFLICT DO NOTHING
        """;

    // A removed offer seen again is listed again. An older page (re-parse) changes nothing.
    private const string SeenSql = """
        UPDATE housing.offer
        SET last_seen_at = @SeenAt, missed_scans = 0, removed_at = NULL
        WHERE source_id = @SourceId AND external_id = ANY(@ExternalIds) AND last_seen_at < @SeenAt
        """;

    // The location is not part of the listing hash, so it is set here for new and unchanged offers alike.
    private const string LocationSql = """
        UPDATE housing.offer o
        SET location_key = l.location_key, place_id = l.place_id, district_id = l.district_id
        FROM unnest(@ExternalIds, @LocationKeys, @PlaceIds, @DistrictIds) AS l (external_id, location_key, place_id, district_id)
        WHERE o.source_id = @SourceId AND o.external_id = l.external_id
          AND (o.location_key, o.place_id, o.district_id) IS DISTINCT FROM (l.location_key, l.place_id, l.district_id)
        """;

    // The right side of SET reads the values from before the update.
    private const string MarkMissedSql = """
        UPDATE housing.offer
        SET missed_scans = missed_scans + 1,
            removed_at = CASE WHEN missed_scans + 1 >= @MissedScansToRemove THEN last_seen_at END
        WHERE source_id = @SourceId AND removed_at IS NULL AND last_seen_at < @ScanStartedAt
        RETURNING removed_at IS NOT NULL AS removed
        """;

    // An offer points to its neighborhood when it has one; the neighborhood's parent is its district.
    private const string OfferColumns = """
        o.id, o.source_id AS source, o.title, o.url, o.property_type, o.transaction, o.price, o.price_currency,
        o.area_m2, o.rooms, o.created_at, o.updated_at, o.external_created_at, o.external_updated_at,
        o.place_id, p.name AS place_name, p.kind AS place_kind, p.role AS place_role, p.voivodeship, p.county, p.municipality,
        p.population, p.population_year, p.population_source, o.rent, o.rent_currency, o.furnished,
        coalesce(dp.name, d.name) AS district, CASE WHEN dp.id IS NOT NULL THEN d.name END AS neighborhood,
        (SELECT (c.changes -> 'price' ->> 0)::numeric
         FROM housing.offer_change c
         WHERE c.offer_id = o.id AND c.changes ? 'price'
         ORDER BY c.changed_at DESC, c.id DESC
         LIMIT 1) AS previous_price
        """;

    // The page is cut first and only its offers are joined with their places.
    // The WHERE conditions from BuildWhere pick the offers; the ORDER BY list from BuildOrderBy picks the page and orders its rows.
    private static string BuildListSql(string where, string orderBy) => $$"""
        SELECT {{OfferColumns}}
        FROM (
            SELECT *
            FROM housing.offer o
            WHERE {{where}}
            ORDER BY {{orderBy}}
            LIMIT @PageSize OFFSET @Offset
        ) o
        LEFT JOIN housing.place p ON p.id = o.place_id
        LEFT JOIN housing.district d ON d.id = o.district_id
        LEFT JOIN housing.district dp ON dp.id = d.parent_id
        ORDER BY {{orderBy}};

        SELECT count(*)::int FROM housing.offer o WHERE {{where}};
        """;

    // A place filter picks places first: a few hundred of them have offers, so the offers are found by their place_id.
    // A picked district or neighborhood narrows its own place: the place counts whole only while none of its districts or
    // neighborhoods is picked. A district takes in its neighborhoods, unless one of them is picked: then it is narrowed the same way.
    private const string LocationCondition = """
        (o.place_id IN (
            SELECT id
            FROM housing.place
            WHERE voivodeship = ANY(@Voivodeships)
               OR id = ANY(@PlaceIds)
                  AND id NOT IN (SELECT place_id FROM housing.district WHERE id = ANY(@DistrictIds) OR id = ANY(@NeighborhoodIds)))
         OR o.district_id IN (
            SELECT id
            FROM housing.district
            WHERE id = ANY(@NeighborhoodIds)
               OR (id = ANY(@DistrictIds) OR parent_id = ANY(@DistrictIds))
                  AND coalesce(parent_id, id) NOT IN (
                      SELECT parent_id FROM housing.district WHERE id = ANY(@NeighborhoodIds) AND parent_id IS NOT NULL)))
        """;

    private const string PlaceSizeCondition = """
        o.place_id IN (
            SELECT p.id
            FROM housing.place p
            JOIN unnest(@SizeKinds, @SizeMins, @SizeMaxes) AS s (kind, min, max)
                ON p.kind = s.kind AND p.population >= s.min AND (s.max IS NULL OR p.population < s.max))
        """;

    private const string PlaceRoleCondition = "o.place_id IN (SELECT id FROM housing.place WHERE role = ANY(@Roles))";

    // The price per m² as the list shows it; an offer without an area has none.
    private const string PricePerM2 = "o.price / NULLIF(o.area_m2, 0)";

    private const string AllFeaturesCondition = """
        (SELECT count(*) FROM housing.offer_feature f WHERE f.offer_id = o.id AND f.feature = ANY(@Features)) = @FeatureCount
        """;

    private const string AnyFeatureCondition = """
        EXISTS (SELECT 1 FROM housing.offer_feature f WHERE f.offer_id = o.id AND f.feature = ANY(@Features))
        """;

    private const string ChangedCondition = "EXISTS (SELECT 1 FROM housing.offer_change c WHERE c.offer_id = o.id)";

    // housing.offer_change keeps a price change as [old, new]; a price removed (new is null) is no cut.
    private const string PriceCutCondition = """
        EXISTS (
            SELECT 1
            FROM housing.offer_change c
            WHERE c.offer_id = o.id AND (c.changes -> 'price' ->> 1)::numeric < (c.changes -> 'price' ->> 0)::numeric)
        """;

    // A removed offer is still shown, e.g. from an old link.
    private const string GetSql = $"""
        SELECT {OfferColumns},
            o.deposit, o.deposit_currency, o.available_from, o.floor, o.building_floors,
            o.build_year, o.building_type, o.heating, o.condition, o.latitude, o.longitude, o.location_radius_m,
            o.description, f.fetched_at AS details_fetched_at
        FROM housing.offer o
        LEFT JOIN housing.place p ON p.id = o.place_id
        LEFT JOIN housing.district d ON d.id = o.district_id
        LEFT JOIN housing.district dp ON dp.id = d.parent_id
        LEFT JOIN housing.offer_detail_fetch f ON f.offer_id = o.id
        WHERE o.id = @Id;

        SELECT feature FROM housing.offer_feature WHERE offer_id = @Id ORDER BY feature;

        SELECT id, changed_at, changes::text AS changes
        FROM housing.offer_change
        WHERE offer_id = @Id
        ORDER BY changed_at DESC, id DESC;
        """;

    private const string NearbySql = """
        SELECT o.id AS offer_id, n.tier, c.name, n.km
        FROM housing.offer o
        JOIN housing.place_nearby n ON n.place_id = o.place_id
        JOIN housing.place c ON c.id = n.nearby_id
        WHERE o.id = ANY(@OfferIds)
        ORDER BY o.id, n.tier, n.rank
        """;

    // Newest first, like the listing, so new offers get their details before the backlog.
    // An offer stored before content_hash existed waits until an import fills it.
    // An offer that used up its attempts comes back once its last attempt is older than RetryGivenUpBefore.
    private const string PendingDetailsSql = """
        SELECT o.id AS offer_id, o.external_id, o.url, o.content_hash
        FROM housing.offer o
        LEFT JOIN housing.offer_detail_fetch f ON f.offer_id = o.id
        WHERE o.source_id = @SourceId AND o.removed_at IS NULL AND o.content_hash IS NOT NULL
          AND (f.offer_id IS NULL
               OR f.content_hash <> o.content_hash
               OR (f.failed_attempts BETWEEN 1 AND @MaxFailedAttempts - 1 AND f.attempted_at < @RunStartedAt)
               OR (f.failed_attempts >= @MaxFailedAttempts AND f.attempted_at < @RetryGivenUpBefore))
        ORDER BY o.external_created_at DESC NULLS LAST, o.id DESC
        LIMIT @Limit
        """;

    // A null never clears a stored value: another page may not show a field this one showed.
    // A rejected value does: the page shows the field, and the stored value is no longer what it says.
    private const string SaveDetailsSql = """
        UPDATE housing.offer
        SET rent = COALESCE(@Rent, rent),
            rent_currency = COALESCE(@RentCurrency, rent_currency),
            deposit = COALESCE(@Deposit, deposit),
            deposit_currency = COALESCE(@DepositCurrency, deposit_currency),
            available_from = COALESCE(@AvailableFrom, available_from),
            floor = COALESCE(@Floor, floor),
            building_floors = CASE WHEN @RejectedValues::jsonb ? 'building_floors' THEN NULL ELSE COALESCE(@BuildingFloors, building_floors) END,
            build_year = CASE WHEN @RejectedValues::jsonb ? 'build_year' THEN NULL ELSE COALESCE(@BuildYear, build_year) END,
            building_type = COALESCE(@BuildingType, building_type),
            heating = COALESCE(@Heating, heating),
            condition = COALESCE(@Condition, condition),
            latitude = COALESCE(@Latitude, latitude),
            longitude = COALESCE(@Longitude, longitude),
            location_radius_m = CASE WHEN @RejectedValues::jsonb ? 'location_radius_m' THEN NULL ELSE COALESCE(@LocationRadiusM, location_radius_m) END,
            description = COALESCE(@Description, description),
            furnished = COALESCE(@Furnished, furnished),
            rejected_values = @RejectedValues::jsonb
        WHERE source_id = @SourceId AND external_id = @ExternalId
        RETURNING id
        """;

    private const string ReplaceFeaturesSql = """
        DELETE FROM housing.offer_feature WHERE offer_id = @OfferId;
        INSERT INTO housing.offer_feature (offer_id, feature)
        SELECT @OfferId, unnest(@Features);
        """;

    // An offer deleted during the run gets no row (and breaks no foreign key).
    private const string DetailsFetchedSql = """
        INSERT INTO housing.offer_detail_fetch AS f (offer_id, content_hash, attempted_at, fetched_at, failed_attempts, last_error)
        SELECT @OfferId, @ContentHash, @At, @At, 0, NULL
        WHERE EXISTS (SELECT 1 FROM housing.offer WHERE id = @OfferId)
        ON CONFLICT (offer_id) DO UPDATE SET
            content_hash = excluded.content_hash,
            attempted_at = excluded.attempted_at,
            fetched_at = excluded.fetched_at,
            failed_attempts = 0,
            last_error = NULL
        """;

    // Failures are counted per content version: a changed offer gets fresh attempts.
    private const string DetailsFailedSql = """
        INSERT INTO housing.offer_detail_fetch AS f (offer_id, content_hash, attempted_at, fetched_at, failed_attempts, last_error)
        SELECT @OfferId, @ContentHash, @At, NULL, 1, @LastError
        WHERE EXISTS (SELECT 1 FROM housing.offer WHERE id = @OfferId)
        ON CONFLICT (offer_id) DO UPDATE SET
            failed_attempts = CASE WHEN f.content_hash = excluded.content_hash THEN f.failed_attempts + 1 ELSE 1 END,
            content_hash = excluded.content_hash,
            attempted_at = excluded.attempted_at,
            last_error = excluded.last_error
        """;

    // As MarkMissedSql: removed_at is when the offer was last seen.
    private const string MarkRemovedSql = """
        UPDATE housing.offer SET removed_at = last_seen_at WHERE id = @OfferId AND removed_at IS NULL
        """;

    // The fields whose changes are logged, read before and after a save (locked, so another job cannot change them in between).
    // Not logged: the portal's dates (a bump), the URL, the location (not part of the content) and the place (our match).
    // Features count only after the first detail page: before it they are listing tags, a part of the full set.
    // available_from as text: read without a type, a date comes as DateTime and would be logged with a time.
    private const string ContentSql = """
        SELECT o.id, o.title, o.property_type, o.transaction, o.price, o.price_currency, o.area_m2, o.rooms,
            o.rent, o.rent_currency, o.deposit, o.deposit_currency, o.available_from::text AS available_from, o.floor, o.building_floors,
            o.build_year, o.building_type, o.heating, o.condition, o.latitude, o.longitude, o.location_radius_m,
            o.description, o.furnished,
            CASE WHEN f.fetched_at IS NOT NULL
                THEN ARRAY(SELECT feature FROM housing.offer_feature WHERE offer_id = o.id ORDER BY feature)
            END AS features,
            f.fetched_at IS NOT NULL AS has_details
        FROM housing.offer o
        LEFT JOIN housing.offer_detail_fetch f ON f.offer_id = o.id
        WHERE o.source_id = @SourceId AND o.external_id = ANY(@ExternalIds)
        FOR UPDATE OF o
        """;

    private const string RecordChangesSql = """
        INSERT INTO housing.offer_change (offer_id, changed_at, changes)
        SELECT l.offer_id, @ChangedAt, l.changes
        FROM unnest(@OfferIds, @Changes::jsonb[]) AS l (offer_id, changes)
        """;

    private const string LocationsSql = """
        SELECT source_id AS source, location_key, count(*)::int AS offer_count
        FROM housing.offer
        WHERE location_key IS NOT NULL
        GROUP BY source_id, location_key
        """;

    // A district belongs to the old place, so it is cleared; the next listing page of the offer sets it again.
    private const string SetPlacesSql = """
        UPDATE housing.offer o
        SET place_id = l.place_id, district_id = NULL
        FROM unnest(@SourceIds, @LocationKeys, @PlaceIds) AS l (source_id, location_key, place_id)
        WHERE o.source_id = l.source_id AND o.location_key = l.location_key
          AND o.place_id IS DISTINCT FROM l.place_id
        """;

    public async Task<SaveCounts> SaveAsync(IReadOnlyList<Offer> offers, RawCapture capture, bool recordChanges, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var externalIds = offers.Select(o => o.ExternalId).ToArray();
        var before = recordChanges ? await ReadContentAsync(connection, transaction, capture.SourceId, externalIds, cancellationToken) : [];

        int added = 0, updated = 0;
        foreach (var offer in offers)
        {
            var inserted = await connection.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(SaveSql, new
            {
                SourceId = (int)offer.SourceId,
                offer.ExternalId,
                offer.Title,
                Url = offer.Url.ToString(),
                PropertyType = (int)offer.PropertyType,
                Transaction = (int)offer.Transaction,
                Price = offer.Price?.Amount,
                PriceCurrency = (int?)offer.Price?.Currency,
                offer.AreaM2,
                offer.Rooms,
                Rent = offer.Rent?.Amount,
                RentCurrency = (int?)offer.Rent?.Currency,
                offer.Floor,
                offer.ExternalCreatedAt,
                offer.ExternalUpdatedAt,
                offer.LocationKey,
                offer.PlaceId,
                ListingHash = offer.ListingHash(),
                ContentHash = offer.ContentHash(),
                RawCaptureId = capture.Id,
                SeenAt = capture.FetchedAt,
            }, transaction, cancellationToken: cancellationToken));

            if (inserted == true)
            {
                added++;
            }
            else if (inserted == false)
            {
                updated++;
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(SeenSql, new
        {
            SourceId = (int)capture.SourceId,
            ExternalIds = externalIds,
            SeenAt = capture.FetchedAt,
        }, transaction, cancellationToken: cancellationToken));

        var features = offers.SelectMany(o => (o.Features ?? []).Select(f => (o.ExternalId, Feature: (int)f))).ToList();
        await connection.ExecuteAsync(new CommandDefinition(AddFeaturesSql, new
        {
            SourceId = (int)capture.SourceId,
            ExternalIds = features.Select(f => f.ExternalId).ToArray(),
            Features = features.Select(f => f.Feature).ToArray(),
        }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(LocationSql, new
        {
            SourceId = (int)capture.SourceId,
            ExternalIds = externalIds,
            LocationKeys = offers.Select(o => o.LocationKey).ToArray(),
            PlaceIds = offers.Select(o => o.PlaceId).ToArray(),
            DistrictIds = offers.Select(o => o.DistrictId).ToArray(),
        }, transaction, cancellationToken: cancellationToken));

        if (recordChanges && updated > 0)
        {
            await RecordChangesAsync(connection, transaction, capture, externalIds, before, isDetailPage: false, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SaveCounts(added, updated, offers.Count - added - updated);
    }

    private static async Task<Dictionary<int, IDictionary<string, object?>>> ReadContentAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Source source, string[] externalIds, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync(new CommandDefinition(ContentSql, new
        {
            SourceId = (int)source,
            ExternalIds = externalIds,
        }, transaction, cancellationToken: cancellationToken));
        return rows.Cast<IDictionary<string, object?>>().ToDictionary(r => (int)r["id"]!);
    }

    private static async Task RecordChangesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RawCapture capture,
        string[] externalIds,
        Dictionary<int, IDictionary<string, object?>> before,
        bool isDetailPage,
        CancellationToken cancellationToken)
    {
        var after = await ReadContentAsync(connection, transaction, capture.SourceId, externalIds, cancellationToken);
        var changes = after
            .Where(a => before.ContainsKey(a.Key))
            .Select(a => (OfferId: a.Key, Changes: BuildChangeMap(before[a.Key], a.Value, isDetailPage)))
            .Where(c => c.Changes.Count > 0)
            .ToList();
        if (changes.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(RecordChangesSql, new
        {
            ChangedAt = capture.FetchedAt,
            OfferIds = changes.Select(c => c.OfferId).ToArray(),
            Changes = changes.Select(c => JsonSerializer.Serialize(c.Changes)).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
    }

    // A value where there was none is a change (a seller adds the heating), except on the first detail page of an
    // offer: it fills all detail fields at once. A new offer has no "before" and so no changes.
    private static Dictionary<string, object?[]> BuildChangeMap(
        IDictionary<string, object?> before, IDictionary<string, object?> after, bool isDetailPage)
    {
        var isFirstDetailPage = isDetailPage && !(bool)before["has_details"]!;
        return after
            .Where(f => f.Key is not ("id" or "has_details"))
            .Where(f => !(isFirstDetailPage && before[f.Key] is null))
            .Where(f => !AreEqual(before[f.Key], f.Value))
            .ToDictionary(f => f.Key, f => new[] { before[f.Key], f.Value });
    }

    private static bool AreEqual(object? old, object? value) => old switch
    {
        null => value is null,
        int[] oldFeatures when value is int[] features => oldFeatures.SequenceEqual(features),
        _ => old.Equals(value),
    };

    public async Task<MissedCounts> MarkMissedAsync(Source source, DateTimeOffset scanStartedAt, int missedScansToRemove, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var removed = (await connection.QueryAsync<bool>(new CommandDefinition(MarkMissedSql, new
        {
            SourceId = (int)source,
            ScanStartedAt = scanStartedAt,
            MissedScansToRemove = missedScansToRemove,
        }, cancellationToken: cancellationToken))).ToList();

        return new MissedCounts(removed.Count, removed.Count(r => r));
    }

    public async Task<PagedResult<OfferListItem>> ListAsync(
        OfferFilter filter,
        IReadOnlyList<PopulationRange> placeSizes,
        PageRequest page,
        OfferSort sort,
        SortDirection direction,
        CancellationToken cancellationToken)
    {
        var sql = BuildListSql(BuildWhere(filter, placeSizes), BuildOrderBy(sort, direction));
        var parameters = BuildParameters(filter, placeSizes);
        parameters.Add("PageSize", page.PageSize);
        parameters.Add("Offset", page.Offset);
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var items = (await results.ReadAsync<OfferListItem>()).ToList();
        var total = await results.ReadSingleAsync<int>();
        return new PagedResult<OfferListItem>(await WithNearbyAsync(connection, items, cancellationToken), page.Page, page.PageSize, total);
    }

    // Each group is counted under the filter without its own choices where they join with OR (see OfferFacets).
    public async Task<OfferFacets> CountFacetsAsync(
        OfferFilter filter,
        IReadOnlyList<PopulationRange> placeSizes,
        IReadOnlyDictionary<PlaceSize, PopulationRange> allSizes,
        IReadOnlyList<DateTimeOffset> countAddedSince,
        CancellationToken cancellationToken)
    {
        var withoutLocation = BuildWhere(filter with { Voivodeships = [], PlaceIds = [], DistrictIds = [], NeighborhoodIds = [], WithoutPlace = false }, placeSizes);
        var withoutSize = BuildWhere(filter, []);
        var withoutRole = BuildWhere(filter with { PlaceRoles = [] }, placeSizes);
        var withoutFurnished = BuildWhere(filter with { Furnished = null }, placeSizes);
        var forFeatures = BuildWhere(filter.FeatureMatch == FeatureMatch.Any ? filter with { Features = [] } : filter, placeSizes);
        var withoutChanged = BuildWhere(filter with { Changed = null }, placeSizes);
        var withPriceCut = BuildWhere(filter with { PriceCut = true }, placeSizes);
        var withoutPrice = BuildWhere(filter with { PriceMin = null, PriceMax = null }, placeSizes) + " AND o.price_currency = @PriceCurrency";
        var withoutPricePerM2 = BuildWhere(filter with { PricePerM2Min = null, PricePerM2Max = null }, placeSizes)
            + " AND o.price_currency = @PriceCurrency";
        var withoutArea = BuildWhere(filter with { AreaMin = null, AreaMax = null }, placeSizes);
        var withoutAddedSince = BuildWhere(filter with { AddedSince = null }, placeSizes);
        var sql = $$"""
            SELECT p.voivodeship, count(*)::int
            FROM housing.offer o
            JOIN housing.place p ON p.id = o.place_id
            WHERE {{withoutLocation}} AND p.voivodeship IS NOT NULL
            GROUP BY p.voivodeship;

            SELECT count(*)::int FROM housing.offer o WHERE {{withoutLocation}} AND o.place_id IS NULL;

            SELECT s.size, count(*)::int
            FROM housing.offer o
            JOIN housing.place p ON p.id = o.place_id
            JOIN unnest(@AllSizes, @AllSizeKinds, @AllSizeMins, @AllSizeMaxes) AS s (size, kind, min, max)
                ON p.kind = s.kind AND p.population >= s.min AND (s.max IS NULL OR p.population < s.max)
            WHERE {{withoutSize}}
            GROUP BY s.size;

            SELECT p.role, count(*)::int
            FROM housing.offer o
            JOIN housing.place p ON p.id = o.place_id
            WHERE {{withoutRole}} AND p.role IS NOT NULL
            GROUP BY p.role;

            SELECT o.furnished, count(*)::int
            FROM housing.offer o
            WHERE {{withoutFurnished}} AND o.furnished IS NOT NULL
            GROUP BY o.furnished;

            SELECT listed.feature, count(*)::int
            FROM housing.offer o
            JOIN housing.offer_feature listed ON listed.offer_id = o.id
            WHERE {{forFeatures}}
            GROUP BY listed.feature;

            SELECT {{ChangedCondition}} AS changed, count(*)::int
            FROM housing.offer o
            WHERE {{withoutChanged}}
            GROUP BY 1;

            SELECT count(*)::int FROM housing.offer o WHERE {{withPriceCut}};

            {{BuildHistogramSql("o.price", withoutPrice)}}

            {{BuildHistogramSql(PricePerM2, withoutPricePerM2)}}

            {{BuildHistogramSql("o.area_m2", withoutArea)}}

            {{string.Concat(countAddedSince.Select((_, i) => $"SELECT count(*)::int FROM housing.offer o WHERE {withoutAddedSince} AND o.created_at >= @CountAddedSince{i};\n"))}}
            """;
        var parameters = BuildParameters(filter, placeSizes);
        parameters.Add("AllSizes", allSizes.Keys.Select(s => (int)s).ToArray());
        parameters.Add("AllSizeKinds", allSizes.Values.Select(s => (int)s.Kind).ToArray());
        parameters.Add("AllSizeMins", allSizes.Values.Select(s => s.Min).ToArray());
        parameters.Add("AllSizeMaxes", allSizes.Values.Select(s => s.Max).ToArray());
        parameters.Add("Widths", HistogramWidths);
        parameters.Add("MinBars", HistogramMinBars);
        for (var i = 0; i < countAddedSince.Count; i++)
        {
            parameters.Add($"CountAddedSince{i}", countAddedSince[i]);
        }

        parameters.Add("MaxBars", HistogramMaxBars);
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var voivodeships = (await results.ReadAsync<(int Value, int Count)>()).ToDictionary(r => (Voivodeship)r.Value, r => r.Count);
        var withoutPlace = await results.ReadSingleAsync<int>();
        var sizes = (await results.ReadAsync<(int Value, int Count)>()).ToDictionary(r => (PlaceSize)r.Value, r => r.Count);
        var roles = (await results.ReadAsync<(int Value, int Count)>()).ToDictionary(r => (PlaceRole)r.Value, r => r.Count);
        var furnished = (await results.ReadAsync<(int Value, int Count)>()).ToDictionary(r => (Answer)r.Value, r => r.Count);
        var features = (await results.ReadAsync<(int Value, int Count)>()).ToDictionary(r => (Feature)r.Value, r => r.Count);
        var changed = (await results.ReadAsync<(bool Changed, int Count)>()).ToDictionary(r => r.Changed, r => r.Count);
        return new OfferFacets
        {
            Voivodeships = voivodeships,
            WithoutPlace = withoutPlace,
            PlaceSizes = sizes,
            PlaceRoles = roles,
            Furnished = furnished,
            Features = features,
            Changed = changed.GetValueOrDefault(true),
            Unchanged = changed.GetValueOrDefault(false),
            PriceCut = await results.ReadSingleAsync<int>(),
            Price = ReadHistogram(await results.ReadAsync<HistogramRow>()),
            PricePerM2 = ReadHistogram(await results.ReadAsync<HistogramRow>()),
            AreaM2 = ReadHistogram(await results.ReadAsync<HistogramRow>()),
            AddedSince = await ReadCountsAsync(results, countAddedSince.Count),
        };
    }

    // The widths a histogram bucket may have, so its edges are round numbers ("2 500 zł", "40 m²").
    private static readonly decimal[] HistogramWidths =
        [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1_000, 2_000, 2_500, 5_000, 10_000, 20_000, 50_000, 100_000];

    // The number of bars grows with the offers (Rice rule: 2 * cube root of n), so a few offers get a few wide bars.
    private const int HistogramMinBars = 5;

    private const int HistogramMaxBars = 30;

    // The scale spans the usual values (Tukey's fences: from Q1 - 1.5 * IQR to Q3 + 1.5 * IQR, within the lowest
    // and the highest value), so a few extreme offers do not squeeze the rest into one bar; the values beyond the
    // fences fall into the first and the last bar. The width is the narrowest round one that fits in the bars.
    private static string BuildHistogramSql(string value, string where) => $$"""
        WITH v AS (
            SELECT {{value}} AS value FROM housing.offer o WHERE {{where}} AND {{value}} IS NOT NULL
        ), q AS (
            SELECT percentile_cont(0.25) WITHIN GROUP (ORDER BY value)::numeric AS q1,
                percentile_cont(0.75) WITHIN GROUP (ORDER BY value)::numeric AS q3,
                min(value) AS lowest, max(value) AS highest,
                least(@MaxBars, greatest(@MinBars, ceil(2 * cbrt(count(*))))) AS bars
            FROM v
        ), r AS (
            SELECT greatest(q.lowest, q.q1 - 1.5 * (q.q3 - q.q1)) AS low,
                least(q.highest, q.q3 + 1.5 * (q.q3 - q.q1)) AS high,
                q.bars
            FROM q
        ), s AS (
            SELECT floor(r.low / w.width) * w.width AS start, w.width,
                greatest(ceil(r.high / w.width), floor(r.low / w.width) + 1) * w.width AS stop
            -- A range too wide for every round width (e.g. a price typed in millions) gets a plain one.
            FROM r, LATERAL (
                SELECT coalesce(min(c), ceil((r.high - r.low) / r.bars::numeric)) AS width
                FROM unnest(@Widths) AS c
                WHERE (r.high - r.low) / c <= r.bars) w
        )
        SELECT s.start, s.width, s.stop,
            least(greatest(floor((v.value - s.start) / s.width), 0), (s.stop - s.start) / s.width - 1)::int AS bucket,
            count(*)::int AS count
        FROM v, s
        GROUP BY s.start, s.width, s.stop, bucket
        ORDER BY bucket;
        """;

    private static Histogram? ReadHistogram(IEnumerable<HistogramRow> rows)
    {
        var buckets = rows.ToList();
        if (buckets.Count == 0)
        {
            return null;
        }

        var (start, width, stop) = (buckets[0].Start, buckets[0].Width, buckets[0].Stop);
        var counts = new int[(int)((stop - start) / width)];
        foreach (var bucket in buckets)
        {
            counts[bucket.Bucket] = bucket.Count;
        }

        return new Histogram(start, width, counts);
    }

    private static async Task<List<int>> ReadCountsAsync(SqlMapper.GridReader results, int count)
    {
        var counts = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            counts.Add(await results.ReadSingleAsync<int>());
        }

        return counts;
    }

    private sealed record HistogramRow(decimal Start, decimal Width, decimal Stop, int Bucket, int Count);

    public async Task<OfferSummary> SummarizeAsync(
        OfferFilter filter, IReadOnlyList<PopulationRange> placeSizes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sql = $$"""
            SELECT count(*)::int AS count,
                {{BuildSpreadSql("o.price", "o.price_currency = @PriceCurrency", "price")}},
                {{BuildSpreadSql(PricePerM2, "o.price_currency = @PriceCurrency", "price_per_m2")}},
                {{BuildSpreadSql("o.rent", "o.rent_currency = @PriceCurrency", "rent")}},
                {{BuildSpreadSql("o.area_m2", "TRUE", "area")}},
                {{BuildSpreadSql("extract(epoch FROM @Now - coalesce(o.external_created_at, o.created_at)) / 86400", "TRUE", "days_listed")}}
            FROM housing.offer o
            WHERE {{BuildWhere(filter, placeSizes)}};
            """;
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var parameters = BuildParameters(filter, placeSizes);
        parameters.Add("Now", now);
        var row = await connection.QuerySingleAsync<SummaryRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return new OfferSummary
        {
            Count = row.Count,
            Price = ReadSpread(row.Price, row.PriceCount),
            PricePerM2 = ReadSpread(row.PricePerM2, row.PricePerM2Count),
            Rent = ReadSpread(row.Rent, row.RentCount),
            AreaM2 = ReadSpread(row.Area, row.AreaCount),
            DaysListed = ReadSpread(row.DaysListed, row.DaysListedCount),
        };
    }

    // The quartiles and the number of offers with the value; condition picks the offers it is counted for (e.g. its currency).
    private static string BuildSpreadSql(string value, string condition, string name) => $"""
        percentile_cont(ARRAY[0.25, 0.5, 0.75]) WITHIN GROUP (ORDER BY {value}) FILTER (WHERE {condition}) AS {name},
        count({value}) FILTER (WHERE {condition})::int AS {name}_count
        """;

    private static Spread? ReadSpread(double[]? quartiles, int count) =>
        quartiles is [var low, var median, var high] && count > 0
            ? new Spread(Math.Round((decimal)low, 2), Math.Round((decimal)median, 2), Math.Round((decimal)high, 2), count)
            : null;

    // Properties, not a constructor: Dapper matches snake_case columns (price_per_m2_count) to property names.
    private sealed class SummaryRow
    {
        public int Count { get; init; }

        public double[]? Price { get; init; }

        public int PriceCount { get; init; }

        public double[]? PricePerM2 { get; init; }

        public int PricePerM2Count { get; init; }

        public double[]? Rent { get; init; }

        public int RentCount { get; init; }

        public double[]? Area { get; init; }

        public int AreaCount { get; init; }

        public double[]? DaysListed { get; init; }

        public int DaysListedCount { get; init; }
    }

    // The values of every condition BuildWhere may add.
    private static DynamicParameters BuildParameters(OfferFilter filter, IReadOnlyList<PopulationRange> placeSizes)
    {
        var features = filter.Features.Distinct().Select(f => (int)f).ToArray();
        return new DynamicParameters(new
        {
            Voivodeships = filter.Voivodeships.Select(v => (int)v).ToArray(),
            PlaceIds = filter.PlaceIds.ToArray(),
            DistrictIds = filter.DistrictIds.ToArray(),
            NeighborhoodIds = filter.NeighborhoodIds.ToArray(),
            SizeKinds = placeSizes.Select(s => (int)s.Kind).ToArray(),
            SizeMins = placeSizes.Select(s => s.Min).ToArray(),
            SizeMaxes = placeSizes.Select(s => s.Max).ToArray(),
            Roles = filter.PlaceRoles.Select(r => (int)r).ToArray(),
            PriceCurrency = (int)OfferFilter.PriceCurrency,
            filter.PriceMin,
            filter.PriceMax,
            filter.PricePerM2Min,
            filter.PricePerM2Max,
            filter.AreaMin,
            filter.AreaMax,
            Rooms = filter.Rooms.ToArray(),
            filter.RoomsMin,
            Features = features,
            FeatureCount = features.Length,
            Furnished = (int?)filter.Furnished,
            filter.AddedSince,
            filter.ExcludeOffer,
        });
    }

    // Only the filters in use become conditions, so the plain list stays a plain query.
    private static string BuildWhere(OfferFilter filter, IReadOnlyList<PopulationRange> placeSizes)
    {
        var conditions = new List<string> { "o.removed_at IS NULL" };
        var byPlace = filter.Voivodeships.Count > 0 || filter.PlaceIds.Count > 0 || filter.DistrictIds.Count > 0 || filter.NeighborhoodIds.Count > 0;
        if (byPlace && filter.WithoutPlace)
        {
            conditions.Add($"({LocationCondition.Trim()} OR o.place_id IS NULL)");
        }
        else if (byPlace)
        {
            conditions.Add(LocationCondition);
        }
        else if (filter.WithoutPlace)
        {
            conditions.Add("o.place_id IS NULL");
        }

        if (placeSizes.Count > 0)
        {
            conditions.Add(PlaceSizeCondition);
        }

        if (filter.PlaceRoles.Count > 0)
        {
            conditions.Add(PlaceRoleCondition);
        }

        if (filter.PriceMin is not null || filter.PriceMax is not null || filter.PricePerM2Min is not null || filter.PricePerM2Max is not null)
        {
            conditions.Add("o.price_currency = @PriceCurrency");
        }

        if (filter.PriceMin is not null)
        {
            conditions.Add("o.price >= @PriceMin");
        }

        if (filter.PriceMax is not null)
        {
            conditions.Add("o.price <= @PriceMax");
        }

        if (filter.PricePerM2Min is not null)
        {
            conditions.Add($"round({PricePerM2}, 2) >= @PricePerM2Min");
        }

        if (filter.PricePerM2Max is not null)
        {
            conditions.Add($"round({PricePerM2}, 2) <= @PricePerM2Max");
        }

        if (filter.AreaMin is not null)
        {
            conditions.Add("o.area_m2 >= @AreaMin");
        }

        if (filter.AreaMax is not null)
        {
            conditions.Add("o.area_m2 <= @AreaMax");
        }

        if (filter.Rooms.Count > 0 && filter.RoomsMin is not null)
        {
            conditions.Add("(o.rooms = ANY(@Rooms) OR o.rooms >= @RoomsMin)");
        }
        else if (filter.Rooms.Count > 0)
        {
            conditions.Add("o.rooms = ANY(@Rooms)");
        }
        else if (filter.RoomsMin is not null)
        {
            conditions.Add("o.rooms >= @RoomsMin");
        }

        if (filter.Furnished is not null)
        {
            conditions.Add("o.furnished = @Furnished");
        }

        if (filter.Features.Count > 0)
        {
            conditions.Add(filter.FeatureMatch == FeatureMatch.Any ? AnyFeatureCondition : AllFeaturesCondition);
        }

        if (filter.AddedSince is not null)
        {
            conditions.Add("o.created_at >= @AddedSince");
        }

        if (filter.ExcludeOffer is not null)
        {
            conditions.Add("o.id <> @ExcludeOffer");
        }

        if (filter.Changed is { } changed)
        {
            conditions.Add(changed ? ChangedCondition : $"NOT {ChangedCondition}");
        }

        if (filter.PriceCut)
        {
            conditions.Add(PriceCutCondition);
        }

        return string.Join(" AND ", conditions.Select(c => c.Trim()));
    }

    public async Task<OfferListItem?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));
        var offer = await results.ReadSingleOrDefaultAsync<OfferListItem>();
        if (offer is null)
        {
            return null;
        }

        var features = (await results.ReadAsync<int>()).Select(f => (Feature)f).ToList();
        var changes = (await results.ReadAsync<(int Id, DateTimeOffset ChangedAt, string Changes)>())
            .Select(c => new OfferChange(c.Id, c.ChangedAt, ReadFieldChanges(c.Changes)))
            .ToList();
        return (await WithNearbyAsync(connection, [offer with { Features = features, Changes = changes }], cancellationToken))[0];
    }

    // The newest offer first breaks ties, so a page always has the same offers.
    private static string BuildOrderBy(OfferSort sort, SortDirection direction)
    {
        var column = sort switch
        {
            OfferSort.FirstSeen => "o.external_created_at",
            OfferSort.Price => "o.price",
            OfferSort.PricePerM2 => PricePerM2,
            OfferSort.AreaM2 => "o.area_m2",
            OfferSort.Rooms => "o.rooms",
            OfferSort.Rent => "o.rent",
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
        };
        var order = direction == SortDirection.Ascending ? "ASC" : "DESC";
        return $"{column} {order} NULLS LAST, o.external_created_at DESC NULLS LAST, o.created_at DESC, o.id DESC";
    }

    private static List<FieldChange> ReadFieldChanges(string changes)
    {
        using var document = JsonDocument.Parse(changes);
        return document.RootElement.EnumerateObject()
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => new FieldChange(ConvertToCamelCase(f.Name), ReadChangedValue(f.Name, f.Value[0])!, ReadChangedValue(f.Name, f.Value[1])))
            .ToList();
    }

    private static string ConvertToCamelCase(string column)
    {
        var words = column.Split('_');
        return words[0] + string.Concat(words.Skip(1).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    // housing.offer_change keeps enum numbers, like the offer table; the card gets the enums, which the API writes as names.
    private static object? ReadChangedValue(string column, JsonElement value) => column switch
    {
        "property_type" => value.Deserialize<PropertyType?>(),
        "transaction" => value.Deserialize<Transaction?>(),
        "price_currency" or "rent_currency" or "deposit_currency" => value.Deserialize<Currency?>(),
        "building_type" => value.Deserialize<BuildingType?>(),
        "heating" => value.Deserialize<Heating?>(),
        "condition" => value.Deserialize<Condition?>(),
        "furnished" => value.Deserialize<Answer?>(),
        "features" => value.Deserialize<Feature[]>(),
        _ => value.ValueKind == JsonValueKind.Null ? null : value.Clone(),
    };

    private static async Task<IReadOnlyList<OfferListItem>> WithNearbyAsync(
        NpgsqlConnection connection, IReadOnlyList<OfferListItem> offers, CancellationToken cancellationToken)
    {
        var nearby = (await connection.QueryAsync<(int OfferId, int Tier, string Name, decimal Km)>(new CommandDefinition(
                NearbySql, new { OfferIds = offers.Select(o => o.Id).ToArray() }, cancellationToken: cancellationToken)))
            .ToLookup(n => n.OfferId, n => new NearbyPlace((NearbyTier)n.Tier, n.Name, n.Km));
        return offers.Select(o => o with { Nearby = nearby[o.Id].ToList() }).ToList();
    }

    public async Task<IReadOnlyList<PendingDetails>> ListPendingDetailsAsync(
        Source source, int limit, int maxFailedAttempts, DateTimeOffset runStartedAt, DateTimeOffset retryGivenUpBefore,
        CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(int OfferId, string ExternalId, string Url, string ContentHash)>(new CommandDefinition(
            PendingDetailsSql,
            new { SourceId = (int)source, Limit = limit, MaxFailedAttempts = maxFailedAttempts, RunStartedAt = runStartedAt, RetryGivenUpBefore = retryGivenUpBefore },
            cancellationToken: cancellationToken));
        return rows.Select(r => new PendingDetails(r.OfferId, r.ExternalId, new Uri(r.Url), r.ContentHash)).ToList();
    }

    public async Task<bool> SaveDetailsAsync(
        RawCapture capture, string externalId, OfferDetails details, bool recordChanges, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string[] externalIds = [externalId];
        var before = recordChanges ? await ReadContentAsync(connection, transaction, capture.SourceId, externalIds, cancellationToken) : [];
        var offerId = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(SaveDetailsSql, new
        {
            SourceId = (int)capture.SourceId,
            ExternalId = externalId,
            Rent = details.Rent?.Amount,
            RentCurrency = (int?)details.Rent?.Currency,
            Deposit = details.Deposit?.Amount,
            DepositCurrency = (int?)details.Deposit?.Currency,
            details.AvailableFrom,
            details.Floor,
            details.BuildingFloors,
            details.BuildYear,
            BuildingType = (int?)details.BuildingType,
            Heating = (int?)details.Heating,
            Condition = (int?)details.Condition,
            details.Latitude,
            details.Longitude,
            details.LocationRadiusM,
            details.Description,
            Furnished = (int?)details.Furnished,
            RejectedValues = details.RejectedValues is { } rejected ? JsonSerializer.Serialize(rejected) : null,
        }, transaction, cancellationToken: cancellationToken));
        if (offerId is null)
        {
            return false;
        }

        if (details.Features is { } features)
        {
            await connection.ExecuteAsync(new CommandDefinition(ReplaceFeaturesSql, new
            {
                OfferId = offerId,
                Features = features.Distinct().Select(f => (int)f).ToArray(),
            }, transaction, cancellationToken: cancellationToken));
        }

        if (recordChanges)
        {
            await RecordChangesAsync(connection, transaction, capture, externalIds, before, isDetailPage: true, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task MarkDetailsFetchedAsync(PendingDetails offer, DateTimeOffset at, CancellationToken cancellationToken) =>
        ExecuteAsync(DetailsFetchedSql, new { offer.OfferId, offer.ContentHash, At = at }, cancellationToken);

    public Task MarkDetailsFailedAsync(PendingDetails offer, DateTimeOffset at, string lastError, CancellationToken cancellationToken) =>
        ExecuteAsync(DetailsFailedSql, new { offer.OfferId, offer.ContentHash, At = at, LastError = lastError }, cancellationToken);

    public Task MarkRemovedAsync(PendingDetails offer, CancellationToken cancellationToken) =>
        ExecuteAsync(MarkRemovedSql, new { offer.OfferId }, cancellationToken);

    private async Task ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<OfferLocation>> ListLocationsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(int Source, string LocationKey, int OfferCount)>(
            new CommandDefinition(LocationsSql, cancellationToken: cancellationToken));
        return rows.Select(r => new OfferLocation((Source)r.Source, r.LocationKey, r.OfferCount)).ToList();
    }

    public async Task<int> SetPlacesAsync(IReadOnlyList<PlaceLink> links, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(SetPlacesSql, new
        {
            SourceIds = links.Select(l => (int)l.Source).ToArray(),
            LocationKeys = links.Select(l => l.LocationKey).ToArray(),
            PlaceIds = links.Select(l => l.PlaceId).ToArray(),
        }, cancellationToken: cancellationToken));
    }
}
