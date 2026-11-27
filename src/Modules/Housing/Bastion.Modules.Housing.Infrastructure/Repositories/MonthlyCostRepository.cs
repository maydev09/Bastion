using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;
using Dapper;
using Npgsql;

namespace Bastion.Modules.Housing.Infrastructure.Repositories;

public sealed class MonthlyCostRepository(NpgsqlDataSource db) : IMonthlyCostRepository
{
    // An offer in a neighborhood counts in the neighborhood's district too.
    private const string Columns = """
        o.id AS offer_id, o.price, o.price_currency, o.area_m2, o.rent, o.rent_currency, o.building_type, o.place_id,
        coalesce(d.parent_id, d.id) AS district_id, CASE WHEN d.parent_id IS NOT NULL THEN d.id END AS neighborhood_id,
        p.voivodeship, o.latitude, o.longitude
        """;

    private const string From = """
        FROM housing.offer o
        LEFT JOIN housing.place p ON p.id = o.place_id
        LEFT JOIN housing.district d ON d.id = o.district_id
        """;

    private const string InputsSql = $"SELECT {Columns}, o.title, o.description {From}";

    private const string RateSamplesSql = $"SELECT {Columns} {From} WHERE o.removed_at IS NULL AND o.rent IS NOT NULL AND o.area_m2 > 0";

    // Every collecting run calculates the rates again: a group keeps its row, which is written only when its rate changes,
    // and a group without a rate now loses its row.
    private const string ReplaceRatesSql = """
        DELETE FROM housing.rent_rate r
        WHERE NOT EXISTS (
            SELECT 1
            FROM unnest(@Bases, @Voivodeships, @PlaceIds, @DistrictIds, @BuildingTypes)
                AS u (basis, voivodeship, place_id, district_id, building_type)
            WHERE u.basis = r.basis AND u.voivodeship IS NOT DISTINCT FROM r.voivodeship AND u.place_id IS NOT DISTINCT FROM r.place_id
              AND u.district_id IS NOT DISTINCT FROM r.district_id AND u.building_type IS NOT DISTINCT FROM r.building_type);
        INSERT INTO housing.rent_rate AS r
            (basis, voivodeship, place_id, district_id, building_type, median_per_m2, currency, sample_size, calculated_at)
        SELECT * FROM unnest(@Bases, @Voivodeships, @PlaceIds, @DistrictIds, @BuildingTypes, @Medians, @Currencies, @SampleSizes, @CalculatedAts)
        ON CONFLICT (basis, voivodeship, place_id, district_id, building_type) DO UPDATE SET
            median_per_m2 = excluded.median_per_m2,
            currency = excluded.currency,
            sample_size = excluded.sample_size,
            calculated_at = excluded.calculated_at
        WHERE (r.median_per_m2, r.currency, r.sample_size) IS DISTINCT FROM (excluded.median_per_m2, excluded.currency, excluded.sample_size);
        """;

