using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bastion.BuildingBlocks.Jobs;

namespace Bastion.IntegrationTests.Database;

public static class JobRuns
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<JobRun> Start(HttpClient client, string job)
    {
        var response = await client.PostAsync(new Uri($"{job}/runs", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobRun>(Json))!;
    }

    // Without a run id it waits for any run to finish.
    public static async Task<JobRun> WaitForFinish(HttpClient client, string job, int? runId = null)
    {
        for (var i = 0; i < 100; i++)
        {
            var latest = await client.GetFromJsonAsync<LatestJobRun>($"{job}/runs/latest", Json);
            if (latest!.Run is { FinishedAt: not null } run && (runId is null || run.Id == runId))
            {
                return run;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The job did not finish.");
    }

    public static async Task<JobRun> Run(HttpClient client, string job)
    {
        var started = await Start(client, job);
        return await WaitForFinish(client, job, started.Id);
    }
}
