using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.DerivedData;
using Bastion.Modules.Housing.Application.FetchDetails;
using Bastion.Modules.Housing.Application.ImportOffers;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Logging;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// A collecting run, for every source: the listing (new offers or all of them, by mode), then the detail pages of the
// offers that need them until the queue is empty or the run's time limit passes, then the derived data of all offers.
// The run ends with complete data. A failing source stops the run, which still calculates the derived data and keeps the
// counts of the work done; a rate limited portal only ends the detail pages, as the scraping client already slows down.
public abstract partial class ImportJob(
    OfferSources sources,
    ImportOffersHandler listing,
    FetchDetailsHandler details,
    DerivedDataService derivedData,
    ILogger<ImportJob> logger,
    ImportMode mode) : IJob
{
    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        var counts = new Counts();
        Failure? failure = null;
        foreach (var source in sources.All)
        {
            failure = await CollectAsync(source, context.Deadline, counts, cancellationToken);
            if (failure is not null)
            {
                break;
            }
        }

        var derived = await RecalculateAsync(failure, cancellationToken);
        var summary = BuildSummary(counts, derived);
        if (failure is not null)
        {
            throw new JobFailedException(failure.Code, failure.Detail) { Summary = summary };
        }

        return summary;
    }

    // After a failure the run fails with that failure, which the UI explains; a failing calculation is only logged then.
    private async Task<DerivedDataResult?> RecalculateAsync(Failure? failure, CancellationToken cancellationToken)
    {
        try
        {
            return await derivedData.RecalculateAllAsync(cancellationToken);
        }
        catch (Exception e) when (failure is not null && e is not OperationCanceledException)
        {
            LogRecalculationFailed(logger, e, failure.Code);
            return null;
        }
    }

    private async Task<Failure?> CollectAsync(Source source, DateTimeOffset? deadline, Counts counts, CancellationToken cancellationToken)
    {
        var imported = await listing.Handle(source, mode, cancellationToken);
        if (imported.Result is { } result)
        {
            counts.Pages += result.PageCount;
            counts.Added += result.AddedCount;
            counts.Updated += result.UpdatedCount;
            counts.Unchanged += result.UnchangedCount;
            counts.Skipped += result.SkippedCount;
            counts.Missed += result.MissedCount;
            counts.Removed += result.RemovedCount;
        }

        if (imported.Failure is { } listingFailure)
        {
            return listingFailure;
        }

        var fetched = await details.Handle(source, deadline, cancellationToken);
        if (fetched.Result is { } detailResult)
        {
            counts.DetailsFetched += detailResult.FetchedCount;
            counts.DetailsFailed += detailResult.FailedCount;
            counts.DetailsRemoved += detailResult.RemovedCount;
            counts.DetailsRetried += detailResult.RetriedCount;
        }

        return fetched.Failure is { Kind: not FailureKind.RateLimited } detailFailure ? detailFailure : null;
    }

    private JobSummary BuildSummary(Counts counts, DerivedDataResult? derived)
    {
        var summary = new Dictionary<string, long>
        {
            ["pages"] = counts.Pages,
            ["added"] = counts.Added,
            ["updated"] = counts.Updated,
            ["unchanged"] = counts.Unchanged,
            ["skipped"] = counts.Skipped,
        };
        if (mode == ImportMode.FullScan)
        {
            summary["missed"] = counts.Missed;
            summary["removed"] = counts.Removed;
        }

        summary["detailsFetched"] = counts.DetailsFetched;
        summary["detailsFailed"] = counts.DetailsFailed;
        summary["detailsRemoved"] = counts.DetailsRemoved;
        summary["detailsRetried"] = counts.DetailsRetried;
        if (derived is not null)
        {
            summary["estimatedRents"] = derived.Costs.EstimatedRentCount;
            summary["excludedOffers"] = derived.Exclusions.Excluded.Values.Sum();
        }

        return new JobSummary(summary);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The derived data could not be recalculated after the collecting run failed with {Code}")]
    private static partial void LogRecalculationFailed(ILogger logger, Exception exception, string code);

    private sealed class Counts
    {
        public int Pages { get; set; }

        public int Added { get; set; }

        public int Updated { get; set; }

        public int Unchanged { get; set; }

        public int Skipped { get; set; }

        public int Missed { get; set; }

        public int Removed { get; set; }

        public int DetailsFetched { get; set; }

        public int DetailsFailed { get; set; }

        public int DetailsRemoved { get; set; }

        public int DetailsRetried { get; set; }
    }
}
