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
            .Bind(moduleSection.GetSection(JobScheduleOptions.Section).GetSection(name));
        Jobs.Add(new JobDefinition(name, typeof(TJob)));
        return this;
    }
}
