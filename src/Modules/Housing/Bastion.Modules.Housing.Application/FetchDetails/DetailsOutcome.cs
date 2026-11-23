using Bastion.Modules.Housing.Application.Abstractions;

namespace Bastion.Modules.Housing.Application.FetchDetails;

// A failure after some work keeps its result, so the run can still show what was done.
public sealed record DetailsOutcome(DetailsResult? Result, Failure? Failure);
