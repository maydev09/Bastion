using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace Bastion.Host;

internal static partial class DatabaseStatusEndpoint
{
    public const string ConnectionStringName = "Bastion";
    public const string Unavailable = "database.unavailable";

    private const string ServerSql = """
        SELECT current_user AS "User",
               split_part(current_setting('server_version'), ' ', 1) AS "ServerVersion",
               has_database_privilege(current_database(), 'CREATE')
                 OR EXISTS (SELECT 1 FROM pg_namespace
                            WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema'
                              AND has_schema_privilege(oid, 'CREATE')) AS "CanChangeSchema"
        """;

    private const string MigrationSchemasSql = """
        SELECT table_schema FROM information_schema.tables
        WHERE table_name = 'schema_migrations'
        ORDER BY table_schema
        """;

    public static void MapDatabaseStatus(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/platform/database", async Task<Results<Ok<DatabaseStatus>, ProblemHttpResult>> (
            NpgsqlDataSource db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                return TypedResults.Ok(await Read(db, cancellationToken));
            }
            catch (NpgsqlException e)
            {
                LogCheckFailed(loggerFactory.CreateLogger(typeof(DatabaseStatusEndpoint)), e);
                return Failure(Unavailable, "Could not connect to the database.");
            }
        })
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
        .WithTags("Platform")
        .WithName("GetDatabaseStatus")
        .WithSummary("Connects as the application role and returns the server version, whether the role can change the schema, and the last migration of every module.");

    private static async Task<DatabaseStatus> Read(NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        var server = await connection.QuerySingleAsync<ServerInfo>(new CommandDefinition(ServerSql, cancellationToken: cancellationToken));
        var schemas = await connection.QueryAsync<string>(new CommandDefinition(MigrationSchemasSql, cancellationToken: cancellationToken));

        var migrations = new List<SchemaMigration>();
        foreach (var schema in schemas)
        {
            var quoted = $"\"{schema.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
            var version = await connection.QuerySingleAsync<string?>(new CommandDefinition(
                $"SELECT max(version) FROM {quoted}.schema_migrations", cancellationToken: cancellationToken));
            migrations.Add(new SchemaMigration(schema, version));
        }

        return new DatabaseStatus(server.User, server.ServerVersion, server.CanChangeSchema, migrations);
    }

    private static ProblemHttpResult Failure(string code, string detail) =>
        TypedResults.Problem(
            detail: detail,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database unavailable",
            extensions: new Dictionary<string, object?> { ["code"] = code });

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database status check failed")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);

    private sealed record ServerInfo(string User, string ServerVersion, bool CanChangeSchema);
}
