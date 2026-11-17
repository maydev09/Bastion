using System.Security.Cryptography;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.RawCaptures;

public sealed class RawCaptureService(
    IRawCaptureRepository repository,
    IRawCaptureFileStorage files,
    TimeProvider time,
    IOptions<RawCaptureOptions> options)
{
    public async Task<RawCapture> SaveAsync(Source source, RawPage page, CancellationToken cancellationToken)
    {
        var capture = new RawCapture(
            0,
            source,
            page.Kind,
            page.ContentType,
            page.Url,
            page.HttpStatus,
            time.GetUtcNow(),
            page.Content.Length,
            Convert.ToHexStringLower(SHA256.HashData(page.Content)));
        capture = capture with { Id = await repository.AddAsync(capture, cancellationToken) };
        await files.WriteAsync(capture, page.Content, cancellationToken);
        return capture;
    }

    public Task<IReadOnlyList<RawCapture>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);

    public async Task<RawPage?> ReadAsync(RawCapture capture, CancellationToken cancellationToken)
    {
        var content = await files.ReadAsync(capture, cancellationToken);
        return content is null
            ? null
            : new RawPage(capture.Url, capture.Kind, capture.ContentType, capture.HttpStatus, content);
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken)
    {
        var firstKeptDay = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(-options.Value.RetentionDays);
        var start = new DateTimeOffset(firstKeptDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        await repository.DeleteFetchedBeforeAsync(start, cancellationToken);
        files.DeleteDaysBefore(firstKeptDay);
    }
}
