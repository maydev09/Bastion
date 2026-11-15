using Bastion.Modules.FlatOffers.Domain;

namespace Bastion.Modules.FlatOffers.Application.Abstractions;

internal interface IListingSource
{
    string Name { get; }

    Task<ListingFetchResult> FetchSmokeTestPageAsync(CancellationToken cancellationToken);
}

internal sealed record ListingFetchResult(IReadOnlyList<ListingOffer> Offers, int SkippedCount, string? ErrorCode = null, string? ErrorDetail = null)
{
    public bool Succeeded => ErrorCode is null;

    public static ListingFetchResult Failed(string code, string detail) => new([], 0, code, detail);
}
