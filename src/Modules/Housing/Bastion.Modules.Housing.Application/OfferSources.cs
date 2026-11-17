using System.Diagnostics.CodeAnalysis;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application;

public sealed class OfferSources(IEnumerable<IOfferSource> sources)
{
    private readonly Dictionary<Source, IOfferSource> _bySource = sources.ToDictionary(s => s.Source);

    public bool TryGet(Source source, [NotNullWhen(true)] out IOfferSource? offerSource) =>
        _bySource.TryGetValue(source, out offerSource);
}
