using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// The populations of one place size: from Min up to, but not including, Max (null: no upper bound).
public sealed record PopulationRange(PlaceKind Kind, int Min, int? Max);
