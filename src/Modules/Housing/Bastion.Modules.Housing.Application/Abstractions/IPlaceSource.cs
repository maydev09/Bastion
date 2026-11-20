namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IPlaceSource
{
    Task<PlaceFetchResult> FetchAsync(CancellationToken cancellationToken);
}
