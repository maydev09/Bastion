using Bastion.Modules.Housing.Application.Abstractions;

namespace Bastion.Modules.Housing.Application.ImportOffers;

public sealed record ImportOutcome(ImportResult? Result, Failure? Failure);
