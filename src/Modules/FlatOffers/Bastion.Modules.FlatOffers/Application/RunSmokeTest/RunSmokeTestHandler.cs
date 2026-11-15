using Bastion.Modules.FlatOffers.Application.Abstractions;

namespace Bastion.Modules.FlatOffers.Application.RunSmokeTest;

internal sealed class RunSmokeTestHandler(IListingSource source)
{
    public async Task<SmokeTestOutcome> Handle(CancellationToken cancellationToken)
    {
        var result = await source.FetchSmokeTestPageAsync(cancellationToken);
        return result.Succeeded
            ? new SmokeTestOutcome(new SmokeTestResult(source.Name, result.Offers.Count, result.SkippedCount), null, null)
            : new SmokeTestOutcome(null, result.ErrorCode, result.ErrorDetail);
    }
}

internal sealed record SmokeTestResult(string Source, int OfferCount, int SkippedCount);

internal sealed record SmokeTestOutcome(SmokeTestResult? Result, string? ErrorCode, string? ErrorDetail);
