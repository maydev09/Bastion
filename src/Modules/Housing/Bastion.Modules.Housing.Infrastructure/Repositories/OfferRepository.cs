using System.Text.Json;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ListOffers;
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
        SET location_key = l.location_key, place_id = l.place_id
        FROM unnest(@ExternalIds, @LocationKeys, @PlaceIds) AS l (external_id, location_key, place_id)
        WHERE o.source_id = @SourceId AND o.external_id = l.external_id
          AND (o.location_key, o.place_id) IS DISTINCT FROM (l.location_key, l.place_id)
        """;

    // The right side of SET reads the values from before the update.
    private const string MarkMissedSql = """
        UPDATE housing.offer
        SET missed_scans = missed_scans + 1,
            removed_at = CASE WHEN missed_scans + 1 >= @MissedScansToRemove THEN last_seen_at END
        WHERE source_id = @SourceId AND removed_at IS NULL AND last_seen_at < @ScanStartedAt
        RETURNING removed_at IS NOT NULL AS removed
        """;

    private const string OfferColumns = """
        o.id, o.source_id AS source, o.title, o.url, o.property_type, o.transaction, o.price, o.price_currency,
        o.area_m2, o.rooms, o.created_at, o.updated_at, o.external_created_at, o.external_updated_at,
        p.name AS place_name, p.kind AS place_kind, p.role AS place_role, p.voivodeship, p.county, p.municipality,
        p.population, p.population_year, p.population_source
        """;

    // The page is cut first and only its offers are joined with their places.
    // The ORDER BY list from BuildOrderBy picks the page and orders its rows.
    private static string BuildListSql(string orderBy) => $$"""
        SELECT {{OfferColumns}}
        FROM (
            SELECT *
            FROM housing.offer o
            WHERE removed_at IS NULL
            ORDER BY {{orderBy}}
            LIMIT @PageSize OFFSET @Offset
        ) o
        LEFT JOIN housing.place p ON p.id = o.place_id
        ORDER BY {{orderBy}};

        SELECT count(*)::int FROM housing.offer WHERE removed_at IS NULL;
        """;

    // A removed offer is still shown, e.g. from an old link.
    private const string GetSql = $"""
        SELECT {OfferColumns},
            o.rent, o.rent_currency, o.deposit, o.deposit_currency, o.available_from, o.floor, o.building_floors,
            o.build_year, o.building_type, o.heating, o.condition, o.latitude, o.longitude, o.location_radius_m,
            o.description, f.fetched_at AS details_fetched_at
        FROM housing.offer o
        LEFT JOIN housing.place p ON p.id = o.place_id
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
    private const string PendingDetailsSql = """
        SELECT o.id AS offer_id, o.external_id, o.url, o.content_hash
        FROM housing.offer o
        LEFT JOIN housing.offer_detail_fetch f ON f.offer_id = o.id
        WHERE o.source_id = @SourceId AND o.removed_at IS NULL AND o.content_hash IS NOT NULL
          AND (f.offer_id IS NULL
               OR f.content_hash <> o.content_hash
               OR (f.failed_attempts BETWEEN 1 AND @MaxFailedAttempts - 1 AND f.attempted_at < @RunStartedAt))
        ORDER BY o.external_created_at DESC NULLS LAST, o.id DESC
        LIMIT @Limit
        """;

    // A null never clears a stored value: another page may not show a field this one showed.
    private const string SaveDetailsSql = """
        UPDATE housing.offer
        SET rent = COALESCE(@Rent, rent),
            rent_currency = COALESCE(@RentCurrency, rent_currency),
            deposit = COALESCE(@Deposit, deposit),
            deposit_currency = COALESCE(@DepositCurrency, deposit_currency),
            available_from = COALESCE(@AvailableFrom, available_from),
            floor = COALESCE(@Floor, floor),
            building_floors = COALESCE(@BuildingFloors, building_floors),
            build_year = COALESCE(@BuildYear, build_year),
            building_type = COALESCE(@BuildingType, building_type),
            heating = COALESCE(@Heating, heating),
            condition = COALESCE(@Condition, condition),
            latitude = COALESCE(@Latitude, latitude),
            longitude = COALESCE(@Longitude, longitude),
            location_radius_m = COALESCE(@LocationRadiusM, location_radius_m),
            description = COALESCE(@Description, description),
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
            o.description,
            CASE WHEN f.fetched_at IS NOT NULL
                THEN ARRAY(SELECT feature FROM housing.offer_feature WHERE offer_id = o.id ORDER BY feature)
            END AS features
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

    private const string SetPlacesSql = """
        UPDATE housing.offer o
        SET place_id = l.place_id
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
        }, transaction, cancellationToken: cancellationToken));

        if (recordChanges && updated > 0)
        {
            await RecordChangesAsync(connection, transaction, capture, externalIds, before, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var after = await ReadContentAsync(connection, transaction, capture.SourceId, externalIds, cancellationToken);
        var changes = after
            .Where(a => before.ContainsKey(a.Key))
            .Select(a => (OfferId: a.Key, Changes: BuildChangeMap(before[a.Key], a.Value)))
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

    // A field that had no value gets its first one, which is not a change.
    private static Dictionary<string, object?[]> BuildChangeMap(IDictionary<string, object?> before, IDictionary<string, object?> after) =>
        after
            .Where(f => f.Key != "id" && before[f.Key] is { } old && !AreEqual(old, f.Value))
            .ToDictionary(f => f.Key, f => new[] { before[f.Key], f.Value });

    private static bool AreEqual(object old, object? value) =>
        old is int[] oldFeatures && value is int[] features ? oldFeatures.SequenceEqual(features) : old.Equals(value);

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
        PageRequest page, OfferSort sort, SortDirection direction, CancellationToken cancellationToken)
    {
        var sql = BuildListSql(BuildOrderBy(sort, direction));
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            sql, new { page.PageSize, page.Offset }, cancellationToken: cancellationToken));

        var items = (await results.ReadAsync<OfferListItem>()).ToList();
        var total = await results.ReadSingleAsync<int>();
        return new PagedResult<OfferListItem>(await WithNearbyAsync(connection, items, cancellationToken), page.Page, page.PageSize, total);
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
            OfferSort.PricePerM2 => "o.price / NULLIF(o.area_m2, 0)",
            OfferSort.AreaM2 => "o.area_m2",
            OfferSort.Rooms => "o.rooms",
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
        Source source, int limit, int maxFailedAttempts, DateTimeOffset runStartedAt, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(int OfferId, string ExternalId, string Url, string ContentHash)>(new CommandDefinition(
            PendingDetailsSql,
            new { SourceId = (int)source, Limit = limit, MaxFailedAttempts = maxFailedAttempts, RunStartedAt = runStartedAt },
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
            await RecordChangesAsync(connection, transaction, capture, externalIds, before, cancellationToken);
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
