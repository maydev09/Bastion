using Bastion.Modules.Housing.Application.Abstractions;

namespace Bastion.Modules.Housing.Application.FetchDetails;

public sealed record DetailsOutcome(DetailsResult? Result, Failure? Failure);
