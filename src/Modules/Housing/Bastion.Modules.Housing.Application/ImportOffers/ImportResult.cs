using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ImportOffers;

// Missed and removed are counted only by a full scan.
public sealed record ImportResult(
    Source Source,
    int PageCount,
    int AddedCount,
    int UpdatedCount,
    int UnchangedCount,
    int SkippedCount,
    int MissedCount,
    int RemovedCount);
