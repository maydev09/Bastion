using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

// A portal's location key shared by OfferCount offers.
public sealed record OfferLocation(Source Source, string LocationKey, int OfferCount);
