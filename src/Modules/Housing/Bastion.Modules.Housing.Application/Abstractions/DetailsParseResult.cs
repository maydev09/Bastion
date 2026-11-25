namespace Bastion.Modules.Housing.Application.Abstractions;

public sealed record DetailsParseResult(string? ExternalId, ParsedOfferDetails? Details, Failure? Failure = null)
{
    public static DetailsParseResult Failed(Failure failure) => new(null, null, failure);
}
