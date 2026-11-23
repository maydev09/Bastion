using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListPlaces;

// The populations of a place size under the configured thresholds: from MinPopulation up to, but not including,
// MaxPopulation (null: no upper bound).
public sealed record PlaceSizeListItem(PlaceSize Size, PlaceKind Kind, int MinPopulation, int? MaxPopulation);
