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

    private const string ReplaceRatesSql = """
        DELETE FROM housing.rent_rate;
        INSERT INTO housing.rent_rate (basis, voivodeship, place_id, district_id, building_type, median_per_m2, sample_size, calculated_at)
        SELECT * FROM unnest(@Bases, @Voivodeships, @PlaceIds, @DistrictIds, @BuildingTypes, @Medians, @SampleSizes, @CalculatedAts);
        """;

    // Every calculation rewrites the rows, so calculated_at says when the cost was last checked. An estimate belongs to its
    // cost: the costs are written first.
    private const string SaveSql = """
        DELETE FROM housing.monthly_cost WHERE offer_id = ANY(@OfferIds) AND NOT offer_id = ANY(@CostOfferIds);
        INSERT INTO housing.monthly_cost AS c (offer_id, rent, rent_source, total, currency, rent_quote, calculated_at)
        SELECT * FROM unnest(@CostOfferIds, @Rents, @RentSources, @Totals, @Currencies, @RentQuotes, @CalculatedAts)
        ON CONFLICT (offer_id) DO UPDATE SET
            rent = excluded.rent,
            rent_source = excluded.rent_source,
            total = excluded.total,
            currency = excluded.currency,
            rent_quote = excluded.rent_quote,
            calculated_at = excluded.calculated_at;

        DELETE FROM housing.rent_estimate WHERE offer_id = ANY(@OfferIds) AND NOT offer_id = ANY(@EstimateOfferIds);
        INSERT INTO housing.rent_estimate AS e
            (offer_id, rate_per_m2, basis, basis_per_m2, sample_size, neighbors, neighbors_per_m2, neighbors_km)
        SELECT * FROM unnest(@EstimateOfferIds, @RatesPerM2, @Bases, @BasisRates, @SampleSizes, @Neighbors, @NeighborRates, @NeighborKms)
        ON CONFLICT (offer_id) DO UPDATE SET
            rate_per_m2 = excluded.rate_per_m2,
            basis = excluded.basis,
            basis_per_m2 = excluded.basis_per_m2,
            sample_size = excluded.sample_size,
            neighbors = excluded.neighbors,
            neighbors_per_m2 = excluded.neighbors_per_m2,
            neighbors_km = excluded.neighbors_km;
        """;

    public async Task<IReadOnlyList<CostInput>> ListInputsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<CostInput>(new CommandDefinition(InputsSql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<CostInput>> ListInputsAsync(
        Source source, IReadOnlyList<string> externalIds, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var sql = $"{InputsSql} WHERE o.source_id = @SourceId AND o.external_id = ANY(@ExternalIds)";
        var parameters = new { SourceId = (int)source, ExternalIds = externalIds.ToArray() };
        return (await connection.QueryAsync<CostInput>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<CostInput>> ListRateSamplesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<CostInput>(new CommandDefinition(RateSamplesSql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<RentRate>> ListRatesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT basis, voivodeship, place_id, district_id, building_type, median_per_m2, sample_size, calculated_at
            FROM housing.rent_rate
            """;
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<RateRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToRate()).ToList();
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
            SampleSizes = rates.Select(r => r.SampleSize).ToArray(),
            CalculatedAts = rates.Select(r => r.CalculatedAt).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveAsync(
        IReadOnlyList<int> offerIds, IReadOnlyList<MonthlyCost> costs, IReadOnlyList<RentEstimate> estimates, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(SaveSql, new
        {
            OfferIds = offerIds.ToArray(),
            CostOfferIds = costs.Select(c => c.OfferId).ToArray(),
            Rents = costs.Select(c => c.Rent).ToArray(),
            RentSources = costs.Select(c => (int)c.RentSource).ToArray(),
            Totals = costs.Select(c => c.Total).ToArray(),
            Currencies = costs.Select(c => (int)c.Currency).ToArray(),
            RentQuotes = costs.Select(c => c.RentQuote).ToArray(),
            CalculatedAts = costs.Select(c => c.CalculatedAt).ToArray(),
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

    // Dapper fills properties, not the parameters of a record with enums, so rows are read as they are and converted here.
    private sealed record RateRow
    {
        public RentBasis Basis { get; init; }

        public Voivodeship? Voivodeship { get; init; }

        public int? PlaceId { get; init; }

        public int? DistrictId { get; init; }

        public BuildingType? BuildingType { get; init; }

        public decimal MedianPerM2 { get; init; }

        public int SampleSize { get; init; }

        public DateTimeOffset CalculatedAt { get; init; }

        public RentRate ToRate() => new(Basis, Voivodeship, PlaceId, DistrictId, BuildingType, MedianPerM2, SampleSize, CalculatedAt);
    }
}
