using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IRawCaptureRepository
{
    Task<int> AddAsync(RawCapture capture, CancellationToken cancellationToken);

    // Oldest first.
    Task<IReadOnlyList<RawCapture>> ListAsync(CancellationToken cancellationToken);

    // Returns the number of deleted captures.
    Task<int> DeleteFetchedBeforeAsync(DateTimeOffset time, CancellationToken cancellationToken);
}
