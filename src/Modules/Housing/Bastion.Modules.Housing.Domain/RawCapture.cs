namespace Bastion.Modules.Housing.Domain;

// Table housing.raw_capture.
public sealed record RawCapture(
    int Id,
    Source SourceId,
    CaptureKind Kind,
    string ContentType,
    Uri Url,
    int HttpStatus,
    DateTimeOffset FetchedAt,
    int Size,
    string Sha256);
