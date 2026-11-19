using System.Text.Json;
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

    public async Task MarkInterruptedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {schema}.job_run SET status = @Status, finished_at = @Now WHERE finished_at IS NULL",
            new { Status = (int)JobRunStatus.Interrupted, Now = now },
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
