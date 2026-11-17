namespace Bastion.Modules.Housing.Domain;

public sealed record RawPage(Uri Url, CaptureKind Kind, string ContentType, int HttpStatus, byte[] Content);
