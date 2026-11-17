using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public sealed record ParseResult(IReadOnlyList<Offer> Offers, int SkippedCount, Failure? Failure = null)
{
    public static ParseResult Failed(Failure failure) => new([], 0, failure);
}
