using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;
using Dapper;
using Npgsql;

namespace Bastion.Modules.Housing.Infrastructure.Repositories;

public sealed class StatisticsExclusionRepository(NpgsqlDataSource db) : IStatisticsExclusionRepository
{
    private const string InputsSql = """
        SELECT o.id AS offer_id, o.title, o.description, o.price, o.price_currency, o.area_m2, o.rooms, o.place_id,
            p.voivodeship, p.kind AS place_kind, p.population
        FROM housing.offer o
        LEFT JOIN housing.place p ON p.id = o.place_id
        """;

    // A group keeps its row, so the exclusions that point to it keep their fence; a group that lost its fence is deleted
    // (its exclusions keep no fence until they are found again).
    private const string ReplaceFencesSql = """
        DELETE FROM housing.price_fence f
        WHERE NOT EXISTS (
            SELECT 1
            FROM unnest(@Bases, @Rooms, @PlaceIds, @Voivodeships, @PlaceSizes) AS u (basis, rooms, place_id, voivodeship, place_size)
            WHERE u.basis = f.basis AND u.rooms IS NOT DISTINCT FROM f.rooms AND u.place_id IS NOT DISTINCT FROM f.place_id
              AND u.voivodeship IS NOT DISTINCT FROM f.voivodeship AND u.place_size IS NOT DISTINCT FROM f.place_size);
        INSERT INTO housing.price_fence
            (basis, rooms, place_id, voivodeship, place_size, low_per_m2, high_per_m2, currency, sample_size, calculated_at)
        SELECT * FROM unnest(@Bases, @Rooms, @PlaceIds, @Voivodeships, @PlaceSizes, @Lows, @Highs, @Currencies, @SampleSizes, @CalculatedAts)
        ON CONFLICT (basis, rooms, place_id, voivodeship, place_size) DO UPDATE SET
            low_per_m2 = excluded.low_per_m2,
            high_per_m2 = excluded.high_per_m2,
            currency = excluded.currency,
            sample_size = excluded.sample_size,
            calculated_at = excluded.calculated_at;
        """;

    // Only rows whose values change are written (calculated_at: when the exclusion got its current values).
    private const string SaveSql = """
        DELETE FROM housing.statistics_exclusion WHERE offer_id = ANY(@OfferIds) AND NOT offer_id = ANY(@ExcludedOfferIds);
        INSERT INTO housing.statistics_exclusion AS e (offer_id, reason, price_fence_id, calculated_at)
        SELECT u.offer_id, u.reason, u.price_fence_id, @CalculatedAt
        FROM unnest(@ExcludedOfferIds, @Reasons, @PriceFenceIds) AS u (offer_id, reason, price_fence_id)
        -- An offer deleted meanwhile gets no row.
        WHERE EXISTS (SELECT 1 FROM housing.offer o WHERE o.id = u.offer_id)
        ON CONFLICT (offer_id) DO UPDATE SET
            reason = excluded.reason,
            price_fence_id = excluded.price_fence_id,
            calculated_at = excluded.calculated_at
        WHERE (e.reason, e.price_fence_id) IS DISTINCT FROM (excluded.reason, excluded.price_fence_id);
        """;

    public async Task<IReadOnlyList<ExclusionInput>> ListInputsAsync(CancellationToken cancellationToken)
    {
        const string sql = $"{InputsSql} WHERE o.removed_at IS NULL";
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ExclusionInput>(new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<PriceFence>> ListFencesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, basis, rooms, place_id, voivodeship, place_size, low_per_m2, high_per_m2, currency, sample_size, calculated_at
            FROM housing.price_fence
            """;
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<FenceRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToFence()).ToList();
    }

    public async Task ReplaceFencesAsync(IReadOnlyList<PriceFence> fences, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(ReplaceFencesSql, new
        {
            Bases = fences.Select(f => (int)f.Basis).ToArray(),
            Rooms = fences.Select(f => f.Rooms).ToArray(),
            PlaceIds = fences.Select(f => f.PlaceId).ToArray(),
            Voivodeships = fences.Select(f => (int?)f.Voivodeship).ToArray(),
            PlaceSizes = fences.Select(f => (int?)f.PlaceSize).ToArray(),
            Lows = fences.Select(f => f.LowPerM2).ToArray(),
            Highs = fences.Select(f => f.HighPerM2).ToArray(),
            Currencies = fences.Select(f => (int)f.Currency).ToArray(),
            SampleSizes = fences.Select(f => f.SampleSize).ToArray(),
            CalculatedAts = fences.Select(f => f.CalculatedAt).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveAsync(
        IReadOnlyList<int> offerIds, DateTimeOffset calculatedAt, IReadOnlyList<StatisticsExclusion> exclusions, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(SaveSql, new
        {
            OfferIds = offerIds.ToArray(),
            CalculatedAt = calculatedAt,
            ExcludedOfferIds = exclusions.Select(e => e.OfferId).ToArray(),
            Reasons = exclusions.Select(e => (int)e.Reason).ToArray(),
            PriceFenceIds = exclusions.Select(e => e.PriceFenceId).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    // Dapper fills properties, not the parameters of a record with enums, so rows are read as they are and converted here.
    private sealed record FenceRow
    {
        public int Id { get; init; }

        public PriceFenceBasis Basis { get; init; }

        public int? Rooms { get; init; }

        public int? PlaceId { get; init; }

        public Voivodeship? Voivodeship { get; init; }

        public PlaceSize? PlaceSize { get; init; }

        public decimal LowPerM2 { get; init; }

        public decimal HighPerM2 { get; init; }

        public Currency Currency { get; init; }

        public int SampleSize { get; init; }

        public DateTimeOffset CalculatedAt { get; init; }

        public PriceFence ToFence() =>
            new(Id, Basis, Rooms, PlaceId, Voivodeship, PlaceSize, LowPerM2, HighPerM2, Currency, SampleSize, CalculatedAt);
    }
}
