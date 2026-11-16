using Bastion.Modules.FlatOffers.Domain;

namespace Bastion.Modules.FlatOffers.Application.ListOffers;

internal sealed record OfferListItem
{
    public required int Id { get; init; }

    public required OfferSource Source { get; init; }

    public required string Title { get; init; }

    public required string Url { get; init; }

    public decimal? Price { get; init; }

    public string? PriceCurrency { get; init; }

    public decimal? AreaM2 { get; init; }

    public int? Rooms { get; init; }

    public required DateTimeOffset FirstSeenAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
