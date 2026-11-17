using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IRawCaptureFileStorage
{
    Task WriteAsync(RawCapture capture, byte[] content, CancellationToken cancellationToken);

    Task<byte[]?> ReadAsync(RawCapture capture, CancellationToken cancellationToken);

    void DeleteDaysBefore(DateOnly day);
}
