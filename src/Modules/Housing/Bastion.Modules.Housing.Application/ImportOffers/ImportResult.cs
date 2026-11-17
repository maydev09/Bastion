using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ImportOffers;

public sealed record ImportResult(Source Source, int AddedCount, int UpdatedCount, int UnchangedCount, int SkippedCount);
