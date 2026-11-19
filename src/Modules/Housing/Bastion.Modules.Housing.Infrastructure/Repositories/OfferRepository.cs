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
             external_created_at, external_updated_at, listing_hash, raw_capture_id, created_at, updated_at)
        VALUES
            (@SourceId, @ExternalId, @Title, @Url, @PropertyType, @Transaction, @Price, @PriceCurrency, @AreaM2, @Rooms,
             @ExternalCreatedAt, @ExternalUpdatedAt, @ListingHash, @RawCaptureId, @SeenAt, @SeenAt)
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

    private const string ListSql = """
        SELECT id, source_id AS source, title, url, property_type, transaction, price, price_currency, area_m2, rooms,
               created_at, updated_at, external_created_at, external_updated_at
        FROM housing.offer
        ORDER BY external_created_at DESC NULLS LAST, created_at DESC, id DESC
        LIMIT @PageSize OFFSET @Offset;

        SELECT count(*)::int FROM housing.offer;
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

        await transaction.CommitAsync(cancellationToken);
        return new SaveCounts(added, updated, offers.Count - added - updated);
    }

    public async Task<PagedResult<OfferListItem>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            ListSql, new { page.PageSize, page.Offset }, cancellationToken: cancellationToken));

        var items = (await results.ReadAsync<OfferListItem>()).ToList();
        var total = await results.ReadSingleAsync<int>();
        return new PagedResult<OfferListItem>(items, page.Page, page.PageSize, total);
    }
}
