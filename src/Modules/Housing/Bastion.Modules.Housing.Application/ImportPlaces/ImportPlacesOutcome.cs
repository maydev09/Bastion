using Bastion.Modules.Housing.Application.Abstractions;

namespace Bastion.Modules.Housing.Application.ImportPlaces;

public sealed record ImportPlacesOutcome(ImportPlacesResult? Result, Failure? Failure);
