using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

// What the exclusion of an offer is found from; the place's kind and population give its size.
public sealed record ExclusionInput
{
    public required int OfferId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public decimal? Price { get; init; }

    public Currency? PriceCurrency { get; init; }

    public decimal? AreaM2 { get; init; }

    public int? Rooms { get; init; }

    public int? PlaceId { get; init; }

    public Voivodeship? Voivodeship { get; init; }

    public PlaceKind? PlaceKind { get; init; }

    public int? Population { get; init; }
}
