using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ListPlaces;
using Bastion.Modules.Housing.Domain;
using Dapper;
using Npgsql;

namespace Bastion.Modules.Housing.Infrastructure.Repositories;

public sealed class PlaceRepository(NpgsqlDataSource db) : IPlaceRepository
{
    private const string Columns = """
        id, simc, name, voivodeship, county, municipality, kind, role, population, population_year, population_source,
        latitude, longitude
        """;

    private const string ListSql = $"SELECT {Columns} FROM housing.place";

    private const string ListByNameSql = $"SELECT {Columns} FROM housing.place WHERE voivodeship = @Voivodeship AND name_key = @NameKey";

    // One statement for all places (about 65 000); returns no row for an unchanged place.
    private const string SaveSql = """
        INSERT INTO housing.place AS p
            (simc, name, name_key, voivodeship, county, municipality, kind, role, population, population_year, population_source,
             latitude, longitude)
        SELECT * FROM unnest(@Simcs, @Names, @NameKeys, @Voivodeships, @Counties, @Municipalities, @Kinds, @Roles, @Populations,
                             @PopulationYears, @PopulationSources, @Latitudes, @Longitudes)
        ON CONFLICT (simc) DO UPDATE SET
            name = excluded.name,
            name_key = excluded.name_key,
            voivodeship = excluded.voivodeship,
            county = excluded.county,
            municipality = excluded.municipality,
            kind = excluded.kind,
            role = excluded.role,
            population = excluded.population,
            population_year = excluded.population_year,
            population_source = excluded.population_source,
            latitude = excluded.latitude,
            longitude = excluded.longitude
        WHERE (p.name, p.voivodeship, p.county, p.municipality, p.kind, p.role, p.population, p.population_year,
               p.population_source, p.latitude, p.longitude)
              IS DISTINCT FROM
              (excluded.name, excluded.voivodeship, excluded.county, excluded.municipality, excluded.kind, excluded.role,
               excluded.population, excluded.population_year, excluded.population_source, excluded.latitude,
               excluded.longitude)
        RETURNING (xmax = 0) AS inserted
        """;

    // A name key holds only a-z, 0-9 and '-', so the prefix needs no LIKE escaping.
    // Ties go to the bigger place, then to the name, so the order is stable.
    private const string SearchWithOffersSql = """
        SELECT p.id, p.name, p.voivodeship, p.county, p.municipality, p.kind, o.offer_count
        FROM (
            SELECT place_id, count(*)::int AS offer_count
            FROM housing.offer
            WHERE removed_at IS NULL AND place_id IS NOT NULL
            GROUP BY place_id
        ) o
        JOIN housing.place p ON p.id = o.place_id
        WHERE p.name_key LIKE @Prefix || '%' OR p.name_key LIKE '%-' || @Prefix || '%'
        ORDER BY o.offer_count DESC, p.population DESC NULLS LAST, p.name, p.id
        LIMIT @Limit
        """;

    private const string GetListItemSql = """
        SELECT p.id, p.name, p.voivodeship, p.county, p.municipality, p.kind,
            (SELECT count(*)::int FROM housing.offer o WHERE o.place_id = p.id AND o.removed_at IS NULL) AS offer_count
        FROM housing.place p
        WHERE p.id = @Id
        """;

    private const string ReplaceNearbySql = """
        DELETE FROM housing.place_nearby;
        INSERT INTO housing.place_nearby (place_id, tier, rank, nearby_id, km)
        SELECT * FROM unnest(@PlaceIds, @Tiers, @Ranks, @NearbyIds, @Kms);
        """;

    public async Task<IReadOnlyList<Place>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PlaceRow>(new CommandDefinition(ListSql, cancellationToken: cancellationToken));
        return rows.Select(ToPlace).ToList();
    }

    public async Task<IReadOnlyList<Place>> ListByNameAsync(Voivodeship voivodeship, string nameKey, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PlaceRow>(new CommandDefinition(
            ListByNameSql, new { Voivodeship = (int)voivodeship, NameKey = nameKey }, cancellationToken: cancellationToken));
        return rows.Select(ToPlace).ToList();
    }

    public async Task<SaveCounts> SaveAsync(IReadOnlyList<Place> places, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var inserted = (await connection.QueryAsync<bool>(new CommandDefinition(SaveSql, new
        {
            Simcs = places.Select(p => p.Simc).ToArray(),
            Names = places.Select(p => p.Name).ToArray(),
            NameKeys = places.Select(p => p.NameKey).ToArray(),
            Voivodeships = places.Select(p => (int)p.Voivodeship).ToArray(),
            Counties = places.Select(p => p.County).ToArray(),
            Municipalities = places.Select(p => p.Municipality).ToArray(),
            Kinds = places.Select(p => (int?)p.Kind).ToArray(),
            Roles = places.Select(p => (int?)p.Role).ToArray(),
            Populations = places.Select(p => p.Population).ToArray(),
            PopulationYears = places.Select(p => p.PopulationYear).ToArray(),
            PopulationSources = places.Select(p => (int?)p.PopulationSource).ToArray(),
            Latitudes = places.Select(p => p.Latitude).ToArray(),
            Longitudes = places.Select(p => p.Longitude).ToArray(),
        }, cancellationToken: cancellationToken))).ToList();

        var added = inserted.Count(i => i);
        var updated = inserted.Count - added;
        return new SaveCounts(added, updated, places.Count - added - updated);
    }

    public async Task<IReadOnlyList<PlaceListItem>> SearchWithOffersAsync(string nameKeyPrefix, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PlaceListRow>(new CommandDefinition(
            SearchWithOffersSql, new { Prefix = nameKeyPrefix, Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(ToListItem).ToList();
    }

    public async Task<PlaceListItem?> GetListItemAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<PlaceListRow>(new CommandDefinition(
            GetListItemSql, new { Id = id }, cancellationToken: cancellationToken));
        return row is null ? null : ToListItem(row);
    }

    public async Task ReplaceNearbyAsync(IReadOnlyList<PlaceNearby> nearby, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(ReplaceNearbySql, new
        {
            PlaceIds = nearby.Select(n => n.PlaceId).ToArray(),
            Tiers = nearby.Select(n => (int)n.Tier).ToArray(),
            Ranks = nearby.Select(n => n.Rank).ToArray(),
            NearbyIds = nearby.Select(n => n.NearbyId).ToArray(),
            Kms = nearby.Select(n => n.Km).ToArray(),
        }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    private static Place ToPlace(PlaceRow r) => new(
        r.Simc, r.Name, (Voivodeship)r.Voivodeship, r.County, r.Municipality, (PlaceKind?)r.Kind, (PlaceRole?)r.Role,
        r.Population, r.PopulationYear, (PlaceSource?)r.PopulationSource, r.Latitude, r.Longitude)
    { Id = r.Id };

    private static PlaceListItem ToListItem(PlaceListRow r) =>
        new(r.Id, r.Name, (Voivodeship)r.Voivodeship, r.County, r.Municipality, (PlaceKind?)r.Kind, r.OfferCount);

    private sealed record PlaceListRow(int Id, string Name, int Voivodeship, string? County, string? Municipality, int? Kind, int OfferCount);

    private sealed record PlaceRow(
        int Id, string Simc, string Name, int Voivodeship, string? County, string? Municipality, int? Kind, int? Role,
        int? Population, int? PopulationYear, int? PopulationSource, decimal? Latitude, decimal? Longitude);
}
