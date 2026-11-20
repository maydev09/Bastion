using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

// All offers with this location key belong to the place; no place when PlaceId is null.
public sealed record PlaceLink(Source Source, string LocationKey, int? PlaceId);
