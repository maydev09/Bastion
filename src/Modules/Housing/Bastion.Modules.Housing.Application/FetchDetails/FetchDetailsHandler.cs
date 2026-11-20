using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.FetchDetails;

public sealed class FetchDetailsHandler(
    OfferSources sources,
    RawCaptureService rawCaptures,
    IOfferRepository repository,
    TimeProvider time,
    IOptions<DetailsOptions> options)
{
    // A single bad page (HTTP 404 of an offer removed since the last scan) is that offer's failure.
    // Many in a row mean the portal refuses us (403, CAPTCHA): the run stops and those offers are not blamed.
    // A portal out of reach (no VPN, rate limited, network) stops the run at once and blames no offer.
    public const int MaxFetchFailuresInRow = 10;

    private const int BatchSize = 100;

    public async Task<DetailsOutcome> Handle(Source source, CancellationToken cancellationToken)
    {
        if (!sources.TryGet(source, out var offerSource))
        {
            return new DetailsOutcome(null, new Failure(OfferSources.UnknownSource, $"No offer source is registered for {source}.", FailureKind.NotFound));
        }

        var startedAt = time.GetUtcNow();
        var deadline = startedAt.AddMinutes(options.Value.MaxMinutes);
        int fetched = 0, failed = 0;

        // Failed fetches since the last success; blamed on their offers only when the streak ends without stopping the run.
        var streak = new List<(PendingDetails Offer, DateTimeOffset At, string Error)>();
        while (time.GetUtcNow() < deadline)
        {
            // Before the next batch, or these offers would come back in it.
            failed += await BlameAsync(streak, cancellationToken);
            var pending = await repository.ListPendingDetailsAsync(source, BatchSize, options.Value.MaxFailedAttempts, startedAt, cancellationToken);
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
                if (fetch.Failure is { Kind: FailureKind.Unavailable } unavailable)
                {
                    return new DetailsOutcome(null, unavailable);
                }

                if (fetch.Failure is { } fetchFailure)
                {
                    streak.Add((offer, time.GetUtcNow(), fetchFailure.Detail));
                    if (streak.Count >= MaxFetchFailuresInRow)
                    {
                        return new DetailsOutcome(null, fetchFailure);
                    }

                    continue;
                }

                failed += await BlameAsync(streak, cancellationToken);
                var page = offerSource.Anonymize(fetch.Page!);
                var capture = await rawCaptures.SaveAsync(source, page, cancellationToken);
                var error = await SaveAsync(offerSource, page, offer, cancellationToken);
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
        return new DetailsOutcome(new DetailsResult(source, fetched, failed), null);
    }

    private async Task<int> BlameAsync(List<(PendingDetails Offer, DateTimeOffset At, string Error)> streak, CancellationToken cancellationToken)
    {
        foreach (var (offer, at, error) in streak)
        {
            await repository.MarkDetailsFailedAsync(offer, at, error, cancellationToken);
        }

        var count = streak.Count;
        streak.Clear();
        return count;
    }

    // Returns the error, or null when the details were saved.
    private async Task<string?> SaveAsync(IOfferSource offerSource, RawPage page, PendingDetails offer, CancellationToken cancellationToken)
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

        await repository.SaveDetailsAsync(offerSource.Source, offer.ExternalId, parsed.Details!, cancellationToken);
        return null;
    }
}
