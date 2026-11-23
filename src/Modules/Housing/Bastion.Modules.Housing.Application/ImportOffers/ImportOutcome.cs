using Bastion.Modules.Housing.Application.Abstractions;

namespace Bastion.Modules.Housing.Application.ImportOffers;

// A failure after some pages keeps their result, so the run can still show what was done.
public sealed record ImportOutcome(ImportResult? Result, Failure? Failure);
