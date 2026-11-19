using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace Bastion.BuildingBlocks.Jobs;

public static class JobsServiceCollectionExtensions
{
    // The module's migrations create <schema>.job_run.
    public static JobsBuilder AddJobs(this IServiceCollection services, string schema, IConfiguration moduleSection)
    {
        services.AddSignalR()
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<JobsOptions>()
            .BindConfiguration(JobsOptions.Section)
            .ValidateDataAnnotations()
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Jobs:TimeZone is not a known time zone.")
            .ValidateOnStart();

        var builder = new JobsBuilder(services, schema, moduleSection);
        services.AddKeyedSingleton(schema, (sp, _) => new JobRunner(
            schema,
            builder.Jobs,
            new JobRunRepository(sp.GetRequiredService<NpgsqlDataSource>(), schema),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IHubContext<JobsHub, IJobsClient>>(),
            sp.GetRequiredService<IOptionsMonitor<JobScheduleOptions>>(),
            sp.GetRequiredService<IOptions<JobsOptions>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<JobRunner>>()));
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredKeyedService<JobRunner>(schema));
        return builder;
    }
}
