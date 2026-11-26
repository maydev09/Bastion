using System.Text.Json;
using Bastion.Platform.Abstractions;
using Dapper;
using Npgsql;

namespace Bastion.BuildingBlocks.Jobs;

// The schema comes from the module's code, never from a request, so it is safe in the SQL text.
public sealed class JobRunRepository(NpgsqlDataSource db, string schema)
{
    private const string Columns = "id, job, trigger, status, queued_at, started_at, finished_at, error_code, summary::text AS summary";

    public async Task<JobRun> AddAsync(string job, JobTrigger trigger, DateTimeOffset queuedAt, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            INSERT INTO {schema}.job_run (job, trigger, status, queued_at)
            VALUES (@Job, @Trigger, @Status, @QueuedAt)
            RETURNING id
            """,
            new { Job = job, Trigger = (int)trigger, Status = (int)JobRunStatus.Queued, QueuedAt = queuedAt },
            cancellationToken: cancellationToken));
        return new JobRun(id, job, trigger, JobRunStatus.Queued, queuedAt, null, null, null, null);
    }

    public async Task UpdateAsync(JobRun run, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE {schema}.job_run
            SET status = @Status, started_at = @StartedAt, finished_at = @FinishedAt, error_code = @ErrorCode, summary = @Summary::jsonb
            WHERE id = @Id
            """,
            new
            {
                run.Id,
                Status = (int)run.Status,
                run.StartedAt,
                run.FinishedAt,
                run.ErrorCode,
                Summary = run.Summary is null ? null : JsonSerializer.Serialize(run.Summary.Counts),
            },
            cancellationToken: cancellationToken));
    }

    public async Task<JobRun?> LatestAsync(string job, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM {schema}.job_run WHERE job = @Job ORDER BY id DESC LIMIT 1",
            new { Job = job },
            cancellationToken: cancellationToken));
        return row?.ToRun();
    }

    // The latest run of every job that has run.
    public async Task<IReadOnlyList<JobRun>> ListLatestAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"SELECT DISTINCT ON (job) {Columns} FROM {schema}.job_run ORDER BY job, id DESC",
            cancellationToken: cancellationToken));
        return rows.Select(r => r.ToRun()).ToList();
    }

    // Newest first; a null job or status leaves that filter off.
    public async Task<PagedResult<JobRun>> ListAsync(JobRunFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "TRUE" };
        if (filter.Job is not null)
        {
            conditions.Add("job = @Job");
        }

        if (filter.Status is not null)
        {
            conditions.Add("status = @Status");
        }

        if (filter.Trigger is not null)
        {
            conditions.Add("trigger = @Trigger");
        }

        if (filter.From is not null)
        {
            conditions.Add("queued_at >= @From");
        }

        if (filter.To is not null)
        {
            conditions.Add("queued_at < @To");
        }

        var where = string.Join(" AND ", conditions);
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT {Columns} FROM {schema}.job_run WHERE {where} ORDER BY id DESC LIMIT @PageSize OFFSET @Offset;
            SELECT count(*)::int FROM {schema}.job_run WHERE {where};
            """,
            new
            {
                filter.Job,
                Status = (int?)filter.Status,
                Trigger = (int?)filter.Trigger,
                // Npgsql writes timestamptz only from UTC values; the API takes any offset.
                From = filter.From?.ToUniversalTime(),
                To = filter.To?.ToUniversalTime(),
                page.PageSize,
                page.Offset,
            },
            cancellationToken: cancellationToken));
        var runs = (await results.ReadAsync<Row>()).Select(r => r.ToRun()).ToList();
        return new PagedResult<JobRun>(runs, page.Page, page.PageSize, await results.ReadSingleAsync<int>());
    }

    // Only runs queued before the application started, so runs of the current start are never touched.
    public async Task MarkInterruptedAsync(DateTimeOffset queuedBefore, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {schema}.job_run SET status = @Status, finished_at = @Now WHERE finished_at IS NULL AND queued_at < @QueuedBefore",
            new { Status = (int)JobRunStatus.Interrupted, Now = now, QueuedBefore = queuedBefore },
            cancellationToken: cancellationToken));
    }

    // Dapper cannot map jsonb to the summary, so rows are read as they are and converted here.
    private sealed record Row
    {
        public int Id { get; init; }

        public string Job { get; init; } = "";

        public JobTrigger Trigger { get; init; }

        public JobRunStatus Status { get; init; }

        public DateTimeOffset QueuedAt { get; init; }

        public DateTimeOffset? StartedAt { get; init; }

        public DateTimeOffset? FinishedAt { get; init; }

        public string? ErrorCode { get; init; }

        public string? Summary { get; init; }

        public JobRun ToRun() => new(
            Id,
            Job,
            Trigger,
            Status,
            QueuedAt,
            StartedAt,
            FinishedAt,
            ErrorCode,
            Summary is null ? null : new JobSummary(JsonSerializer.Deserialize<Dictionary<string, long>>(Summary)!));
    }
}
