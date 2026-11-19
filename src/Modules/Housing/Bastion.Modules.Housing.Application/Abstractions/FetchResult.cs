using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public sealed record FetchResult(RawPage? Page, Failure? Failure = null)
{
    public static FetchResult Failed(Failure failure) => new(null, failure);
}
