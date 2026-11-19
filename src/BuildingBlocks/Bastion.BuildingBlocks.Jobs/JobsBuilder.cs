using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.BuildingBlocks.Jobs;

public sealed class JobsBuilder(IServiceCollection services, string schema, IConfiguration moduleSection)
{
    internal List<JobDefinition> Jobs { get; } = [];

    // Settings of the job are read from [Modules.<Module>.Jobs.<name>].
    public JobsBuilder Add<TJob>(string name)
        where TJob : class, IJob
    {
        services.AddTransient<TJob>();
        services.AddOptions<JobScheduleOptions>(JobRunner.OptionsName(schema, name))
            .Bind(moduleSection.GetSection(JobScheduleOptions.Section).GetSection(name))
            .Validate(o => o.DailyAt is null || o.Every is null, $"Job {name}: set DailyAt or Every, not both.")
            .Validate(o => o.Every is null || o.Every >= JobScheduleOptions.MinEvery, $"Job {name}: Every must be at least {JobScheduleOptions.MinEvery}.")
            .ValidateOnStart();
        Jobs.Add(new JobDefinition(name, typeof(TJob)));
        return this;
    }
}
