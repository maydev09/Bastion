using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

public sealed record NearbyPlace(NearbyTier Tier, string Name, decimal Km);
