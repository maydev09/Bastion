using System.Globalization;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZstdSharp;

namespace Bastion.Modules.Housing.Infrastructure.Files;

public sealed class RawCaptureFileStorage(IOptions<RawCaptureOptions> options, IHostEnvironment environment) : IRawCaptureFileStorage
{
    private const string DayFormat = "yyyy-MM-dd";

    private string Root => Path.Combine(environment.ContentRootPath, options.Value.Path);

    public async Task WriteAsync(RawCapture capture, byte[] content, CancellationToken cancellationToken)
    {
        var file = FileOf(capture);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        // The row is the source of truth: a file left from a deleted database (same id) is overwritten.
        await using var output = new FileStream(file, FileMode.Create);
        await using var zstd = new CompressionStream(output);
        await zstd.WriteAsync(content, cancellationToken);
    }

    public async Task<byte[]?> ReadAsync(RawCapture capture, CancellationToken cancellationToken)
    {
        var file = FileOf(capture);
        if (!File.Exists(file))
        {
            return null;
        }

        await using var input = File.OpenRead(file);
        await using var zstd = new DecompressionStream(input);
        using var content = new MemoryStream();
        await zstd.CopyToAsync(content, cancellationToken);
        return content.ToArray();
    }

    public long DeleteDaysBefore(DateOnly day)
    {
        if (!Directory.Exists(Root))
        {
            return 0;
        }

        long freedBytes = 0;
        foreach (var folder in Directory.GetDirectories(Root))
        {
            if (DateOnly.TryParseExact(Path.GetFileName(folder), DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var folderDay)
                && folderDay < day)
            {
                freedBytes += new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                Directory.Delete(folder, recursive: true);
            }
        }

        return freedBytes;
    }

    private string FileOf(RawCapture capture) =>
        Path.Combine(
            Root,
            capture.FetchedAt.UtcDateTime.ToString(DayFormat, CultureInfo.InvariantCulture),
            $"{capture.Id.ToString(CultureInfo.InvariantCulture)}.zst");
}
