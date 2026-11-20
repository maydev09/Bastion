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
             external_created_at, external_updated_at, location_key, place_id, listing_hash, raw_capture_id,
             created_at, updated_at, last_seen_at)
        VALUES
            (@SourceId, @ExternalId, @Title, @Url, @PropertyType, @Transaction, @Price, @PriceCurrency, @AreaM2, @Rooms,
             @ExternalCreatedAt, @ExternalUpdatedAt, @LocationKey, @PlaceId, @ListingHash, @RawCaptureId,
             @SeenAt, @SeenAt, @SeenAt)
        ON CONFLICT (source_id, external_id) DO UPDATE SET
            title = excluded.title,
            url = excluded.url,
            property_type = excluded.property_type,
            transaction = excluded.transaction,
            price = excluded.price,
            price_currency = excluded.price_currency,
            area_m2 = excluded.area_m2,
            rooms = excluded.rooms,
            external_created_at = excluded.external_created_at,
            external_updated_at = excluded.external_updated_at,
            listing_hash = excluded.listing_hash,
            raw_capture_id = excluded.raw_capture_id,
            updated_at = excluded.updated_at
        WHERE o.listing_hash <> excluded.listing_hash
          AND o.updated_at <= excluded.updated_at
        RETURNING (xmax = 0) AS inserted
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
    private const string ListSql = $"""
        SELECT {OfferColumns}
        FROM (
            SELECT *
            FROM housing.offer
            WHERE removed_at IS NULL
            ORDER BY external_created_at DESC NULLS LAST, created_at DESC, id DESC
            LIMIT @PageSize OFFSET @Offset
        ) o
        LEFT JOIN housing.place p ON p.id = o.place_id
        ORDER BY o.external_created_at DESC NULLS LAST, o.created_at DESC, o.id DESC;

        SELECT count(*)::int FROM housing.offer WHERE removed_at IS NULL;
        """;

    // A removed offer is still shown, e.g. from an old link.
    private const string GetSql = $"""
        SELECT {OfferColumns}
        FROM housing.offer o
        LEFT JOIN housing.place p ON p.id = o.place_id
        WHERE o.id = @Id
        """;

    private const string NearbySql = """
        SELECT o.id AS offer_id, n.tier, c.name, n.km
        FROM housing.offer o
        JOIN housing.place_nearby n ON n.place_id = o.place_id
        JOIN housing.place c ON c.id = n.nearby_id
        WHERE o.id = ANY(@OfferIds)
        ORDER BY o.id, n.tier, n.rank
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

    public async Task<SaveCounts> SaveAsync(IReadOnlyList<Offer> offers, RawCapture capture, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

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
                offer.ExternalCreatedAt,
                offer.ExternalUpdatedAt,
                offer.LocationKey,
                offer.PlaceId,
                ListingHash = offer.ListingHash(),
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
            ExternalIds = offers.Select(o => o.ExternalId).ToArray(),
            SeenAt = capture.FetchedAt,
        }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(LocationSql, new
        {
            SourceId = (int)capture.SourceId,
            ExternalIds = offers.Select(o => o.ExternalId).ToArray(),
            LocationKeys = offers.Select(o => o.LocationKey).ToArray(),
            PlaceIds = offers.Select(o => o.PlaceId).ToArray(),
        }, transaction, cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return new SaveCounts(added, updated, offers.Count - added - updated);
    }

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

    public async Task<PagedResult<OfferListItem>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            ListSql, new { page.PageSize, page.Offset }, cancellationToken: cancellationToken));

        var items = (await results.ReadAsync<OfferListItem>()).ToList();
        var total = await results.ReadSingleAsync<int>();
        return new PagedResult<OfferListItem>(await WithNearbyAsync(connection, items, cancellationToken), page.Page, page.PageSize, total);
    }

    public async Task<OfferListItem?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var offer = await connection.QuerySingleOrDefaultAsync<OfferListItem>(new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));
        return offer is null ? null : (await WithNearbyAsync(connection, [offer], cancellationToken))[0];
    }

    private static async Task<IReadOnlyList<OfferListItem>> WithNearbyAsync(
        NpgsqlConnection connection, IReadOnlyList<OfferListItem> offers, CancellationToken cancellationToken)
    {
        var nearby = (await connection.QueryAsync<(int OfferId, int Tier, string Name, decimal Km)>(new CommandDefinition(
                NearbySql, new { OfferIds = offers.Select(o => o.Id).ToArray() }, cancellationToken: cancellationToken)))
            .ToLookup(n => n.OfferId, n => new NearbyPlace((NearbyTier)n.Tier, n.Name, n.Km));
        return offers.Select(o => o with { Nearby = nearby[o.Id].ToList() }).ToList();
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
