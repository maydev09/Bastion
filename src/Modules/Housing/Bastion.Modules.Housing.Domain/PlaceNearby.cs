namespace Bastion.Modules.Housing.Domain;

// Table housing.place_nearby: a place of the tier nearest to PlaceId; rank 1 is the nearest of its tier.
public sealed record PlaceNearby(int PlaceId, NearbyTier Tier, int Rank, int NearbyId, decimal Km);
