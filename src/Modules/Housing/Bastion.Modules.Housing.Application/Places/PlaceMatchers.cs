using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// The matcher over all places (about 64 000), built on first use and kept until ImportPlaces stores new places,
// so the frequent offer imports do not read the whole dictionary every time.
public sealed class PlaceMatchers(IPlaceRepository places) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private PlaceMatcher? _current;

    public async Task<PlaceMatcher> GetAsync(CancellationToken cancellationToken)
    {
        if (_current is { } current)
        {
            return current;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _current ??= new PlaceMatcher(await places.ListAsync(cancellationToken));
        }
        finally
        {
            _lock.Release();
        }
    }

    public PlaceMatcher Replace(IReadOnlyList<Place> stored) => _current = new PlaceMatcher(stored);

    public void Dispose() => _lock.Dispose();
}
