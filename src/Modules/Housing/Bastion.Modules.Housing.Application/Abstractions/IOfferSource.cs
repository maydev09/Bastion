using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IOfferSource
{
    Source Source { get; }

    Task<FetchResult> FetchAsync(CancellationToken cancellationToken);

    RawPage Anonymize(RawPage page);

    ParseResult Parse(RawPage page);
}