    // Only rows whose values change are written, so a recalculation at the end of every collecting run leaves the rest as
    // they are (and keeps their calculated_at: when the cost got its current value).
    private const string SaveSql = """
        DELETE FROM housing.monthly_cost WHERE offer_id = ANY(@OfferIds) AND NOT offer_id = ANY(@CostOfferIds);
        INSERT INTO housing.monthly_cost AS c (offer_id, rent, rent_source, total, currency, rent_quote, calculated_at)
        SELECT u.offer_id, u.rent, u.rent_source, u.total, u.currency, u.rent_quote, @CalculatedAt
        FROM unnest(@CostOfferIds, @Rents, @RentSources, @Totals, @Currencies, @RentQuotes)
            AS u (offer_id, rent, rent_source, total, currency, rent_quote)
        -- An offer deleted meanwhile gets no row.
        WHERE EXISTS (SELECT 1 FROM housing.offer o WHERE o.id = u.offer_id)
        ON CONFLICT (offer_id) DO UPDATE SET
            rent = excluded.rent,
            rent_source = excluded.rent_source,
            total = excluded.total,
            currency = excluded.currency,
            rent_quote = excluded.rent_quote,
            calculated_at = excluded.calculated_at
        WHERE (c.rent, c.rent_source, c.total, c.currency, c.rent_quote)
            IS DISTINCT FROM (excluded.rent, excluded.rent_source, excluded.total, excluded.currency, excluded.rent_quote);

        DELETE FROM housing.rent_estimate WHERE offer_id = ANY(@OfferIds) AND NOT offer_id = ANY(@EstimateOfferIds);
        INSERT INTO housing.rent_estimate AS e
            (offer_id, rate_per_m2, basis, basis_per_m2, sample_size, neighbors, neighbors_per_m2, neighbors_km)
        SELECT u.*
        FROM unnest(@EstimateOfferIds, @RatesPerM2, @Bases, @BasisRates, @SampleSizes, @Neighbors, @NeighborRates, @NeighborKms)
            AS u (offer_id, rate_per_m2, basis, basis_per_m2, sample_size, neighbors, neighbors_per_m2, neighbors_km)
        WHERE EXISTS (SELECT 1 FROM housing.monthly_cost c WHERE c.offer_id = u.offer_id)
        ON CONFLICT (offer_id) DO UPDATE SET
            rate_per_m2 = excluded.rate_per_m2,
            basis = excluded.basis,
            basis_per_m2 = excluded.basis_per_m2,
            sample_size = excluded.sample_size,
            neighbors = excluded.neighbors,
            neighbors_per_m2 = excluded.neighbors_per_m2,
            neighbors_km = excluded.neighbors_km
        WHERE (e.rate_per_m2, e.basis, e.basis_per_m2, e.sample_size, e.neighbors, e.neighbors_per_m2, e.neighbors_km)
            IS DISTINCT FROM (excluded.rate_per_m2, excluded.basis, excluded.basis_per_m2, excluded.sample_size,
                excluded.neighbors, excluded.neighbors_per_m2, excluded.neighbors_km);
        """;

    public async Task<IReadOnlyList<CostInput>> ListInputsAsync(CancellationToken cancellationToken)
    {
        const string sql = $"""
            {InputsSql}
            WHERE o.removed_at IS NULL OR NOT EXISTS (SELECT 1 FROM housing.monthly_cost c WHERE c.offer_id = o.id)
            """;
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<CostInput>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<CostInput>> ListRateSamplesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<CostInput>(new CommandDefinition(RateSamplesSql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task ReplaceRatesAsync(IReadOnlyList<RentRate> rates, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(ReplaceRatesSql, new
        {
            Bases = rates.Select(r => (int)r.Basis).ToArray(),
            Voivodeships = rates.Select(r => (int?)r.Voivodeship).ToArray(),
            PlaceIds = rates.Select(r => r.PlaceId).ToArray(),
            DistrictIds = rates.Select(r => r.DistrictId).ToArray(),
            BuildingTypes = rates.Select(r => (int?)r.BuildingType).ToArray(),
            Medians = rates.Select(r => r.MedianPerM2).ToArray(),
            Currencies = rates.Select(r => (int)r.Currency).ToArray(),
            SampleSizes = rates.Select(r => r.SampleSize).ToArray(),
            CalculatedAts = rates.Select(r => r.CalculatedAt).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveAsync(
        IReadOnlyList<int> offerIds,
        DateTimeOffset calculatedAt,
        IReadOnlyList<MonthlyCost> costs,
        IReadOnlyList<RentEstimate> estimates,
        CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(SaveSql, new
        {
            OfferIds = offerIds.ToArray(),
            CalculatedAt = calculatedAt,
            CostOfferIds = costs.Select(c => c.OfferId).ToArray(),
            Rents = costs.Select(c => c.Rent).ToArray(),
            RentSources = costs.Select(c => (int)c.RentSource).ToArray(),
            Totals = costs.Select(c => c.Total).ToArray(),
            Currencies = costs.Select(c => (int)c.Currency).ToArray(),
            RentQuotes = costs.Select(c => c.RentQuote).ToArray(),
            EstimateOfferIds = estimates.Select(e => e.OfferId).ToArray(),
            RatesPerM2 = estimates.Select(e => e.RatePerM2).ToArray(),
            Bases = estimates.Select(e => (int?)e.Basis).ToArray(),
            BasisRates = estimates.Select(e => e.BasisPerM2).ToArray(),
            SampleSizes = estimates.Select(e => e.SampleSize).ToArray(),
            Neighbors = estimates.Select(e => e.Neighbors).ToArray(),
            NeighborRates = estimates.Select(e => e.NeighborsPerM2).ToArray(),
            NeighborKms = estimates.Select(e => e.NeighborsKm).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }
}
