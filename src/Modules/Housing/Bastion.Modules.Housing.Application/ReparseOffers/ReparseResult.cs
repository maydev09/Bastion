namespace Bastion.Modules.Housing.Application.ReparseOffers;

public sealed record ReparseResult(int CaptureCount, int FailedCount, int AddedCount, int UpdatedCount, int UnchangedCount, int SkippedCount);
