namespace Bastion.BuildingBlocks.Jobs;

// What the user sees after a run, e.g. { "deletedCaptures": 3, "freedBytes": 1024 }.
public sealed record JobSummary(IReadOnlyDictionary<string, long> Counts);
