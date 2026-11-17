using Bastion.Modules.FlatOffers.Application.Abstractions;
using Bastion.Modules.FlatOffers.Application.ListOffers;
using Bastion.Modules.FlatOffers.Domain;
using Bastion.Platform.Abstractions;
using Dapper;
using Npgsql;

namespace Bastion.Modules.FlatOffers.Infrastructure.Storage;

// The data source is optional so the app still starts without a database (Phase 2 rule).
internal sealed class OfferStore(NpgsqlDataSource? dataSource = null) : IOfferStore
{
    // Updates only when the listing hash changed. Returns no row for an unchanged offer,
    // otherwise whether the row was inserted (xmax = 0) or updated.
    private const string SaveSql = """
        INSERT INTO flat_offers.offer AS o
            (source, external_id, title, url, price, price_currency, area_m2, rooms, listing_json, listing_hash, first_seen_at, updated_at)
        VALUES
            (@Source, @ExternalId, @Title, @Url, @Price, @PriceCurrency, @AreaM2, @Rooms, CAST(@ListingJson AS jsonb), @ListingHash, @Now, @Now)
        ON CONFLICT (source, external_id) DO UPDATE SET
            title = excluded.title,
            url = excluded.url,
            price = excluded.price,
            price_currency = excluded.price_currency,
            area_m2 = excluded.area_m2,
            rooms = excluded.rooms,
            listing_json = excluded.listing_json,
            listing_hash = excluded.listing_hash,
            updated_at = excluded.updated_at
        WHERE o.listing_hash <> excluded.listing_hash
        RETURNING (xmax = 0) AS inserted
        """;

    private const string ListSql = """
        SELECT id, source, title, url, price, price_currency, area_m2, rooms, first_seen_at, updated_at
        FROM flat_offers.offer
        ORDER BY first_seen_at DESC, id DESC
        LIMIT @PageSize OFFSET @Offset;

        SELECT count(*)::int FROM flat_offers.offer;
        """;

    private NpgsqlDataSource Db => dataSource
        ?? throw new InvalidOperationException("The database connection string is not configured.");

    public async Task<SaveCounts> SaveAsync(OfferSource source, IReadOnlyList<ListingOffer> offers, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await Db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        int added = 0, updated = 0;
        foreach (var offer in offers)
        {
            var inserted = await connection.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(SaveSql, new
            {
                Source = source.ToString(),
                offer.ExternalId,
                offer.Title,
                Url = offer.Url.ToString(),
                offer.Price,
                offer.PriceCurrency,
                offer.AreaM2,
                offer.Rooms,
                offer.ListingJson,
                ListingHash = offer.ListingHash(),
                Now = now,
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
        await using var connection = await Db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            ListSql, new { page.PageSize, page.Offset }, cancellationToken: cancellationToken));

        var items = (await results.ReadAsync<OfferListItem>()).ToList();
        var total = await results.ReadSingleAsync<int>();
        return new PagedResult<OfferListItem>(items, page.Page, page.PageSize, total);
    }
}
