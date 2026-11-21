using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.FetchDetails;

public sealed record DetailsResult(Source Source, int FetchedCount, int FailedCount, int RetriedCount);
