using System.Net.Http.Json;
using Bastion.Host;
using Npgsql;

namespace Bastion.IntegrationTests.Database;

public class DatabaseTests
{
    [Test]
    public async Task Status_shows_app_role_and_migrated_module_schemas()
    {
        await using var factory = TestApp.WithAllModules(b =>
            b.UseSetting($"ConnectionStrings:{DatabaseStatusEndpoint.ConnectionStringName}", DatabaseSetup.AppConnectionString));
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<DatabaseStatus>("/api/platform/database");

        Assert.Multiple(() =>
        {
            Assert.That(status!.User, Is.EqualTo("bastion_app"));
            Assert.That(status.CanChangeSchema, Is.False);
            Assert.That(status.Migrations.Select(m => m.Schema), Is.SupersetOf(new[] { "flat_offers", "job_offers" }));
            Assert.That(status.Migrations.Select(m => m.Version), Has.None.Null);
        });
    }

    [TestCase("CREATE TABLE flat_offers.probe (id int)")]
    [TestCase("CREATE TABLE public.probe (id int)")]
    [TestCase("CREATE SCHEMA probe")]
    [TestCase("DROP TABLE flat_offers.schema_migrations")]
    public async Task App_role_cannot_change_the_schema(string sql)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => Execute(DatabaseSetup.AppConnectionString, sql));

        Assert.That(error!.SqlState, Is.EqualTo(PostgresErrorCodes.InsufficientPrivilege));
    }

    [Test]
    public async Task App_role_can_read_and_write_tables_created_by_migrations()
    {
        await Execute(DatabaseSetup.OwnerConnectionString, "CREATE TABLE flat_offers.rw_probe (id int GENERATED ALWAYS AS IDENTITY, name text)");
        try
        {
            await Execute(DatabaseSetup.AppConnectionString, """
                INSERT INTO flat_offers.rw_probe (name) VALUES ('a');
                UPDATE flat_offers.rw_probe SET name = 'b';
                DELETE FROM flat_offers.rw_probe;
                """);
        }
        finally
        {
            await Execute(DatabaseSetup.OwnerConnectionString, "DROP TABLE flat_offers.rw_probe");
        }
    }

    private static async Task Execute(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
