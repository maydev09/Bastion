using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bastion.BuildingBlocks.Jobs;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Host;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Platform.Abstractions;
using Dapper;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bastion.IntegrationTests.Database;

public class HousingJobsTests
{
    private const string Retention = "/api/housing/jobs/RawCaptureRetention";
    private const string Import = "/api/housing/jobs/ImportOffers";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private string _rawPath = "";

    [SetUp]
    public async Task ClearData()
    {
        _rawPath = Directory.CreateTempSubdirectory("bastion-raw-").FullName;
        await Sql("DELETE FROM housing.job_run; DELETE FROM housing.offer; DELETE FROM housing.raw_capture;");
    }

    [TearDown]
    public void DeleteRawFiles() => Directory.Delete(_rawPath, recursive: true);

    [Test]
    public async Task Retention_runs_on_its_own_at_four_in_Warsaw_and_deletes_old_raw_data()
    {
        // 03:59 in Warsaw (UTC+1 in January).
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 2, 59, 0, TimeSpan.Zero));
        var oldDay = Directory.CreateDirectory(Path.Combine(_rawPath, "2000-01-01")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(oldDay, "1.zst"), new byte[100]);
        await Sql("""
            INSERT INTO housing.raw_capture (source_id, kind, content_type, url, http_status, fetched_at, size, sha256)
            VALUES (1, 1, 'text/html', 'https://www.otodom.pl/', 200, '2000-01-01T12:00:00Z', 0, '')
            """);
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)));
        using var client = factory.CreateClient();

        clock.Advance(TimeSpan.FromMinutes(1));
        var run = await WaitForFinish(client);

        Assert.Multiple(() =>
        {
            Assert.That(run.Trigger, Is.EqualTo(JobTrigger.Schedule));
            Assert.That(run.QueuedAt, Is.EqualTo(new DateTimeOffset(2026, 1, 15, 3, 0, 0, TimeSpan.Zero)));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded));
            Assert.That(run.Summary!.Counts, Is.EquivalentTo(new Dictionary<string, long> { ["deletedCaptures"] = 1, ["freedBytes"] = 100 }));
            Assert.That(Directory.Exists(oldDay), Is.False);
        });
    }

    [Test]
    public async Task Import_runs_on_its_own_every_interval_and_a_failure_keeps_the_reason()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var noVpn = Substitute.For<IVpnCheck>();
        noVpn.EnsureTunnelAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new ScrapingException(ScrapingException.VpnRequired, "no VPN in tests"));
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)).AddSingleton(noVpn))
            .WithWebHostBuilder(b => b.UseSetting("Modules:Housing:Jobs:ImportOffers:Every", "00:10:00"));
        using var client = factory.CreateClient();

        clock.Advance(TimeSpan.FromMinutes(10));
        var run = await JobRuns.WaitForFinish(client, Import);

        Assert.Multiple(() =>
        {
            Assert.That(run.Trigger, Is.EqualTo(JobTrigger.Schedule));
            Assert.That(run.QueuedAt, Is.EqualTo(new DateTimeOffset(2026, 1, 15, 12, 10, 0, TimeSpan.Zero)));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(run.ErrorCode, Is.EqualTo(ScrapingException.VpnRequired));
        });
    }

    [Test]
    public async Task With_schedules_off_a_job_runs_only_when_started()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)))
            .WithWebHostBuilder(b => b.UseSetting(TestApp.ScheduledKey, "false"));
        using var client = factory.CreateClient();

        // Past 04:00 in Warsaw, when retention runs on its schedule.
        clock.Advance(TimeSpan.FromDays(1));
        await Task.Delay(500);
        var latest = await client.GetFromJsonAsync<LatestJobRun>($"{Retention}/runs/latest", Json);
        var run = await JobRuns.Run(client, Retention);

        Assert.Multiple(() =>
        {
            Assert.That(latest!.Run, Is.Null);
            Assert.That(run.Trigger, Is.EqualTo(JobTrigger.Manual));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded));
        });
    }

    [Test]
    public async Task Manual_start_runs_the_job_in_the_background()
    {
        await using var factory = App();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri($"{Retention}/runs", UriKind.Relative), null);
        var started = await response.Content.ReadFromJsonAsync<JobRun>(Json);
        var run = await WaitForFinish(client, started!.Id);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            Assert.That(run.Trigger, Is.EqualTo(JobTrigger.Manual));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded));
        });
    }

    [Test]
    public async Task Second_start_of_a_running_job_returns_the_same_run()
    {
        var release = new TaskCompletionSource<int>();
        var repository = Substitute.For<IRawCaptureRepository>();
        repository.DeleteFetchedBeforeAsync(default, default).ReturnsForAnyArgs(release.Task);
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton(repository)));
        using var client = factory.CreateClient();

        var first = await Start(client);
        var second = await Start(client);
        release.SetResult(0);
        var run = await WaitForFinish(client, first.Id);

        Assert.Multiple(() =>
        {
            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded));
        });
    }

    [Test]
    public async Task Failed_run_keeps_the_error_code()
    {
        var repository = Substitute.For<IRawCaptureRepository>();
        repository.DeleteFetchedBeforeAsync(default, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("disk gone"));
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton(repository)));
        using var client = factory.CreateClient();

        var started = await Start(client);
        var run = await WaitForFinish(client, started.Id);

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(run.ErrorCode, Is.EqualTo(JobFailedException.Unexpected));
        });
    }

    [Test]
    public async Task Run_left_unfinished_is_interrupted_after_a_restart()
    {
        var id = await Scalar<int>("""
            INSERT INTO housing.job_run (job, trigger, status, queued_at, started_at)
            VALUES ('RawCaptureRetention', 1, 2, now() - interval '1 minute', now() - interval '1 minute')
            RETURNING id
            """);
        await using var factory = App();
        using var client = factory.CreateClient();

        var run = await WaitForFinish(client, id);

        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Interrupted));
    }

    [Test]
    public async Task Module_clients_hear_about_finished_runs()
    {
        await using var factory = App();
        using var client = factory.CreateClient();
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/api/housing/jobs/hub"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        var finished = new TaskCompletionSource<JobRun>();
        hub.On<JobRun>(nameof(IJobsClient.JobRunFinished), run => finished.TrySetResult(run));
        await hub.StartAsync();

        var started = await Start(client);
        var run = await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(run.Id, Is.EqualTo(started.Id));
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded));
        });
    }

    [Test]
    public async Task The_job_list_shows_every_job_with_its_schedule_and_latest_run()
    {
        await using var factory = App();
        using var client = factory.CreateClient();
        var run = await WaitForFinish(client, (await Start(client)).Id);

        var list = await client.GetFromJsonAsync<JobList>("/api/housing/jobs", Json);

        Assert.Multiple(() =>
        {
            Assert.That(list!.Jobs.Select(j => j.Name), Is.EqualTo(new[]
            {
                "ImportOffers", "ScanOffers", "FetchDetails", "ReparseOffers", "RawCaptureRetention", "ImportPlaces", "LinkPlaces",
                "CalculateMonthlyCosts",
            }));
            var import = list.Jobs.Single(j => j.Name == "ImportOffers");
            Assert.That((import.Every, import.DailyAt), Is.EqualTo(((TimeSpan?)TimeSpan.FromMinutes(10), (TimeOnly?)null)));
            var retention = list.Jobs.Single(j => j.Name == "RawCaptureRetention");
            Assert.That((retention.DailyAt, retention.LatestRun?.Id), Is.EqualTo(((TimeOnly?)new TimeOnly(4, 0), (int?)run.Id)));
            Assert.That(list.Jobs.Single(j => j.Name == "ReparseOffers").LatestRun, Is.Null);
            Assert.That(list.TimeZone, Is.EqualTo("Europe/Warsaw"));
        });
    }

    [Test]
    public async Task Run_history_is_newest_first_and_narrowed_by_job_status_trigger_and_time()
    {
        await Sql("""
            INSERT INTO housing.job_run (job, trigger, status, queued_at, started_at, finished_at, error_code)
            VALUES ('RawCaptureRetention', 2, 3, '2026-11-20T03:00:00Z', '2026-11-20T03:00:00Z', '2026-11-20T03:00:05Z', NULL),
                   ('ImportOffers', 2, 4, '2026-11-20T03:10:00Z', '2026-11-20T03:10:00Z', '2026-11-20T03:10:09Z', 'housing.pageLimitReached'),
                   ('RawCaptureRetention', 1, 4, '2026-11-21T03:00:00Z', '2026-11-21T03:00:00Z', '2026-11-21T03:00:01Z', 'jobs.unexpected');
            """);
        await using var factory = App();
        using var client = factory.CreateClient();

        var all = await client.GetFromJsonAsync<PagedResult<JobRun>>("/api/housing/jobs/runs", Json);
        var retention = await client.GetFromJsonAsync<PagedResult<JobRun>>("/api/housing/jobs/runs?job=RawCaptureRetention", Json);
        var failed = await client.GetFromJsonAsync<PagedResult<JobRun>>("/api/housing/jobs/runs?status=Failed&pageSize=1&page=2", Json);
        var manual = await client.GetFromJsonAsync<PagedResult<JobRun>>("/api/housing/jobs/runs?trigger=Manual", Json);
        // One day in Warsaw: from inclusive, to exclusive.
        var day = await client.GetFromJsonAsync<PagedResult<JobRun>>(
            "/api/housing/jobs/runs?from=2026-11-20T04:00:00%2B01:00&to=2026-11-21T04:00:00%2B01:00", Json);
        // Without an offset a time is UTC, whatever the server's time zone.
        var utc = await client.GetFromJsonAsync<PagedResult<JobRun>>(
            "/api/housing/jobs/runs?from=2026-11-20T03:05:00&to=2026-11-21T03:00:00", Json);
        var unknown = await client.GetAsync(new Uri("/api/housing/jobs/runs?job=nothing", UriKind.Relative));

        Assert.Multiple(() =>
        {
            Assert.That(all!.Items.Select(r => r.Job), Is.EqualTo(new[] { "RawCaptureRetention", "ImportOffers", "RawCaptureRetention" }));
            Assert.That(retention!.Items.Select(r => r.Status), Is.EqualTo(new[] { JobRunStatus.Failed, JobRunStatus.Succeeded }));
            Assert.That((failed!.TotalCount, failed.Items.Single().Job), Is.EqualTo((2, "ImportOffers")));
            Assert.That(manual!.Items.Select(r => r.QueuedAt.Day), Is.EqualTo(new[] { 21 }));
            Assert.That(day!.Items.Select(r => r.Job), Is.EqualTo(new[] { "ImportOffers", "RawCaptureRetention" }));
            Assert.That(utc!.Items.Select(r => r.Job), Is.EqualTo(new[] { "ImportOffers" }));
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task Unknown_job_is_not_found()
    {
        await using var factory = App();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri("/api/housing/jobs/nothing/runs", UriKind.Relative), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static Task<JobRun> Start(HttpClient client) => JobRuns.Start(client, Retention);

    private static Task<JobRun> WaitForFinish(HttpClient client, int? runId = null) => JobRuns.WaitForFinish(client, Retention, runId);

    private WebApplicationFactory<Program> App(Action<IServiceCollection>? services = null) =>
        TestApp.WithAllModules(b => b
            .UseSetting($"ConnectionStrings:{DatabaseStatusEndpoint.ConnectionStringName}", DatabaseSetup.AppConnectionString)
            .UseSetting("Modules:Housing:RawCaptures:Path", _rawPath)
            .ConfigureTestServices(services ?? (_ => { })));

    private static async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(DatabaseSetup.AppConnectionString);
        await connection.ExecuteAsync(sql);
    }

    private static async Task<T> Scalar<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(DatabaseSetup.AppConnectionString);
        return (await connection.ExecuteScalarAsync<T>(sql))!;
    }
}
