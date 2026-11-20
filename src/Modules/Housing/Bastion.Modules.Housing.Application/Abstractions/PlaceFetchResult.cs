using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public sealed record PlaceFetchResult(IReadOnlyList<Place> Places, Failure? Failure = null)
{
    public static PlaceFetchResult Failed(Failure failure) => new([], failure);
}
