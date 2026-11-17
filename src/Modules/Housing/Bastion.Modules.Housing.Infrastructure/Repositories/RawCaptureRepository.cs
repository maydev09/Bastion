using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;
using Dapper;
using Npgsql;

namespace Bastion.Modules.Housing.Infrastructure.Repositories;

public sealed class RawCaptureRepository(NpgsqlDataSource db) : IRawCaptureRepository
{
    private const string AddSql = """
        INSERT INTO housing.raw_capture (source_id, kind, content_type, url, http_status, fetched_at, size, sha256)
        VALUES (@SourceId, @Kind, @ContentType, @Url, @HttpStatus, @FetchedAt, @Size, @Sha256)
        RETURNING id
        """;

    private const string ListSql = """
        SELECT id, source_id, kind, content_type, url, http_status, fetched_at, size, sha256
        FROM housing.raw_capture
        ORDER BY fetched_at, id
        """;

    public async Task<int> AddAsync(RawCapture capture, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(AddSql, new
        {
            SourceId = (int)capture.SourceId,
            Kind = (int)capture.Kind,
            capture.ContentType,
            Url = capture.Url.ToString(),
            capture.HttpStatus,
            capture.FetchedAt,
            capture.Size,
            capture.Sha256,
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RawCapture>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(ListSql, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToCapture()).ToList();
    }

    public async Task DeleteFetchedBeforeAsync(DateTimeOffset time, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM housing.raw_capture WHERE fetched_at < @Time", new { Time = time }, cancellationToken: cancellationToken));
    }

    // Dapper cannot map text to Uri, so rows are read as they are and converted here.
    private sealed record Row
    {
        public int Id { get; init; }

        public Source SourceId { get; init; }

        public CaptureKind Kind { get; init; }

        public string ContentType { get; init; } = "";

        public string Url { get; init; } = "";

        public int HttpStatus { get; init; }

        public DateTimeOffset FetchedAt { get; init; }

        public int Size { get; init; }

        public string Sha256 { get; init; } = "";

        public RawCapture ToCapture() =>
            new(Id, SourceId, Kind, ContentType, new Uri(Url), HttpStatus, FetchedAt, Size, Sha256);
    }
}
