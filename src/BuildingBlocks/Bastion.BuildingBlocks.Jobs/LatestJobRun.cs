namespace Bastion.BuildingBlocks.Jobs;

// The server's time lets the UI count the elapsed seconds of a run without trusting the browser's clock.
public sealed record LatestJobRun(JobRun? Run, DateTimeOffset ServerTime);
