using System.Text;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Networks;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bastion.IntegrationTests.Database;

[SetUpFixture]
public sealed partial class DatabaseSetup
{
    private const UnixFileModes Executable = UnixFileModes.UserRead | UnixFileModes.UserWrite | UnixFileModes.UserExecute
        | UnixFileModes.GroupRead | UnixFileModes.GroupExecute | UnixFileModes.OtherRead | UnixFileModes.OtherExecute;

    private INetwork? _network;
    private PostgreSqlContainer? _postgres;

    public static string AppConnectionString { get; private set; } = "";

    public static string OwnerConnectionString { get; private set; } = "";

    [OneTimeSetUp]
    public async Task Start()
    {
        var repo = FindRepositoryRoot();
        var compose = await File.ReadAllTextAsync(Path.Combine(repo, "compose.yaml"));
        var ownerPassword = Guid.NewGuid().ToString("N");
        var appPassword = Guid.NewGuid().ToString("N");

        _network = new NetworkBuilder().Build();
        _postgres = new PostgreSqlBuilder(ImageOf(compose, "postgres"))
            .WithDatabase("bastion")
            .WithUsername("postgres")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .WithNetwork(_network)
            .WithNetworkAliases("db")
            .WithResourceMapping(new FileInfo(Path.Combine(repo, "db/init/01-roles.sh")), "/docker-entrypoint-initdb.d/", fileMode: Executable)
            .WithResourceMapping(Encoding.UTF8.GetBytes(ownerPassword), "/run/secrets/db_owner_password")
            .WithResourceMapping(Encoding.UTF8.GetBytes(appPassword), "/run/secrets/db_app_password")
            .Build();
        await _postgres.StartAsync();

        await Migrate(repo, compose, ownerPassword);

        AppConnectionString = ConnectionString("bastion_app", appPassword);
        OwnerConnectionString = ConnectionString("bastion_owner", ownerPassword);
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (_network is not null)
        {
            await _network.DisposeAsync();
        }
    }

    private async Task Migrate(string repo, string compose, string ownerPassword)
    {
        var builder = new ContainerBuilder(ImageOf(compose, "dbmate"))
            .WithNetwork(_network)
            .WithEntrypoint("/bin/sh", "/migrate.sh")
            .WithCommand("up")
            .WithResourceMapping(new FileInfo(Path.Combine(repo, "db/migrate.sh")), "/")
            .WithResourceMapping(Encoding.UTF8.GetBytes(ownerPassword), "/run/secrets/db_owner_password");

        var modules = Path.Combine(repo, "src/Modules");
        foreach (var dir in Directory.GetDirectories(modules, "Migrations", SearchOption.AllDirectories))
        {
            builder = builder.WithResourceMapping(new DirectoryInfo(dir), $"/modules/{Path.GetRelativePath(modules, dir)}/");
        }

        await using var migrate = builder.Build();
        await migrate.StartAsync();
        if (await migrate.GetExitCodeAsync() != 0)
        {
            var (stdout, stderr) = await migrate.GetLogsAsync();
            throw new InvalidOperationException($"Migrations failed:\n{stdout}\n{stderr}");
        }
    }

    private string ConnectionString(string user, string password) =>
        new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString()) { Username = user, Password = password }.ConnectionString;

    private static string ImageOf(string compose, string name) =>
        ImageRegex().Matches(compose).Select(m => m.Groups[1].Value).Single(image => image.Contains(name, StringComparison.Ordinal));

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Bastion.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Bastion.slnx) not found.");
    }

    [GeneratedRegex(@"^\s*image:\s*(\S+)", RegexOptions.Multiline)]
    private static partial Regex ImageRegex();
}
