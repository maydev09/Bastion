using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.MonthlyCosts;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.FetchDetails;

public sealed class FetchDetailsHandler(
    OfferSources sources,
    RawCaptureService rawCaptures,
    IOfferRepository repository,
    MonthlyCostService costs,
    TimeProvider time,
    IOptions<DetailsOptions> options)
{
    // A removed offer (HTTP 410) is no failure: the offer is marked removed and leaves the queue.
    // A single bad page is that offer's failure.
    // Many in a row mean the portal refuses us (403, CAPTCHA): the run stops and those offers are not blamed.
    // A missing page (HTTP 404) is no sign of a refusal; only far more of them in a row mean the whole portal
    // answers so (changed URLs, a block page) and stop the run.
    // A portal out of reach (no VPN, network, timeout) is retried after FirstRetrySeconds, then twice as long each time;
    // when the retries run out it stops the run and blames no offer. When a retry would pass the time limit, the run ends
    // as at its time limit and the offer waits for the next run.
    // A rate limited portal (429, 503) stops the run at once: the scraping client already backs off before its next request.
    public const int MaxFetchFailuresInRow = 10;

    // Below BatchSize: the streak is blamed before each batch.
    public const int MaxMissingPagesInRow = 50;

    // The longest a retried request may take (the HTTP timeout plus the pause between requests), so a retry ends before the deadline.
    private static readonly TimeSpan RequestAllowance = TimeSpan.FromMinutes(1);

    private const int BatchSize = 100;

    public async Task<DetailsOutcome> Handle(Source source, CancellationToken cancellationToken)
    {
        if (!sources.TryGet(source, out var offerSource))
        {
            return new DetailsOutcome(null, new Failure(OfferSources.UnknownSource, $"No offer source is registered for {source}.", FailureKind.NotFound));
        }

        var startedAt = time.GetUtcNow();
        var deadline = startedAt.AddMinutes(options.Value.MaxMinutes);
        var retryGivenUpBefore = startedAt.AddHours(-options.Value.RetryAfterHours);
        int fetched = 0, failed = 0, removed = 0, retried = 0;

        // Failed fetches since the last success; blamed on their offers only when the streak ends without stopping the run.
        var streak = new List<(PendingDetails Offer, DateTimeOffset At, string Error, bool IsMissing)>();
        while (time.GetUtcNow() < deadline)
        {
            // Before the next batch, or these offers would come back in it.
            failed += await BlameAsync(streak, cancellationToken);
            var pending = await repository.ListPendingDetailsAsync(
                source, BatchSize, options.Value.MaxFailedAttempts, startedAt, retryGivenUpBefore, cancellationToken);
            if (pending.Count == 0)
            {
                break;
            }

            foreach (var offer in pending)
            {
                if (time.GetUtcNow() >= deadline)
                {
                    break;
                }

                var fetch = await offerSource.FetchDetailPageAsync(offer.Url, cancellationToken);
                var retries = 0;
                var outOfTime = false;
                while (fetch.Failure is { Kind: FailureKind.Unavailable } && retries < options.Value.Retries)
                {
                    var wait = TimeSpan.FromSeconds(options.Value.FirstRetrySeconds * (1 << retries));
                    if (time.GetUtcNow() + wait + RequestAllowance >= deadline)
                    {
                        outOfTime = true;
                        break;
                    }

                    await Task.Delay(wait, time, cancellationToken);
                    fetch = await offerSource.FetchDetailPageAsync(offer.Url, cancellationToken);
                    retries++;
                }

                retried += retries;
                if (outOfTime)
                {
                    failed += await BlameAsync(streak, cancellationToken);
                    return new DetailsOutcome(new DetailsResult(source, fetched, failed, removed, retried), null);
                }

                if (fetch.Failure is { Kind: FailureKind.Unavailable or FailureKind.RateLimited } unavailable)
                {
                    // The summary counts the retries of the whole run; the error says how often this request was retried.
                    return new DetailsOutcome(new DetailsResult(source, fetched, failed, removed, retried),
                        unavailable with { Detail = $"{unavailable.Detail} (after {retries} retries)" });
                }

                if (fetch.Failure is { Kind: FailureKind.Removed })
                {
                    await repository.MarkRemovedAsync(offer, cancellationToken);
                    removed++;
                    continue;
                }

                if (fetch.Failure is { } fetchFailure)
                {
                    streak.Add((offer, time.GetUtcNow(), fetchFailure.Detail, fetchFailure.Kind == FailureKind.Missing));
                    if (streak.Count(f => !f.IsMissing) >= MaxFetchFailuresInRow || streak.Count(f => f.IsMissing) >= MaxMissingPagesInRow)
                    {
                        return new DetailsOutcome(new DetailsResult(source, fetched, failed, removed, retried), fetchFailure);
                    }

                    continue;
                }

                failed += await BlameAsync(streak, cancellationToken);
                var page = offerSource.Anonymize(fetch.Page!);
                var capture = await rawCaptures.SaveAsync(source, page, cancellationToken);
                var error = await SaveAsync(offerSource, page, capture, offer, cancellationToken);
                if (error is null)
                {
                    await repository.MarkDetailsFetchedAsync(offer, capture.FetchedAt, cancellationToken);
                    fetched++;
                }
                else
                {
                    await repository.MarkDetailsFailedAsync(offer, capture.FetchedAt, error, cancellationToken);
                    failed++;
                }
            }
        }

        failed += await BlameAsync(streak, cancellationToken);
        return new DetailsOutcome(new DetailsResult(source, fetched, failed, removed, retried), null);
    }

    private async Task<int> BlameAsync(List<(PendingDetails Offer, DateTimeOffset At, string Error, bool IsMissing)> streak, CancellationToken cancellationToken)
    {
        foreach (var (offer, at, error, _) in streak)
        {
            await repository.MarkDetailsFailedAsync(offer, at, error, cancellationToken);
        }

        var count = streak.Count;
        streak.Clear();
        return count;
    }

    // Returns the error, or null when the details were saved.
    private async Task<string?> SaveAsync(
        IOfferSource offerSource, RawPage page, RawCapture capture, PendingDetails offer, CancellationToken cancellationToken)
    {
        var parsed = offerSource.ParseDetails(page);
        if (parsed.Failure is { } failure)
        {
            return failure.Detail;
        }

        // A removed offer may redirect to another one.
        if (parsed.ExternalId != offer.ExternalId)
        {
            return $"{offer.Url} shows offer {parsed.ExternalId}, not {offer.ExternalId}.";
        }

        await repository.SaveDetailsAsync(capture, offer.ExternalId, parsed.Details!.ToDomain(), recordChanges: true, cancellationToken);
        await costs.UpdateAsync(capture.SourceId, [offer.ExternalId], cancellationToken);
        return null;
    }
}
